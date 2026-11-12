using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using static IsolationLabChecks;

// Isolated experiments only: this is not a production retry/locking service.
internal static class LockingLabChecks
{
    const string Increment = "UPDATE dbo.Resources WITH (ROWLOCK) SET Value=Value+@amount WHERE Id=@id";
    const string Active = "SELECT COUNT(*) FROM dbo.Advisors WHERE Active=1";
    const string Department = "SELECT Id FROM dbo.Departments WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id";
    static void Check(bool ok, string label)
    { if (!ok) throw new BehaviorFailure("Locking lab: " + label); }

    sealed class Rendezvous
    {
        readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals;
        public async Task Meet(CancellationToken ct)
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
    }

    static async Task<T[]> Pair<T>(Func<int, Rendezvous, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stop.CancelAfter(TimeSpan.FromSeconds(45));
        var gate = new Rendezvous();
        async Task<T> Guard(int id)
        {
            try { return await work(id, gate, stop.Token); }
            catch { stop.Cancel(); throw; }
        }
        // WhenAll settles both workers, including disposal/rollback, before returning a failure.
        return await Task.WhenAll(Guard(1), Guard(2));
    }

    static async Task Reset(string connection, CancellationToken ct)
    {
        await using var c = await Open(connection, ct);
        await Execute(c, null, "DELETE dbo.Marks; UPDATE dbo.Resources SET Value=0; UPDATE dbo.Advisors SET Active=1", ct);
    }

    static async Task VerifyResources(string connection, int x, int y, CancellationToken ct)
    {
        await using var c = await Open(connection, ct);
        Check(await Scalar(c, null, "SELECT Value FROM dbo.Resources WHERE Id=@id", ct, ("@id", 1)) == x, "persistent X");
        Check(await Scalar(c, null, "SELECT Value FROM dbo.Resources WHERE Id=@id", ct, ("@id", 2)) == y, "persistent Y");
    }

    // Controlled owner/waiter: no barrier requires the blocked worker to acquire its first lock.
    static async Task Ordered(string connection, bool bothRows, CancellationToken ct)
    {
        await Reset(connection, ct);
        await using var a = await Open(connection, ct);
        await using var b = await Open(connection, ct);
        await using var observer = await Open(connection, ct);
        var aId = await Scalar(a, null, "SELECT CAST(@@SPID AS int)", ct);
        var bId = await Scalar(b, null, "SELECT CAST(@@SPID AS int)", ct);
        Check(aId != bId, "separate sessions");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var tx = (SqlTransaction)await a.BeginTransactionAsync(ct);
        Task? pending = null;
        var committed = false;
        try
        {
            Check(await Execute(a, tx, Increment, ct, ("@id", 1), ("@amount", 1)) == 1, "A holds X");
            async Task Writer()
            {
                await using var other = (SqlTransaction)await b.BeginTransactionAsync(stop.Token);
                Check(await Execute(b, other, Increment, stop.Token, ("@id", 1), ("@amount", 1)) == 1, "B updates X");
                if (bothRows) Check(await Execute(b, other, Increment, stop.Token, ("@id", 2), ("@amount", 1)) == 1, "B updates Y");
                await other.CommitAsync(stop.Token);
            }
            pending = Writer();
            await ObserveLock(observer, bId, aId, pending, ct);
            if (bothRows) Check(await Execute(a, tx, Increment, ct, ("@id", 2), ("@amount", 1)) == 1, "A updates Y");
            await tx.CommitAsync(ct);
            committed = true;
            await pending;
        }
        finally
        {
            stop.Cancel();
            try { if (!committed) await tx.RollbackAsync(CancellationToken.None); }
            finally { if (pending is not null) { try { await pending; } catch when (!committed) { } } }
        }
        await VerifyResources(connection, 2, bothRows ? 2 : 0, ct);
        Console.WriteLine(bothRows ? "PASS: consistent X->Y order; both committed; X=2 Y=2" : "PASS: blocking observed; B progresses after A commit; X=2 Y=0");
    }

    static async Task LockTimeout(string connection, CancellationToken ct)
    {
        await Reset(connection, ct);
        await using var a = await Open(connection, ct);
        await using var b = await Open(connection, ct);
        await using var tx = (SqlTransaction)await a.BeginTransactionAsync(ct);
        Check(await Execute(a, tx, Increment, ct, ("@id", 1), ("@amount", 1)) == 1, "timeout blocker holds X");
        await Execute(b, null, "SET LOCK_TIMEOUT 1000", ct); // SQL wait timeout, not client command timeout.
        await using (var other = (SqlTransaction)await b.BeginTransactionAsync(ct))
        {
            await Execute(b, other, "INSERT dbo.Marks(Id) VALUES(@id)", ct, ("@id", 2));
            var timedOut = false;
            try { await Execute(b, other, Increment, ct, ("@id", 1), ("@amount", 10)); }
            catch (SqlException error) when (error.Number == 1222) { timedOut = true; }
            Check(timedOut, "real SQL 1222 required; cancellation/-2/1205 are not success");
            Check(await Scalar(b, other, "SELECT CAST(XACT_STATE() AS int)", ct) == 1, "1222 leaves transaction requiring explicit cleanup");
            await other.RollbackAsync(ct);
        }
        Check(await Scalar(b, null, "SELECT @@TRANCOUNT", ct) == 0, "timeout transaction ended");
        await tx.CommitAsync(ct);
        await VerifyResources(connection, 1, 0, ct);
        await using var verify = await Open(connection, ct);
        Check(await Scalar(verify, null, "SELECT COUNT(*) FROM dbo.Marks", ct) == 0, "pre-timeout write rolled back");
        Console.WriteLine("PASS: SQL 1222; explicit rollback removes prior write; @@TRANCOUNT=0; X=1 Y=0");
    }

