using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class AuthAbuseChecks
{
    static void Check(bool value,string name) { if(!value)throw new BehaviorFailure("Auth abuse: "+name); }
    public sealed class Observation
    {
        public IPAddress? Address=IPAddress.Parse("192.0.2.1"); // Test connection feature, never a request header.
        public int Hashes,DummyHashes,RealVerifies,DummyVerifies,Tokens;
        public string? DummyId;
        public ConcurrentBag<string> Secrets=new();
        public bool FailRead;
    }
    public sealed class Hasher(IOptions<PasswordHasherOptions> options,Observation observed) : IPasswordHasher<ApplicationUser>
    {
        readonly PasswordHasher<ApplicationUser> inner=new(options);
        public string HashPassword(ApplicationUser user,string password)
        {
            Interlocked.Increment(ref observed.Hashes);
            if(user.UserName is null){observed.DummyId=user.Id;Interlocked.Increment(ref observed.DummyHashes);}
            var hash=inner.HashPassword(user,password);observed.Secrets.Add(hash);observed.Secrets.Add(password);return hash;
        }
        public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user,string hash,string password)
        {
            if(user.Id==observed.DummyId)Interlocked.Increment(ref observed.DummyVerifies);else Interlocked.Increment(ref observed.RealVerifies);
            observed.Secrets.Add(password);observed.Secrets.Add(hash);return inner.VerifyHashedPassword(user,hash,password);
        }
    }
    public sealed class ResetProvider(PasswordResetTokenProvider inner,Observation observed) : IUserTwoFactorTokenProvider<ApplicationUser>
    {
        public async Task<string> GenerateAsync(string purpose,UserManager<ApplicationUser> manager,ApplicationUser user)
        {Interlocked.Increment(ref observed.Tokens);var token=await inner.GenerateAsync(purpose,manager,user);observed.Secrets.Add(token);return token;}
        public Task<bool> ValidateAsync(string purpose,string token,UserManager<ApplicationUser> manager,ApplicationUser user)=>inner.ValidateAsync(purpose,token,manager,user);
        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager,ApplicationUser user)=>inner.CanGenerateTwoFactorTokenAsync(manager,user);
    }
    public sealed class ReadFault(Observation observed) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(observed.FailRead && command.CommandText.Contains("[AspNetUsers]")) {observed.FailRead=false;command.CommandText=command.CommandText.Replace("[AspNetUsers]","[MissingAuthAbuseTable]");}
            return ValueTask.FromResult(result);
        }
    }
    public static void Configure(WebApplicationBuilder b,Observation observed)
    {
        b.Services.AddSingleton(observed);b.Services.AddScoped<IPasswordHasher<ApplicationUser>,Hasher>();
        b.Services.AddTransient<ResetProvider>();
        b.Services.Configure<IdentityOptions>(o=>o.Tokens.ProviderMap[PasswordResetTokenProvider.ProviderName]=new(typeof(ResetProvider)));
        b.Services.AddDbContext<UniversityDbContext>(o=>o.AddInterceptors(new ReadFault(observed)));
    }
    public static void Unit(bool probe=false)
    {
        var values=new Dictionary<string,string?> {
            ["AuthenticationLimits:Login:PermitLimit"]="10",["AuthenticationLimits:Login:WindowSeconds"]="60",
            ["AuthenticationLimits:ForgotPassword:PermitLimit"]="3",["AuthenticationLimits:ForgotPassword:WindowSeconds"]="900"};
        foreach(var key in values.Keys) foreach(var bad in new string?[]{null,"","0","-1","NaN","1.5","2147483648","86401"})
        {
            var copy=new Dictionary<string,string?>(values);copy[key]=bad;
            try{new ServiceCollection().AddAuthenticationAbuse(new ConfigurationBuilder().AddInMemoryCollection(copy).Build());throw new BehaviorFailure("Bad quota accepted");}
            catch(InvalidOperationException e){Check(e.Message.StartsWith(key,StringComparison.Ordinal),"safe explicit configuration failure");}
        }
        new ServiceCollection().AddAuthenticationAbuse(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        Check(AuthenticationAbuse.PartitionKey(null)==AuthenticationAbuse.PartitionKey(null),"stable missing peer");
        Check(AuthenticationAbuse.PartitionKey(IPAddress.Parse("127.0.0.1"))==AuthenticationAbuse.PartitionKey(IPAddress.Parse("::ffff:127.0.0.1")),"mapped IPv4 normalization");
        Check(!probe,"controlled failure probe");
        Console.WriteLine("PASS: all limiter settings reject absent/empty/zero/negative/malformed/overflow/out-of-range; stable fallback and mapped IPv4 key");
    }
    public static async Task Run(WebApplication app,Func<HttpClient> clients,Func<UniversityDbContext> factory,
        Func<string,bool,Task<ApplicationUser>> create,ApplicationUser ready,ApplicationUser pending,string password,
        List<string> secrets,Observation observed,JwtChecks.SqlCounter queries,PasswordResetChecks.Sender sender,IEnumerable<string> logs)
    {
        async Task<T> Manager<T>(Func<UserManager<ApplicationUser>,Task<T>> action)
        {await using var scope=app.Services.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());}
        async Task<ApplicationUser> Read(string id){await using var db=factory();return await db.Users.AsNoTracking().SingleAsync(u=>u.Id==id);}
        async Task<string> Csrf(HttpClient c){using var r=await c.GetAsync("/accounts/antiforgery");Check((int)r.StatusCode==200,"CSRF token");var token=JsonNode.Parse(await r.Content.ReadAsStringAsync())!["requestToken"]!.GetValue<string>();secrets.Add(token);return token;}
        async Task<(int Status,string Body,string? ContentType,int? Retry)> Post(HttpClient c,string path,object body,string? csrf=null,string? forwarded=null)
        {
            using var q=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
            if(csrf is not null)q.Headers.Add("X-CSRF-TOKEN",csrf);if(forwarded is not null)q.Headers.Add("X-Forwarded-For",forwarded);
            using var r=await c.SendAsync(q);var text=await r.Content.ReadAsStringAsync();
            Check(r.Headers.Location is null && !text.Contains("<html",StringComparison.OrdinalIgnoreCase),"no redirect/HTML");
            if(r.Headers.TryGetValues("Set-Cookie",out var cookies))foreach(var cookie in cookies)
            {
                var value=cookie.Split(';')[0].Split('=',2).ElementAtOrDefault(1);if(!string.IsNullOrEmpty(value))secrets.Add(value);
                Check((int)r.StatusCode==200 || !cookie.StartsWith("__Host-University.Account="),"no auth cookie on failure");
            }
            int? retry=r.Headers.RetryAfter?.Delta is { } delay ? (int)delay.TotalSeconds : null;
            if((int)r.StatusCode==429)Check(r.Headers.CacheControl?.NoStore==true && retry is >=0 and <=5,"metadata Retry-After/no-store");
            return((int)r.StatusCode,text,r.Content.Headers.ContentType?.ToString(),retry);
        }
        void Peer(int n)=>observed.Address=IPAddress.Parse("192.0.2."+n);
        var lockUser=await create("abuse-lock",true);var other=await create("abuse-other",true);
        using var anon=clients();var csrf=await Csrf(anon);
        var identity=app.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Check(identity.Lockout.MaxFailedAccessAttempts==5 && identity.Lockout.DefaultLockoutTimeSpan==TimeSpan.FromMinutes(5) && identity.Lockout.AllowedForNewUsers,"production lockout unchanged");
        Check(observed.DummyHashes==1,"one startup dummy hash");
        Peer(1);var d=observed.DummyVerifies;var real=observed.RealVerifies;
        var missing=await Post(anon,"/accounts/login",new{email="absent-"+Guid.NewGuid().ToString("N")+"@example.test",password},csrf);
        Check(missing.Status==401 && observed.DummyVerifies==d+1 && observed.RealVerifies==real,"missing uses real hasher dummy once");
        var bad=await Post(anon,"/accounts/login",new{email=ready.Email,password="wrong-password"},csrf);
        Check(observed.RealVerifies==real+1 && observed.DummyVerifies==d+1,"real wrong uses one real check");
        var unconfirmed=await Post(anon,"/accounts/login",new{email=pending.Email,password},csrf);
        Check((await Manager(async m=>await m.SetLockoutEndDateAsync((await m.FindByIdAsync(other.Id))!,DateTimeOffset.UtcNow.AddMinutes(5)))).Succeeded,"locked fixture");
        var locked=await Post(anon,"/accounts/login",new{email=other.Email,password},csrf);
        foreach(var r in new[]{bad,unconfirmed,locked})Check(r.Status==401 && r.Body==missing.Body && r.ContentType==missing.ContentType,"same 401 body/type");
        Check(observed.RealVerifies==real+1,"confirmed/lock precheck may skip hashing (documented timing limit)");
        Check((await Post(anon,"/accounts/login",new{email=" ",password=""},csrf)).Status==400,"structural400");
        Check((await Post(anon,"/accounts/login",new{email=ready.Email,password})).Status==400,"missing CSRF");
        Check((await Post(anon,"/accounts/login",new{email=ready.Email,password},"invalid")).Status==400,"bad CSRF");
        using var signed=clients();var signedCsrf=await Csrf(signed);Peer(2);real=observed.RealVerifies;d=observed.DummyVerifies;
        Check((await Post(signed,"/accounts/login",new{email=" "+ready.Email!.ToUpperInvariant()+" ",password},signedCsrf)).Status==200,"normalized exact-password login");
        Check(observed.RealVerifies==real+1 && observed.DummyVerifies==d && (await Read(ready.Id)).AccessFailedCount==0,"no double check and successful count reset");
        using(var me=await signed.GetAsync("/accounts/me"))Check((int)me.StatusCode==200,"signed me");
        signedCsrf=await Csrf(signed);
        var minted=await Post(signed,"/accounts/dev-token",new{},signedCsrf);Check(minted.Status==200,"JWT/session fixture");
        var pair=JsonNode.Parse(minted.Body)!;var access=pair["accessToken"]!.GetValue<string>();secrets.Add(access);secrets.Add(pair["refreshToken"]!.GetValue<string>());
        // Lockout is separate from the IP quota: five failures, with fresh quota and no manual counter update.
        Peer(3);var otherBefore=await Read(other.Id);
        for(var i=1;i<=5;i++)
        {
            var r=await Post(anon,"/accounts/login",new{email=lockUser.Email,password="wrong-password"},csrf);Check(r.Status==401,"lockout failure not rate limited");
            var saved=await Read(lockUser.Id);Check(saved.LockoutEnabled && saved.AccessFailedCount==(i==5?0:i),"one increment/reset at threshold");
            if(i==5)Check(saved.LockoutEnd>DateTimeOffset.UtcNow.AddMinutes(4),"real five-minute lockout persisted");
        }
        var blocked=await Post(anon,"/accounts/login",new{email=lockUser.Email,password},csrf);Check(blocked.Status==401 && blocked.Body==missing.Body,"locked right password common401");
        Check((await Manager(async m=>await m.SetLockoutEndDateAsync((await m.FindByIdAsync(lockUser.Id))!,DateTimeOffset.UtcNow.AddSeconds(-1)))).Succeeded,"controlled elapsed lockout fixture");
        using var recovered=clients();Check((await Post(recovered,"/accounts/login",new{email=lockUser.Email,password},await Csrf(recovered))).Status==200,"past lockout permits login");
        Check((await Read(lockUser.Id)).AccessFailedCount==0 && (await Read(other.Id)).LockoutEnd==otherBefore.LockoutEnd,"reset and unrelated account intact");
        // Existing credentials are not revoked merely by locking the account.
        Check((await Manager(async m=>await m.SetLockoutEndDateAsync((await m.FindByIdAsync(ready.Id))!,DateTimeOffset.UtcNow.AddMinutes(5)))).Succeeded,"ready lock fixture");
        using(var me=await signed.GetAsync("/accounts/me"))Check((int)me.StatusCode==200,"cookie not immediately revoked by lockout");
        using(var q=new HttpRequestMessage(HttpMethod.Get,"/accounts/bearer-me")){q.Headers.Authorization=new("Bearer",access);using var r=await anon.SendAsync(q);Check((int)r.StatusCode==200,"JWT not revoked by lockout");}
        Check((await Manager(async m=>await m.SetLockoutEndDateAsync((await m.FindByIdAsync(ready.Id))!,null))).Succeeded,"clear owned lock fixture");
        // Ten different emails exhaust one peer's quota. No header-based partition escape.
        Peer(10);
        for(var i=0;i<10;i++)Check((await Post(anon,"/accounts/login",new{email=$"missing{i}@example.test",password},csrf)).Status==401,"quota initial permits");
        real=observed.RealVerifies;d=observed.DummyVerifies;var reads=queries.Reads;var countBefore=(await Read(ready.Id)).AccessFailedCount;
        var rate=await Post(anon,"/accounts/login",new{email=ready.Email,password="wrong-password"},csrf,"198.51.100.99");
        Check(rate.Status==429 && observed.RealVerifies==real && observed.DummyVerifies==d && queries.Reads==reads,"429 before DB/hash, spoofed XFF ineffective");
        Check((await Read(ready.Id)).AccessFailedCount==countBefore,"429 no failure count");
        observed.Address=IPAddress.Parse("::ffff:192.0.2.10");Check((await Post(anon,"/accounts/login",new{email="another@example.test",password},csrf)).Status==429,"mapped IPv4 same quota");
        Peer(11);Check((await Post(anon,"/accounts/login",new{email="another@example.test",password},csrf)).Status==401,"different controlled connection peer independent");Peer(10);
        async Task<string> Snapshot()
        {await using var db=factory();var u=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==ready.Id);return u.PasswordHash+"/"+u.SecurityStamp+"/"+u.AccessFailedCount+"/"+string.Join('|',await db.RefreshSessions.Where(s=>s.UserId==ready.Id).OrderBy(s=>s.Id).Select(s=>s.Id+":"+s.RevokedAtUtc).ToListAsync());}
        var before=await Snapshot();var tokens=observed.Tokens;var sent=sender.Sent.Count;
        var forgot=await Post(anon,"/accounts/forgot-password",new{email=ready.Email});
        foreach(var email in new[]{pending.Email,"unknown-reset@example.test"}){var r=await Post(anon,"/accounts/forgot-password",new{email});Check(r.Status==202 && r.Body==forgot.Body,"forgot common acceptance");}
        Check(forgot.Status==202 && observed.Tokens==tokens+1 && sender.Sent.Count==sent+1,"separate forgot quota/only eligible actual dispatch");
        reads=queries.Reads;var denied=await Post(anon,"/accounts/forgot-password",new{email=ready.Email},forwarded:"203.0.113.99");
        Check(denied.Status==429 && queries.Reads==reads && observed.Tokens==tokens+1 && sender.Sent.Count==sent+1,"forgot429 before lookup/token/disk");
        Check(before==await Snapshot(),"forgot and rate limit preserve password/stamp/session");
        // Wait only the short isolated window, in this same server/limiter instance.
        await Task.Delay(TimeSpan.FromSeconds(Math.Max(rate.Retry??5,denied.Retry??5))+TimeSpan.FromMilliseconds(250));
        Check(observed.RealVerifies==real && observed.DummyVerifies==d+1 && observed.Tokens==tokens+1,"queue zero: rejected work did not run later");
        Check((await Post(anon,"/accounts/login",new{email="after-window@example.test",password},csrf)).Status==401,"same-instance login window replenished");
        Check((await Post(anon,"/accounts/forgot-password",new{email=ready.Email})).Status==202 && observed.Tokens==tokens+2,"same-instance forgot window replenished");
        Peer(12);sender.Fail=true;before=await Snapshot();var failedDispatch=await Post(anon,"/accounts/forgot-password",new{email=ready.Email});sender.Fail=false;
        Check(failedDispatch.Status==202 && failedDispatch.Body==forgot.Body && before==await Snapshot(),"dispatch failure common response/state preserved");
        Check((await Post(anon,"/accounts/forgot-password",new{email=" "})).Status==400,"forgot structure400");
        Peer(13);observed.FailRead=true;Check((await Post(anon,"/accounts/login",new{email=ready.Email,password},csrf)).Status==500 && !observed.FailRead,"real invalid-table DB failure not401");
        // Warm each path then observe alternating samples; this is not a constant-time assertion.
        var timings=new Dictionary<string,List<double>>{{"existing",[]},{"missing",[]}};
        for(var i=-1;i<5;i++)foreach(var kind in timings.Keys)
        {
            Peer(30+i+1);using var c=clients();var token=await Csrf(c);var sw=Stopwatch.StartNew();
            var r=await Post(c,"/accounts/login",new{email=kind=="existing"?ready.Email:"timing-missing@example.test",password},token);sw.Stop();
            Check(r.Status==(kind=="existing"?200:401),"timing fixture outcome");if(i>=0)timings[kind].Add(sw.Elapsed.TotalMilliseconds);
        }
        foreach(var kind in timings.Keys){var values=timings[kind].Order().ToArray();Console.WriteLine($"OBSERVATION: {kind} login n={values.Length}; min/median/max ms={values[0]:F1}/{values[values.Length/2]:F1}/{values[^1]:F1}; warmed HTTPS, not a timing guarantee");}
        Check(observed.DummyHashes==1,"dummy hash never regenerated per request");
        await using(var db=factory())Check(!await db.Users.AnyAsync(u=>u.Id==observed.DummyId),"dummy never persisted");
        foreach(var s in observed.Secrets)secrets.Add(s);
        foreach(var message in sender.Sent){secrets.Add(message.Link.AbsoluteUri);var query=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(message.Link.Query);if(query.TryGetValue("token",out var t))secrets.Add(t.ToString());}
        Check(logs.All(line=>secrets.All(s=>!line.Contains(s,StringComparison.Ordinal))),"logs contain no dummy/real password/hash/cookie/token");
        Console.WriteLine("PASS: real HTTPS common401/CSRF/SQL500; one configured dummy hash and one verification per eligible path; lockout5/5min persisted and past-deadline recovery; separate IP fixed windows/429 no DB/hash/token/dispatch or queued work; XFF ignored; controlled peer isolation; no state/secret leaks");
    }
}
