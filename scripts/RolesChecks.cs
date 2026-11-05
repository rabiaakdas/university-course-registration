using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class RolesChecks
{
    static void Check(bool ok,string name) { if(!ok) throw new BehaviorFailure("Roles: "+name); }
    public static void Configure(WebApplicationBuilder b)
    {
        b.Services.AddAuthorization(o=> {
            foreach(var role in UniversityRoles.All) foreach(var bearer in new[]{false,true})
                o.AddPolicy((bearer?"b":"c")+role,p=>p.AddAuthenticationSchemes(bearer?UniversityJwtSetup.Scheme:IdentityConstants.ApplicationScheme).RequireAuthenticatedUser().RequireRole(role));
            o.AddPolicy("role-or",p=>p.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser().RequireRole(UniversityRoles.Advisor,UniversityRoles.StudentAffairs));
            o.AddPolicy("role-and",p=>p.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme).RequireAuthenticatedUser().RequireRole(UniversityRoles.Advisor).RequireRole(UniversityRoles.Instructor));
        });
    }
    public static void Map(WebApplication app)
    {
        foreach(var role in UniversityRoles.All) foreach(var prefix in new[]{"b","c"})
            app.MapGet("/role-test/"+prefix+role,()=>Results.Ok(new {allowed=true})).RequireAuthorization(prefix+role);
        app.MapGet("/role-test/or",()=>Results.Ok(new {allowed=true})).RequireAuthorization("role-or");
        app.MapGet("/role-test/and",()=>Results.Ok(new {allowed=true})).RequireAuthorization("role-and");
    }
    public static async Task Run(WebApplication app,Func<HttpClient> clients,Func<UniversityDbContext> factory,
        Func<string,bool,Task<ApplicationUser>> create,string password,List<string> secrets,ApplicationUser linked,Action<TimeSpan> advance)
    {
        var ownedRoles=new List<string>(); var ownedUsers=new List<string>();
        var tag="R31_"+Guid.NewGuid().ToString("N")[..12];
        async Task<T> Users<T>(Func<UserManager<ApplicationUser>,Task<T>> f) { await using var scope=app.Services.CreateAsyncScope();return await f(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()); }
        async Task Membership(string id,string role,bool add=true) { Check(await Users(async m=>{var u=(await m.FindByIdAsync(id))!;return (add?await m.AddToRoleAsync(u,role):await m.RemoveFromRoleAsync(u,role)).Succeeded;}),"manager membership"); }
        async Task<string> Csrf(HttpClient c) {using var r=await c.GetAsync("/accounts/antiforgery");var t=JsonNode.Parse(await r.Content.ReadAsStringAsync())!["requestToken"]!.GetValue<string>();secrets.Add(t);return t;}
        async Task<(int Status,string Body)> Send(HttpClient c,string method,string path,object? body=null,string? csrf=null,string? bearer=null)
        {
            using var q=new HttpRequestMessage(new HttpMethod(method),path);if(body is not null)q.Content=JsonContent.Create(body);
            if(csrf is not null)q.Headers.Add("X-CSRF-TOKEN",csrf);if(bearer is not null)q.Headers.Authorization=new("Bearer",bearer);
            using var r=await c.SendAsync(q);Check((int)r.StatusCode!=302 && r.Content.Headers.ContentType?.MediaType!="text/html","no redirect/HTML");return ((int)r.StatusCode,await r.Content.ReadAsStringAsync());
        }
        async Task<string> Login(HttpClient c,ApplicationUser u) {Check((await Send(c,"POST","/accounts/login",new {email=u.Email,password},await Csrf(c))).Status==200,"login");return await Csrf(c);}
        async Task<JsonNode> Mint(HttpClient c,string csrf) {var r=await Send(c,"POST","/accounts/dev-token",new{},csrf);Check(r.Status==200,"mint");var j=JsonNode.Parse(r.Body)!;secrets.Add(j["accessToken"]!.GetValue<string>());secrets.Add(j["refreshToken"]!.GetValue<string>());return j;}
        async Task<string> Snapshot() {await using var db=factory();return string.Join('|',await db.Courses.OrderBy(x=>x.Id).Select(x=>x.Id+":"+x.Code+":"+x.Name+":"+x.Credits).ToListAsync())+"/"+await db.AcademicTerms.CountAsync()+"/"+await db.CourseOfferings.CountAsync();}
        Course course=new(){Code=tag,Name="Role fixture",Credits=3}; AcademicTerm term=new(){Name=tag,StartsOn=new DateOnly(2031,1,1),EndsOn=new DateOnly(2031,6,1)};
        try
        {
            await using(var db=factory()) { var existing=await db.Roles.Select(x=>x.Id).ToListAsync();Check(await RoleMaintenance.RunAsync(app.Services,app.Environment,["prepare"])==0,"prepare");Check(await RoleMaintenance.RunAsync(app.Services,app.Environment,["prepare"])==0,"prepare repeat");ownedRoles.AddRange(await db.Roles.Where(x=>!existing.Contains(x.Id)).Select(x=>x.Id).ToListAsync());Check(await db.Roles.CountAsync(x=>UniversityRoles.All.Contains(x.Name!))==4,"four roles");db.AddRange(course,term);await db.SaveChangesAsync(); }
            Check(await RoleMaintenance.RunAsync(app.Services,app.Environment,["add","missing",UniversityRoles.StudentAffairs])==1,"missing account exit1");
            Check(await RoleMaintenance.RunAsync(app.Services,app.Environment,["add","missing","Admin"])==1,"unknown role exit1");
            var prod=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Production"});
            Check(await RoleMaintenance.RunAsync(app.Services,prod.Environment,["prepare"])==1,"Production maintenance denied");
            using var anon=clients();var initial=await Snapshot();
            var writes=new (string Method,string Path,object Body)[]{("POST","/courses",new{code=tag+"NEW",name="Role course",credits=3}),("PUT","/courses/"+course.Id,new{code=tag,name="Changed role fixture",credits=3}),("POST","/academic-terms",new{name=tag+"NEW",startsOn="2032-01-01",endsOn="2032-06-01"}),("POST","/course-offerings",new{courseId=course.Id,academicTermId=term.Id,sectionCode="R31",capacity=20})};
            foreach(var w in writes)Check((await Send(anon,w.Method,w.Path,w.Body)).Status==401,"anonymous401 "+w.Path);
            foreach(var role in new[]{"",UniversityRoles.Student,UniversityRoles.Instructor,UniversityRoles.Advisor})
            {
                var u=await create("roles"+role,true);ownedUsers.Add(u.Id);if(role!="")await Membership(u.Id,role);
                using var c=clients();var csrf=await Login(c,u);Check((await Send(c,"GET","/accounts/me")).Status==200,"roleless me");
                foreach(var w in writes)Check((await Send(c,w.Method,w.Path,w.Body,csrf)).Status==403,role+"403 "+w.Path);
                if(role!="")
                {
                    var token=await Mint(c,csrf);foreach(var expected in UniversityRoles.All){Check((await Send(c,"GET","/role-test/c"+expected)).Status==(role==expected?200:403),"cookie roles");Check((await Send(anon,"GET","/role-test/b"+expected,bearer:token["accessToken"]!.GetValue<string>())).Status==(role==expected?200:403),"bearer roles");}
                    if(role==UniversityRoles.Advisor){Check((await Send(c,"GET","/role-test/or")).Status==200&&(await Send(c,"GET","/role-test/and")).Status==403,"OR vs AND");await Membership(u.Id,UniversityRoles.Instructor);using var both=clients();await Login(both,u);Check((await Send(both,"GET","/role-test/and")).Status==200,"AND two roles");}
                }
            }
            using(var c=clients()){var csrf=await Login(c,linked);Check((await Send(c,"POST","/courses",writes[0].Body,csrf)).Status==403,"linked confirmed profile not privilege");}
            Check(await Snapshot()==initial,"denials no DB mutation");
            var staff=await create("rolesStaff",true);ownedUsers.Add(staff.Id);
            Check(await RoleMaintenance.RunAsync(app.Services,app.Environment,["add",staff.Id,UniversityRoles.StudentAffairs])==0,"assign command");
            Check(await RoleMaintenance.RunAsync(app.Services,app.Environment,["add",staff.Id,UniversityRoles.StudentAffairs])==0,"idempotent assignment");

            using var staffClient=clients();var staffCsrf=await Login(staffClient,staff);
            foreach(var w in writes){Check((await Send(staffClient,w.Method,w.Path,w.Body)).Status==400,"missing CSRF");Check((await Send(staffClient,w.Method,w.Path,w.Body,"bad")).Status==400,"bad CSRF");}
            Check(await Snapshot()==initial,"CSRF no mutation");
            foreach(var w in writes)Check((await Send(staffClient,w.Method,w.Path,w.Body,staffCsrf)).Status==(w.Method=="PUT"?200:201),"staff success "+w.Path);
            Check((await Send(staffClient,"POST","/courses",new{code=tag+"BAD",name="Bad",credits=-1},staffCsrf)).Status==400,"domain negative credit");
            Check((await Send(staffClient,"POST","/courses",writes[0].Body,staffCsrf)).Status==409,"duplicate409");
            Check((await Send(staffClient,"PUT","/courses/2147483647",writes[1].Body,staffCsrf)).Status==404,"missing404");
            await Membership(staff.Id,UniversityRoles.Advisor);
            staffCsrf=await Login(staffClient,staff);
            Check((await Send(staffClient,"PUT",writes[1].Path,writes[1].Body,staffCsrf)).Status==200,"required plus extra role succeeds");
            var pair=await Mint(staffClient,staffCsrf);var jwt=pair["accessToken"]!.GetValue<string>();
            foreach(var w in writes)Check((await Send(anon,w.Method,w.Path,w.Body,bearer:jwt)).Status==401,"bearer cannot manage");
            Check((await Send(anon,"GET","/role-test/bStudentAffairs",bearer:jwt)).Status==200,"staff JWT role");
            var parts=jwt.Split('.');var payload=JsonNode.Parse(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parts[1])))!;payload["role"]=UniversityRoles.Instructor;parts[1]=WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
            Check((await Send(anon,"GET","/role-test/bInstructor",bearer:string.Join('.',parts))).Status==401,"tampered role signature");
            var stamp=await Users(async m=>await m.GetSecurityStampAsync((await m.FindByIdAsync(staff.Id))!));secrets.Add(stamp);
            Check(await RoleMaintenance.RunAsync(app.Services,app.Environment,["remove",staff.Id,UniversityRoles.StudentAffairs])==0,"remove command");
            Check(stamp==await Users(async m=>await m.GetSecurityStampAsync((await m.FindByIdAsync(staff.Id))!)),"role removal does not change stamp");
            Check((await Send(staffClient,"GET","/role-test/cStudentAffairs")).Status==200,"old cookie retains role before validation");
            Check((await Send(anon,"GET","/role-test/bStudentAffairs",bearer:jwt)).Status==200,"old JWT retains role");
            var fresh=await Mint(staffClient,staffCsrf);Check((await Send(anon,"GET","/role-test/bStudentAffairs",bearer:fresh["accessToken"]!.GetValue<string>())).Status==403,"mint rereads roles not stale cookie");
            var refreshed=await Send(anon,"POST","/accounts/refresh",new{refreshToken=pair["refreshToken"]!.GetValue<string>()});Check(refreshed.Status==200,"role refresh");var next=JsonNode.Parse(refreshed.Body)!;secrets.Add(next["accessToken"]!.GetValue<string>());secrets.Add(next["refreshToken"]!.GetValue<string>());
            Check((await Send(anon,"GET","/role-test/bStudentAffairs",bearer:next["accessToken"]!.GetValue<string>())).Status==403,"refresh rereads roles");
            using(var again=clients()){await Login(again,staff);Check((await Send(again,"GET","/role-test/cStudentAffairs")).Status==403,"fresh login current roles");}
            advance(TimeSpan.FromMinutes(19));
            Check((await Send(staffClient,"GET","/role-test/cStudentAffairs")).Status==200,"old cookie retains removed role at19min");
            advance(TimeSpan.FromMinutes(2));
            Check((await Send(staffClient,"GET","/role-test/cStudentAffairs")).Status==401,"fixed20min ticket expires before30min stamp interval");
            var publicEmail=staff.Email!.Replace("@","public@");
            var registered=await Send(anon,"POST","/accounts/register",new{email=publicEmail,password,role=UniversityRoles.StudentAffairs,roles=new[]{UniversityRoles.StudentAffairs},permission="all"});Check(registered.Status==201,"public registration");
            Check(await Users(async m=>{var u=(await m.FindByEmailAsync(publicEmail))!;return (await m.GetRolesAsync(u)).Count==0;}),"public fields cannot grant roles");
            Console.WriteLine("PASS: roles real HTTPS 4-write matrix anonymous401/other roles403/staff201+200; CSRF/domain400+404+409/no mutation; cookie/JWT role mapping/tampered401/OR-AND; old cookie+JWT retain roles/new login+mint+refresh updated; public escalation denied");
        }
        finally
        {
            await using var db=factory();
            await db.CourseOfferings.Where(x=>x.CourseId==course.Id).ExecuteDeleteAsync();
            await db.AcademicTerms.Where(x=>x.Name.StartsWith(tag)).ExecuteDeleteAsync();await db.Courses.Where(x=>x.Code.StartsWith(tag)).ExecuteDeleteAsync();
            await db.UserRoles.Where(x=>ownedUsers.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.Roles.Where(x=>ownedRoles.Contains(x.Id)).ExecuteDeleteAsync();
        }
    }
}