    sealed record Outcome(int Id, bool Victim);
    static async Task Deadlock(string connection, CancellationToken ct)
    {
        await Reset(connection, ct);
        var results = await Pair(async (id, gate, token) =>
        {
            await using var c = await Open(connection, token);
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(token);
            try
            {
                var amount = id == 1 ? 1 : 10;
                // Distinct first rows are held before either request is sent for the other row.
                Check(await Execute(c, tx, Increment, token, ("@id", id), ("@amount", amount)) == 1, "first deadlock resource");
                await Execute(c, tx, "INSERT dbo.Marks(Id) VALUES(@id)", token, ("@id", id));
                await gate.Meet(token);
                using var command = Command(c, tx, Increment, ("@id", 3 - id), ("@amount", amount));
                command.CommandTimeout = 35; // Allow SQL deadlock monitor time, still below worker deadline.
                Check(await command.ExecuteNonQueryAsync(token) == 1, "second deadlock resource");
                await tx.CommitAsync(token);
                return new Outcome(id, false);
            }
            catch (SqlException error) when (error.Number == 1205)
            {
                // SQL Server rolls the victim transaction back. Dispose before inspecting session state.
                await tx.DisposeAsync();
                Check(await Scalar(c, null, "SELECT @@TRANCOUNT", token) == 0, "1205 victim transaction already rolled back");
                return new Outcome(id, true);
            }
        }, ct);
        Check(results.Count(r => r.Victim) == 1, "exactly one actual 1205 and one committed survivor");
        var winner = results.Single(r => !r.Victim).Id;
        var victim = results.Single(r => r.Victim).Id;
        await VerifyResources(connection, winner == 1 ? 1 : 10, winner == 1 ? 1 : 10, ct);
        await using var verify = await Open(connection, ct);
        Check(await Scalar(verify, null, "SELECT COUNT(*) FROM dbo.Marks WHERE Id=@id", ct, ("@id", victim)) == 0, "all victim writes rolled back");
        Check(await Scalar(verify, null, "SELECT COUNT(*) FROM dbo.Marks", ct) == 1 &&
            await Scalar(verify, null, "SELECT Id FROM dbo.Marks", ct) == winner, "only survivor mark persisted");
        Console.WriteLine($"PASS: real SQL 1205 victim={victim}; survivor={winner}; both victim writes rolled back; survivor committed both rows");
    }

