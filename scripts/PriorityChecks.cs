using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using UniversityRegistration.Priority;

static class PriorityChecks
{
    static void Check(bool value, string scenario) { if (!value) throw new BehaviorFailure("Priority: " + scenario); }
    public static void Unit()
    {
        IRegistrationPriorityStrategy graduation = new GraduationPriorityStrategy();
        IRegistrationPriorityStrategy completed = new CompletedCreditsPriorityStrategy();
        foreach (var (credits, expected) in new[] { (219,0), (220,100), (221,100), (240,100), (250,100) })
            Check(graduation.Calculate(new(credits,240)) == expected, $"graduation {credits}/240 expected {expected}");
        foreach (var credits in new[] { 0,120,225,int.MaxValue })
            Check(completed.Calculate(new(credits,240)) == credits, $"completed {credits} preserved");
        Check(graduation.Calculate(new(0,int.MaxValue)) == 0 && graduation.Calculate(new(int.MaxValue,1)) == 100, "Int32 extremes");
        foreach (var (c,r,parameter) in new[] {(-1,240,"completedCredits"),(0,0,"requiredCredits"),(0,-1,"requiredCredits")})
        {
            try { _ = new PriorityInput(c,r); throw new BehaviorFailure("Invalid input accepted"); }
            catch (ArgumentOutOfRangeException ex) { Check(ex.ParamName == parameter, "constructor parameter"); }
        }
        foreach (var strategy in new[] {graduation,completed})
        {
            try { strategy.Calculate(null!); throw new BehaviorFailure("Null input accepted"); }
            catch (ArgumentNullException ex) { Check(ex.ParamName == "input", "null input parameter"); }
            var input = new PriorityInput(225,240);
            Check(strategy.Calculate(input) == strategy.Calculate(input), "deterministic result");
        }
        Console.WriteLine("PASS: priority unit boundaries, immutable constructor rules, null, determinism and Int32 extremes (no DB)");
    }

    public static async Task Http(string root)
    {
        // Reserve an unused TCP port WITHOUT listening. No SQL Server is reachable here.
        using var unavailable = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        unavailable.ExclusiveAddressUse = true;
        unavailable.Bind(new IPEndPoint(IPAddress.Loopback,0));
        var dbPort = ((IPEndPoint)unavailable.LocalEndPoint!).Port;
        var listener = new TcpListener(IPAddress.Loopback,0);
        listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var connection = new SqlConnectionStringBuilder { DataSource = $"tcp:127.0.0.1,{dbPort}",
            InitialCatalog = "priority_preview_unreachable_test", IntegratedSecurity = true,
            Encrypt = true, TrustServerCertificate = true, ConnectTimeout = 1, ConnectRetryCount = 0 }.ConnectionString;
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(root,"bin/Debug/net10.0/UniversityRegistration.dll"));
        info.ArgumentList.Add("--urls"); info.ArgumentList.Add($"http://127.0.0.1:{port}");
        info.Environment["ConnectionStrings__UniversityDatabase"] = connection;
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        info.Environment["DOTNET_ENVIRONMENT"] = "Development";
        using var api = Process.Start(info)!;
        api.OutputDataReceived += (_,_) => { }; api.ErrorDataReceived += (_,_) => { };
        api.BeginOutputReadLine(); api.BeginErrorReadLine();
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
        Exception? failure = null;
        try
        {
            var ready = false;
            for (var i=0;i<50;i++)
            {
                if(api.HasExited) throw new InvalidOperationException("Preview API exited before readiness");
                try { using var response = await client.GetAsync("/priority-readiness-not-a-route"); ready = response.StatusCode == HttpStatusCode.NotFound; }
                catch(HttpRequestException) { }
                if(ready) break;
                await Task.Delay(200);
            }
            Check(ready,"bounded API readiness");
            async Task<JsonNode> Post(string json, int expected)
            {
                using var response = await client.PostAsync("/registration-priority/preview",new StringContent(json,Encoding.UTF8,"application/json"));
                Check((int)response.StatusCode == expected,$"HTTP expected {expected}, actual {(int)response.StatusCode}");
                Check(response.Content.Headers.ContentType?.MediaType == (expected==200?"application/json":"application/problem+json"),"content type");
                var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                if(expected==400) Check(body["status"]!.GetValue<int>()==400,"problem status");
                return body;
            }
            foreach(var (policy, normalized, score) in new[] {("graduation","graduation",100),("completed-credits","completed-credits",225),("  GrAdUaTiOn  ","graduation",100)})
            {
                var body = await Post(System.Text.Json.JsonSerializer.Serialize(new {policy,completedCredits=225,requiredCredits=240}),200);
                Check(string.Join(',',body.AsObject().Select(p=>p.Key).Order())=="policy,score","only policy/score response");
                Check(body["policy"]!.GetValue<string>()==normalized && body["score"]!.GetValue<int>()==score,"normalized policy and score");
            }
            var zero=await Post("{\"policy\":\"completed-credits\",\"completedCredits\":0,\"requiredCredits\":240}",200);
            Check(zero["score"]!.GetValue<int>()==0,"zero credits accepted");
            foreach(var policy in new string?[]{null,"","  ","unknown"})
            {
                var body=await Post(System.Text.Json.JsonSerializer.Serialize(new {policy,completedCredits=225,requiredCredits=240}),400);
                Check(body["errors"]!.AsObject().ContainsKey("policy"),"policy error field");
            }
            foreach(var json in new[]{"{}","{\"policy\":null,\"completedCredits\":null,\"requiredCredits\":null}","{\"policy\":\"unknown\",\"completedCredits\":-1,\"requiredCredits\":0}","{\"policy\":\"\",\"completedCredits\":-1,\"requiredCredits\":-1}"})
            {
                var body=await Post(json,400);
                Check(string.Join(',',body["errors"]!.AsObject().Select(p=>p.Key).Order())=="completedCredits,policy,requiredCredits","aggregate fields");
            }
            foreach(var json in new[]{"{", "{\"policy\":5}", "{\"completedCredits\":\"225\"}","{\"requiredCredits\":true}","{\"completedCredits\":2147483648}","{\"requiredCredits\":-2147483649}"})
                await Post(json,400);
            using var dbResponse=await client.GetAsync("/courses");
            Check((int)dbResponse.StatusCode==500,"control: DB-dependent endpoint fails on unreachable DB");
            var after=await Post("{\"policy\":\"graduation\",\"completedCredits\":225,\"requiredCredits\":240}",200);
            Check(after["score"]!.GetValue<int>()==100,"preview still works after failed DB request");
            Console.WriteLine("PASS: preview real HTTP policies/validation/binding; unavailable SQL target gives courses=500 and preview=200");
        }
        catch(Exception ex) { failure=ex; throw; }
        finally
        {
            try { if(!api.HasExited) { api.Kill(true); await api.WaitForExitAsync(); } Console.WriteLine("PASS: preview process stopped; no DB created or changed"); }
            catch(Exception ex) { Console.Error.WriteLine("CLEANUP preview process: "+ex.GetType().Name); if(failure is null) throw; }
        }
    }
}
