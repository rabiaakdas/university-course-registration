using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

// Test-only SqlClient queries: no tracked entities, production schema or migrations.
internal static class IsolationLabChecks
{
    const string Read = "SELECT Value FROM dbo.IsolationRows WHERE Id=@id";
    const string Count = "SELECT COUNT(*) FROM dbo.IsolationRows WHERE Bucket=@bucket";
    const string Update = "UPDATE dbo.IsolationRows SET Value=@value WHERE Id=@id";
    const string Insert = "INSERT dbo.IsolationRows(Id,Bucket,Value) VALUES(@id,@bucket,@value)";
    static void Check(bool ok, string label)
    { if (!ok) throw new BehaviorFailure("Isolation lab: " + label); }

    internal static SqlCommand Command(SqlConnection c, SqlTransaction? tx, string sql,
        params (string Name, int Value)[] values)
    {
        var command = new SqlCommand(sql, c, tx) { CommandTimeout = 15 };
        foreach (var (name, value) in values) command.Parameters.Add(name, SqlDbType.Int).Value = value;
        return command;
    }

    internal static async Task<int> Scalar(SqlConnection c, SqlTransaction? tx, string sql, CancellationToken ct,
        params (string Name, int Value)[] values)
    {
        using var command = Command(c, tx, sql, values);
        // Every query used here returns SQL int, including the explicit @@SPID cast.
        return (int)(await command.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Missing scalar"));
    }

    internal static async Task<int> Execute(SqlConnection c, SqlTransaction? tx, string sql, CancellationToken ct,
        params (string Name, int Value)[] values)
    {
        using var command = Command(c, tx, sql, values);
        return await command.ExecuteNonQueryAsync(ct);
    }

    internal static async Task<SqlConnection> Open(string connection, CancellationToken ct)
    {
        var c = new SqlConnection(connection);
        try { await c.OpenAsync(ct); return c; }
        catch { await c.DisposeAsync(); throw; }
    }

    static async Task Reset(string connection, CancellationToken ct)
    {
        await using var c = await Open(connection, ct);
        await Execute(c, null, "DELETE FROM dbo.IsolationRows", ct);
        Check(await Execute(c, null, Insert, ct, ("@id", 1), ("@bucket", 7), ("@value", 10)) == 1, "seed row");
    }

    static async Task Final(string connection, int value, int count, CancellationToken ct)
    {
        await using var c = await Open(connection, ct);
        Check(await Scalar(c, null, Read, ct, ("@id", 1)) == value, "fresh connection persistent value");
        Check(await Scalar(c, null, Count, ct, ("@bucket", 7)) == count, "fresh connection persistent count");
    }

    // Completion is coordinated by observed DB lock state, not by elapsed time.
    // The timer only throttles DMV sampling; its tick is never accepted as lock evidence.
    internal static async Task ObserveLock(SqlConnection observer, int waiter, int blocker, Task pending, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        using var poll = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        while (true)
        {
            if (pending.IsCompleted)
            {
                await pending; // Preserve a SQL error rather than misreporting it as a lock observation.
                throw new BehaviorFailure("Expected SQL lock wait, but command completed before release");
            }
            using var command = Command(observer, null,
                "SELECT wait_type FROM sys.dm_exec_requests WHERE session_id=@waiter AND blocking_session_id=@blocker AND wait_type LIKE N'LCK[_]M[_]%'",
                ("@waiter", waiter), ("@blocker", blocker));
            var wait = await command.ExecuteScalarAsync(deadline.Token);
            if (wait is string kind)
            {
                Console.WriteLine($"OBSERVED: {kind}; waiter={waiter}, blocker={blocker}");
                return;
            }
            await poll.WaitForNextTickAsync(deadline.Token);
        }
    }

    // RC: writer owns X lock, reader waits. RR/serializable: reader owns locks, writer waits.
    static async Task Blocking(string connection, IsolationLevel level, bool insert, CancellationToken ct)
    {
        await Reset(connection, ct);
        await using var owner = await Open(connection, ct);
        await using var waiter = await Open(connection, ct);
        await using var observer = await Open(connection, ct);
        var ownerId = await Scalar(owner, null, "SELECT CAST(@@SPID AS int)", ct);
        var waiterId = await Scalar(waiter, null, "SELECT CAST(@@SPID AS int)", ct);
        Check(ownerId != waiterId, "independent connections");
        using var work = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var tx = (SqlTransaction)await owner.BeginTransactionAsync(level, ct);
        Task<int>? pending = null;
        var ended = false;
        try
        {
            if (level == IsolationLevel.ReadCommitted)
            {
                Check(await Execute(owner, tx, Update, ct, ("@id", 1), ("@value", 99)) == 1, "uncommitted update");
                pending = Scalar(waiter, null, Read, work.Token, ("@id", 1));
            }
            else
            {
                Check(await Scalar(owner, tx, insert ? Count : Read, ct,
                    (insert ? "@bucket" : "@id", insert ? 7 : 1)) == (insert ? 1 : 10), "initial locked read");
                pending = insert
                    ? Execute(waiter, null, Insert, work.Token, ("@id", 2), ("@bucket", 7), ("@value", 20))
                    : Execute(waiter, null, Update, work.Token, ("@id", 1), ("@value", 20));
            }
            await ObserveLock(observer, waiterId, ownerId, pending, ct);
            if (level == IsolationLevel.ReadCommitted) await tx.RollbackAsync(ct);
            else await tx.CommitAsync(ct);
            ended = true;
            Check(await pending == (level == IsolationLevel.ReadCommitted ? 10 : 1), "waiter result after owner release");
        }
        finally
        {
            // On failure cancel/settle the separate worker and release our transaction before leaving.
            work.Cancel();
            try { if (!ended) await tx.RollbackAsync(CancellationToken.None); }
            finally { if (pending is not null) { try { await pending; } catch when (!ended) { } } }
        }
        await Final(connection, level == IsolationLevel.RepeatableRead ? 20 : 10, insert ? 2 : 1, ct);
        Console.WriteLine($"PASS: {level} {(insert ? "matching INSERT" : level == IsolationLevel.ReadCommitted ? "dirty read" : "row UPDATE")} blocked; release and persistent result verified");
    }

    static async Task DirtyRead(string connection, CancellationToken ct)
    {
        await Reset(connection, ct);
        await using var writer = await Open(connection, ct);
        await using var reader = await Open(connection, ct);
        await using var write = (SqlTransaction)await writer.BeginTransactionAsync(ct);
        await using var read = (SqlTransaction)await reader.BeginTransactionAsync(IsolationLevel.ReadUncommitted, ct);
        Check(await Execute(writer, write, Update, ct, ("@id", 1), ("@value", 99)) == 1, "dirty write");
        Check(await Scalar(reader, read, Read, ct, ("@id", 1)) == 99, "uncommitted value visible");
        await write.RollbackAsync(ct);
        await read.CommitAsync(ct);
        await Final(connection, 10, 1, ct);
        Console.WriteLine("PASS: READ UNCOMMITTED read=99; writer rollback; persistent=10");
    }

    static async Task TwoReads(string connection, IsolationLevel level, bool phantom, string label, CancellationToken ct)
    {
        await Reset(connection, ct);
        await using var reader = await Open(connection, ct);
        await using var writer = await Open(connection, ct);
        await using var tx = (SqlTransaction)await reader.BeginTransactionAsync(level, ct);
        var sql = phantom ? Count : Read;
        var parameter = (phantom ? "@bucket" : "@id", phantom ? 7 : 1);
        var first = await Scalar(reader, tx, sql, ct, parameter); // Establish SNAPSHOT here.
        Check(first == (phantom ? 1 : 10), "first read");
        Check(await Execute(writer, null, phantom ? Insert : Update, ct,
            ("@id", phantom ? 2 : 1), ("@bucket", 7), ("@value", 20)) == 1, "independent committed write");
        var second = await Scalar(reader, tx, sql, ct, parameter);
        Check(second == (level == IsolationLevel.Snapshot ? first : phantom ? 2 : 20), "second read visibility");
        await tx.CommitAsync(ct);
        await using var next = (SqlTransaction)await reader.BeginTransactionAsync(level, ct);
        Check(await Scalar(reader, next, sql, ct, parameter) == (phantom ? 2 : 20), "new transaction sees latest commit");
        await next.CommitAsync(ct);
        await Final(connection, phantom ? 10 : 20, phantom ? 2 : 1, ct);
        Console.WriteLine($"PASS: {label} reads={first}/{second}; new transaction={(phantom ? 2 : 20)}; persistent state verified");
    }

    static async Task RcsiUncommitted(string connection, CancellationToken ct)
    {
        await Reset(connection, ct);
        await using var writer = await Open(connection, ct);
        await using var reader = await Open(connection, ct);
        await using var tx = (SqlTransaction)await writer.BeginTransactionAsync(ct);
        Check(await Execute(writer, tx, Update, ct, ("@id", 1), ("@value", 99)) == 1, "RCSI uncommitted update");
        Check(await Scalar(reader, null, Read, ct, ("@id", 1)) == 10, "RCSI read completes with committed version while writer still open");
        await tx.RollbackAsync(ct);
        await Final(connection, 10, 1, ct);
        Console.WriteLine("PASS: RCSI returns committed=10 while uncommitted=99 remains open; rollback persistent=10");
    }

    public static async Task Run(string source, bool probe)
    {
        var settings = new SqlConnectionStringBuilder(source) { Pooling = false };
        var name = settings.InitialCatalog;
        Check(settings.DataSource == @"(localdb)\MSSQLLocalDB" &&
            Regex.IsMatch(name, @"^university_registration_tests_day39_[a-f0-9]{32}$"), "owned fresh database only");
        var connection = settings.ConnectionString;
        settings.InitialCatalog = "master";
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = deadline.Token;
        await using var admin = await Open(settings.ConnectionString, ct);
        using var quote = new SqlCommandBuilder();
        var identifier = quote.QuoteIdentifier(name); // Identifiers cannot be SQL value parameters.
        async Task<bool> Exists(CancellationToken token)
        {
            using var cmd = new SqlCommand("SELECT DB_ID(@name)", admin) { CommandTimeout = 15 };
            cmd.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = name;
            return await cmd.ExecuteScalarAsync(token) is not DBNull;
        }
        Check(!await Exists(ct), "refuse existing database");
        var created = false;
        try
        {
            await Execute(admin, null, $"CREATE DATABASE {identifier}", ct);
            created = true;
            async Task Options(bool rcsi, bool snapshot)
            {
                // No experiment connection/transaction is alive at these calls. No forced disconnection.
                await Execute(admin, null, $"ALTER DATABASE {identifier} SET READ_COMMITTED_SNAPSHOT {(rcsi ? "ON" : "OFF")}", ct);
                await Execute(admin, null, $"ALTER DATABASE {identifier} SET ALLOW_SNAPSHOT_ISOLATION {(snapshot ? "ON" : "OFF")}", ct);
                using var cmd = new SqlCommand("SELECT is_read_committed_snapshot_on, snapshot_isolation_state FROM sys.databases WHERE name=@name", admin);
                cmd.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = name;
                await using var result = await cmd.ExecuteReaderAsync(ct);
                Check(await result.ReadAsync(ct) && result.GetBoolean(0) == rcsi && result.GetByte(1) == (snapshot ? 1 : 0), "independent database options");
                Console.WriteLine($"OPTIONS verified: RCSI={rcsi}; ALLOW_SNAPSHOT_ISOLATION={snapshot}");
            }
            await Options(false, false);
            await using (var c = await Open(connection, ct))
                await Execute(c, null, "CREATE TABLE dbo.IsolationRows(Id int NOT NULL PRIMARY KEY, Bucket int NOT NULL, Value int NOT NULL); CREATE INDEX IX_IsolationRows_Bucket ON dbo.IsolationRows(Bucket)", ct);
            await DirtyRead(connection, ct);
            await Blocking(connection, IsolationLevel.ReadCommitted, false, ct);
            await TwoReads(connection, IsolationLevel.ReadCommitted, false, "RC non-repeatable read (RCSI OFF)", ct);
            await Blocking(connection, IsolationLevel.RepeatableRead, false, ct);
            await TwoReads(connection, IsolationLevel.RepeatableRead, true, "RR phantom", ct);
            await Blocking(connection, IsolationLevel.Serializable, true, ct);
            await Options(true, false);
            await RcsiUncommitted(connection, ct);
            await TwoReads(connection, IsolationLevel.ReadCommitted, false, "RCSI statement snapshots", ct);
            await Options(false, true);
            await TwoReads(connection, IsolationLevel.Snapshot, false, "SNAPSHOT transaction view", ct);
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
                    Check(!await Exists(cleanup.Token), "owned database removed");
                    Console.WriteLine("PASS: owned isolation DB/table removed; unpooled experiment connections disposed; no forced disconnects");
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("CLEANUP isolation failed: " + error.GetType().Name);
                    throw new CleanupFailure();
                }
            }
        }
    }
}