    static async Task WriteSkew(string connection, CancellationToken ct)
    {
        await Reset(connection, ct);
        var counts = await Pair(async (id, gate, token) =>
        {
            await using var c = await Open(connection, token);
            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Snapshot, token);
            var active = await Scalar(c, tx, Active, token);
            using var read = Command(c, tx, "SELECT Version FROM dbo.Advisors WHERE Id=@id", ("@id", id));
            var version = (byte[])(await read.ExecuteScalarAsync(token))!;
            Check(active == 2 && version.Length == 8, "both snapshot decisions see two active and actual rowversion");
            await gate.Meet(token);
            using var write = Command(c, tx, "UPDATE dbo.Advisors SET Active=0 WHERE Id=@id AND Version=@version", ("@id", id));
            write.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
            Check(await write.ExecuteNonQueryAsync(token) == 1, "own rowversion still matches despite other-row change");
            await tx.CommitAsync(token);
            return active;
        }, ct);
        await using var verify = await Open(connection, ct);
        Check(counts.All(n => n == 2) && await Scalar(verify, null, Active, ct) == 0, "expected defect: at-least-one-active invariant violated");
        Console.WriteLine("PASS (expected defect): SNAPSHOT reads=2/2; distinct rowversion-checked updates both commit; active=0");
    }

    static async Task CoordinatedRule(string connection, CancellationToken ct)
    {
        await Reset(connection, ct);
        await using var a = await Open(connection, ct);
        await using var b = await Open(connection, ct);
        await using var observer = await Open(connection, ct);
        var aId = await Scalar(a, null, "SELECT CAST(@@SPID AS int)", ct);
        var bId = await Scalar(b, null, "SELECT CAST(@@SPID AS int)", ct);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var tx = (SqlTransaction)await a.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        Task<int>? pending = null;
        var committed = false;
        try
        {
            Check(await Scalar(a, tx, Department, ct, ("@id", 1)) == 1, "A holds department coordination lock");
            async Task<int> Second()
            {
                await using var other = (SqlTransaction)await b.BeginTransactionAsync(IsolationLevel.ReadCommitted, stop.Token);
                Check(await Scalar(b, other, Department, stop.Token, ("@id", 1)) == 1, "B acquires same department lock");
                var current = await Scalar(b, other, Active, stop.Token);
                if (current > 1) await Execute(b, other, "UPDATE dbo.Advisors SET Active=0 WHERE Id=@id", stop.Token, ("@id", 2));
                await other.CommitAsync(stop.Token);
                return current;
            }
            pending = Second();
            await ObserveLock(observer, bId, aId, pending, ct);
            var first = await Scalar(a, tx, Active, ct);
            Check(first == 2, "A reads current count after lock");
            if (first > 1) Check(await Execute(a, tx, "UPDATE dbo.Advisors SET Active=0 WHERE Id=@id", ct, ("@id", 1)) == 1, "A deactivates one");
            await tx.CommitAsync(ct);
            committed = true;
            Check(await pending == 1, "B sees latest count after acquiring lock and leaves remaining advisor active");
        }
        finally
        {
            stop.Cancel();
            try { if (!committed) await tx.RollbackAsync(CancellationToken.None); }
            finally { if (pending is not null) { try { await pending; } catch when (!committed) { } } }
        }
        await using var verify = await Open(connection, ct);
        Check(await Scalar(verify, null, Active, ct) == 1 &&
            await Scalar(verify, null, "SELECT Id FROM dbo.Advisors WHERE Active=1", ct) == 2, "fixed comparison persistent active=1");
        Console.WriteLine("PASS: RC department UPDLOCK,HOLDLOCK; counts=2/1; active=1; independent rule assertion");
    }

    public static async Task Run(string source, bool probe)
    {
        var settings = new SqlConnectionStringBuilder(source) { Pooling = false };
        var name = settings.InitialCatalog;
        Check(settings.DataSource == @"(localdb)\MSSQLLocalDB" && Regex.IsMatch(name, @"^university_registration_tests_day40_[a-f0-9]{32}$"), "owned target only");
        var connection = settings.ConnectionString;
        settings.InitialCatalog = "master";
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = deadline.Token;
        await using var admin = await Open(settings.ConnectionString, ct);
        using var quote = new SqlCommandBuilder();
        var identifier = quote.QuoteIdentifier(name);
        async Task<bool> Exists(CancellationToken token)
        {
            using var cmd = new SqlCommand("SELECT DB_ID(@name)", admin) { CommandTimeout = 15 };
            cmd.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = name;
            return await cmd.ExecuteScalarAsync(token) is not DBNull;
        }
        Check(!await Exists(ct), "refuse pre-existing DB");
        var created = false;
        try
        {
            await Execute(admin, null, $"CREATE DATABASE {identifier}", ct);
            created = true;
            // Only own DB, outside all experiment connections/transactions; no forced disconnections.
            await Execute(admin, null, $"ALTER DATABASE {identifier} SET READ_COMMITTED_SNAPSHOT OFF", ct);
            await Execute(admin, null, $"ALTER DATABASE {identifier} SET ALLOW_SNAPSHOT_ISOLATION ON", ct);
            using (var cmd = new SqlCommand("SELECT is_read_committed_snapshot_on,snapshot_isolation_state FROM sys.databases WHERE name=@name", admin))
            {
                cmd.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = name;
                await using var r = await cmd.ExecuteReaderAsync(ct);
                Check(await r.ReadAsync(ct) && !r.GetBoolean(0) && r.GetByte(1) == 1, "RCSI OFF and SNAPSHOT ON verified");
            }
            await using (var c = await Open(connection, ct))
                await Execute(c, null, """
                    CREATE TABLE dbo.Resources(Id int NOT NULL PRIMARY KEY, Value int NOT NULL);
                    CREATE TABLE dbo.Marks(Id int NOT NULL PRIMARY KEY);
                    CREATE TABLE dbo.Departments(Id int NOT NULL PRIMARY KEY);
                    CREATE TABLE dbo.Advisors(Id int NOT NULL PRIMARY KEY, Active bit NOT NULL, Version rowversion NOT NULL);
                    INSERT dbo.Resources VALUES(1,0),(2,0);
                    INSERT dbo.Departments VALUES(1);
                    INSERT dbo.Advisors(Id,Active) VALUES(1,1),(2,1);
                    """, ct);
            await Ordered(connection, false, ct);
            await LockTimeout(connection, ct);
            await Deadlock(connection, ct);
            await Ordered(connection, true, ct);
            await WriteSkew(connection, ct);
            await CoordinatedRule(connection, ct);
            Check(!probe, "controlled runner failure probe");
        }
        finally
        {
            if (created)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    await Execute(admin, null, $"DROP DATABASE {identifier}", cleanup.Token);
                    Check(!await Exists(cleanup.Token), "owned DB removed");
                    Console.WriteLine("PASS: owned locking DB and four tables removed; connections/tasks settled; no forced disconnects");
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("CLEANUP locking failed: " + error.GetType().Name);
                    throw new CleanupFailure();
                }
            }
        }
    }
}
