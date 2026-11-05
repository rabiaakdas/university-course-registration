using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class PasswordResetChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new BehaviorFailure("Password reset: " + name); }
    public static void Unit(string root)
    {
        foreach (var entry in new[] { ("Accounts:PasswordResetMinutes", "0"), ("Accounts:PasswordResetMinutes", "61"),
            ("Accounts:PasswordResetPageUrl", "http://remote.example.test/reset"), ("Accounts:PasswordResetPageUrl", "https://example.test/reset?next=evil"),
            ("Accounts:PasswordResetPageUrl", "https://user:placeholder@example.test/reset") })
        {
            var b = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Production" });
            b.Configuration["Accounts:LocalMailboxEnabled"] = "false";
            b.Configuration[entry.Item1] = entry.Item2;
            var rejected = false;
            try { b.Services.AddAccountIdentity(b.Configuration, b.Environment); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "reset lifetime/trusted URL startup guard");
        }
        Console.WriteLine("PASS: password reset duration and trusted callback guards (no DB)");
    }
    public sealed class Sender : IAccountConfirmationSender
    {
        public LocalAccountConfirmationSender Inner = null!;
        public bool Fail;
        public ConcurrentQueue<(string Recipient, Uri Link)> Sent = new();
        public async Task SendAsync(string recipient, Uri link, CancellationToken ct, string subject = "E-posta doğrulama (yerel geliştirme)")
        {
            if (Fail) throw new IOException("Controlled mailbox failure");
            await Inner.SendAsync(recipient, link, ct, subject); Sent.Enqueue((recipient, link));
        }
    }
    public sealed class ResetFault : DbCommandInterceptor
    {
        public bool Armed; public int Calls;
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Armed && command.CommandText.Contains("UPDATE") && command.CommandText.Contains("RefreshSessions") && command.CommandText.Contains("RevokedAtUtc"))
            {
                Armed=false; Calls++;
                command.CommandText=Regex.Replace(command.CommandText,@"SET \[\w+\]\.\[RevokedAtUtc\] = [^\r\n]+", "SET [r].[RevokedAtUtc] = DATEADD(day,-1,[r].[CreatedAtUtc])");
            }
            return ValueTask.FromResult(result);
        }
    }
    public static async Task Run(WebApplication app, Func<HttpClient> clients, Func<UniversityDbContext> factory,
        Func<string,bool,Task<ApplicationUser>> create, string oldPassword, Action<TimeSpan> advance,
        List<string> secrets, RefreshChecks.FamilyGate gate, ResetFault fault, Sender sender)
    {
        var a=await create("resetA",true); var b=await create("resetB",true); var pending=await create("resetPending",false);
        using var anonymous=clients(); using var cookie=clients();
        var password=" New password " + Guid.NewGuid().ToString("N") + " "; secrets.Add(password);
        async Task<T> Manager<T>(Func<UserManager<ApplicationUser>,Task<T>> action) { await using var scope=app.Services.CreateAsyncScope();return await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()); }
        async Task<string> Generate(string id, bool email=false) => await Manager(async m => { var u=(await m.FindByIdAsync(id))!; var raw=email?await m.GenerateEmailConfirmationTokenAsync(u):await m.GeneratePasswordResetTokenAsync(u);secrets.Add(raw);var token=WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(raw));secrets.Add(token);return token; });
        async Task<string> State(string id) {await using var db=factory();var u=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==id);secrets.Add(u.PasswordHash!);secrets.Add(u.SecurityStamp!);return u.PasswordHash+"|"+u.SecurityStamp+"|"+string.Join(',',await db.RefreshSessions.Where(x=>x.UserId==id).OrderBy(x=>x.Id).Select(x=>x.RevokedAtUtc).ToArrayAsync());}
        async Task<(int Status,string Body)> Post(HttpClient c,string path,object body,string? csrf=null,string? host=null)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
            if(csrf is not null) request.Headers.Add("X-CSRF-TOKEN",csrf); if(host is not null)request.Headers.Host=host;
            using var response=await c.SendAsync(request);var text=await response.Content.ReadAsStringAsync();
            Check(!response.Headers.Contains("Set-Cookie") || path=="/accounts/login","reset must not sign in");
            return ((int)response.StatusCode,text);
        }
        async Task<string> Csrf(HttpClient c){using var r=await c.GetAsync("/accounts/antiforgery");var t=JsonNode.Parse(await r.Content.ReadAsStringAsync())!["requestToken"]!.GetValue<string>();secrets.Add(t);return t;}
        async Task<string> Login(HttpClient c,ApplicationUser u,string pw){var t=await Csrf(c);Check((await Post(c,"/accounts/login",new {email=u.Email,password=pw},t)).Status==200,"login fixture");return await Csrf(c);}
        async Task<JsonObject> Mint(HttpClient c,string csrf){var r=await Post(c,"/accounts/dev-token",new {},csrf);Check(r.Status==200,"mint family");var p=JsonNode.Parse(r.Body)!.AsObject();secrets.Add(p["accessToken"]!.GetValue<string>());secrets.Add(p["refreshToken"]!.GetValue<string>());return p;}
        async Task<int> Reset(string id,string token,string pw) => (await Post(anonymous,"/accounts/reset-password",new {userId=id,token,newPassword=pw})).Status;
        async Task<int> Refresh(string token) => (await Post(anonymous,"/accounts/refresh",new {refreshToken=token})).Status;
        var csrf=await Login(cookie,a,oldPassword); var p1=await Mint(cookie,csrf);var p2=await Mint(cookie,csrf);
        using var bCookie=clients();var bCsrf=await Login(bCookie,b,oldPassword);var other=await Mint(bCookie,bCsrf);
        var before=await State(a.Id);var count=sender.Sent.Count;
        var good=await Post(anonymous,"/accounts/forgot-password",new {email=" "+a.Email!.ToUpperInvariant()+" "},host:"attacker.example.test");
        var missing=await Post(anonymous,"/accounts/forgot-password",new {email="missing-"+Guid.NewGuid()+"@example.test"});
        var unconfirmed=await Post(anonymous,"/accounts/forgot-password",new {email=pending.Email});
        Check(good.Status==202 && missing==good && unconfirmed==good && sender.Sent.Count==count+1,"common202/no mailbox for missing/pending");
        var message=sender.Sent.Last(); Check(message.Recipient==a.Email && message.Link.Host=="callback.example.test" && message.Link.AbsolutePath=="/reset","trusted callback ignores Host");
        var token=QueryHelpers.ParseQuery(message.Link.Query)["token"].ToString();secrets.Add(token);
        var directory=app.Services.GetRequiredService<LocalConfirmationSettings>().Directory;
        Check(Directory.GetFiles(directory,"*.json").Any(f=>File.ReadAllText(f).Contains("Parola") && File.ReadAllText(f).Contains("callback.example.test")),"actual local reset message");
        Check(await State(a.Id)==before && !good.Body.Contains(token),"request doesn't change password/stamp/families or expose token");
        sender.Fail=true;var failed=await Post(anonymous,"/accounts/forgot-password",new {email=a.Email});sender.Fail=false;
        Check(failed==good && await State(a.Id)==before,"mailbox failure same202/no mutation");
        Check((await Post(anonymous,"/accounts/forgot-password",new {email=" "})).Status==400,"invalid email400");
        var another=await Generate(a.Id);
        Check(await Manager(async m=>await m.VerifyUserTokenAsync((await m.FindByIdAsync(a.Id))!,PasswordResetTokenProvider.ProviderName,UserManager<ApplicationUser>.ResetPasswordTokenPurpose,Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)))),"new request leaves old token valid");
        Check(await Reset(b.Id,token,password)==400 && await Reset("missing",token,password)==400,"wrong/missing user");
        Check(await Reset(a.Id,token[..^4]+"AAAA",password)==400 && await Reset(a.Id,"%%%",password)==400,"tamper/encoding");
        Check(await Reset(a.Id,await Generate(a.Id,true),password)==400,"email purpose cannot reset");
        using(var r=new HttpRequestMessage(HttpMethod.Get,"/accounts/bearer-me")){r.Headers.Authorization=new("Bearer",token);using var response=await anonymous.SendAsync(r);Check((int)response.StatusCode==401,"reset token not bearer");}
        Check(await Reset(a.Id,token,"short")==400 && await State(a.Id)==before,"password policy preserves state");
        var resetOptions=app.Services.GetRequiredService<IOptions<PasswordResetProviderOptions>>().Value;
        Check(resetOptions.TokenLifespan==TimeSpan.FromMinutes(15) && app.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan==TimeSpan.FromHours(2),"separate lifetimes");
        // Provider uses UtcNow, not TimeProvider. Genuine token with an isolated 10ms test lifetime.
        resetOptions.TokenLifespan=TimeSpan.FromMilliseconds(10);
        try {var expiring=await Generate(a.Id); await Task.Delay(30); Check(await Reset(a.Id,expiring,password)==400,"expired token");} finally {resetOptions.TokenLifespan=TimeSpan.FromMinutes(15);}
        Check(await State(a.Id)==before,"all token rejections preserve state");
        fault.Armed=true;Check(await Reset(a.Id,token,password)==500 && fault.Calls==1,"real family update fault after Identity save");
        Check(await State(a.Id)==before,"outer rollback restores password/stamp/families");
        Check(await Reset(a.Id,token,password)==204,"successful reset204");
        Check(await Manager(async m=>{var u=(await m.FindByIdAsync(a.Id))!;return !await m.CheckPasswordAsync(u,oldPassword)&&await m.CheckPasswordAsync(u,password)&&!await m.CheckPasswordAsync(u,password.Trim());}),"new password exact/no trim, old rejected");
        await using(var db=factory()) Check(await db.RefreshSessions.CountAsync(x=>x.UserId==a.Id && x.RevokedAtUtc!=null)==2 && !await db.RefreshSessions.AnyAsync(x=>x.UserId==b.Id&&x.RevokedAtUtc!=null),"all own families revoked/other untouched");
        Check(await Reset(a.Id,token,password)==400 && await Reset(a.Id,another,password)==400,"used and pre-reset sibling tokens invalidated by stamp");
        Check(await Refresh(p1["refreshToken"]!.GetValue<string>())==401 && await Refresh(p2["refreshToken"]!.GetValue<string>())==401,"both refresh families denied");
        using(var r=new HttpRequestMessage(HttpMethod.Get,"/accounts/bearer-me")){r.Headers.Authorization=new("Bearer",p1["accessToken"]!.GetValue<string>());using var response=await anonymous.SendAsync(r);Check((int)response.StatusCode==200,"old access remains valid");}
        // Cookie remains accepted until validator runs, but cannot mint under the account lock with old stamp.
        Check((await Post(cookie,"/accounts/dev-token",new {},csrf)).Status==401,"stale cookie cannot create family");
        var stampOptions=app.Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;
        Check(stampOptions.ValidationInterval==TimeSpan.FromMinutes(30),"production default stamp interval");
        stampOptions.ValidationInterval=TimeSpan.Zero;advance(TimeSpan.FromSeconds(1));
        try{using var response=await cookie.GetAsync("/accounts/me");Check((int)response.StatusCode==401,"cookie rejected on stamp validation");}finally{stampOptions.ValidationInterval=TimeSpan.FromMinutes(30);}
        using var fresh=clients();await Login(fresh,a,password);
        // Same reset token, two HTTP contexts: one committed password change.
        var race=await create("resetRace",true);var raceToken=await Generate(race.Id);
        var pw1="Race One "+Guid.NewGuid();var pw2="Race Two "+Guid.NewGuid();secrets.Add(pw1);secrets.Add(pw2);
        gate.Arm();var one=Reset(race.Id,raceToken,pw1);var two=Reset(race.Id,raceToken,pw2);
        try{await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(20));}finally{gate.Release.TrySetResult();}
        var results=await Task.WhenAll(one,two);Check(results.Count(x=>x==204)==1&&results.Count(x=>x==400)==1,"concurrent reset one204/one400");
        Check(await Manager(async m=>{var u=(await m.FindByIdAsync(race.Id))!;return await m.CheckPasswordAsync(u,results[0]==204?pw1:pw2)&&!await m.CheckPasswordAsync(u,results[0]==204?pw2:pw1);}),"only winner persisted");
        // Both lock orders for reset vs refresh and reset vs stale-cookie initial issue.
        foreach(var mint in new[]{false,true}) foreach(var resetFirst in new[]{true,false})
        {
            var u=await create("resetOrder"+mint+resetFirst,true);using var c=clients();var t=await Login(c,u,oldPassword);var pair=await Mint(c,t);var rt=await Generate(u.Id);var pw="Ordered "+Guid.NewGuid();secrets.Add(pw);
            gate.ArmOrdered();Task<int>? resetting=null;Task<(int Status,string Body)>? competing=null;
            Task<(int Status,string Body)> Compete()=>mint?Post(c,"/accounts/dev-token",new {},t):Post(anonymous,"/accounts/refresh",new{refreshToken=pair["refreshToken"]!.GetValue<string>()});
            if(resetFirst)resetting=Reset(u.Id,rt,pw);else competing=Compete();
            try{await gate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(20));if(resetFirst)competing=Compete();else resetting=Reset(u.Id,rt,pw);await gate.SecondReady.Task.WaitAsync(TimeSpan.FromSeconds(20));}finally{gate.Release.TrySetResult();}
            Check(await resetting! ==204,"ordered reset success");var response=await competing!;Check(response.Status==(resetFirst?401:200),"ordered reset/refresh or mint outcome");
            if(response.Status==200){var body=JsonNode.Parse(response.Body)!;secrets.Add(body["refreshToken"]!.GetValue<string>());secrets.Add(body["accessToken"]!.GetValue<string>());Check(await Refresh(body["refreshToken"]!.GetValue<string>())==401,"pre-reset issued successor revoked");}
            await using var db=factory();Check(!await db.RefreshSessions.AnyAsync(x=>x.UserId==u.Id&&x.RevokedAtUtc==null),"no post-reset active family");
        }
        Console.WriteLine("PASS: password reset HTTPS uniform request/local mailbox/trusted host/failure, purpose/lifetime/stamp/old tokens, exact password; real post-Identity-save SQL rollback; two families revoked/other preserved; cookie validation/access boundary; concurrent reset single winner and both reset-refresh/mint lock orders");
    }
}
