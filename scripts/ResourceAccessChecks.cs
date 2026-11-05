using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using UniversityRegistration.Accounts;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class ResourceAccessChecks
{
    static void Check(bool value,string name) { if(!value) throw new BehaviorFailure("Resource access: "+name); }
    public static async Task Unit(bool probe=false)
    {
        var options=Microsoft.Extensions.Options.Options.Create(new IdentityOptions());
        var handler=new StudentAccessHandler(options);
        var resource=new StudentAccessResource(1,"owner","advisor");
        async Task<bool> Allowed(string? id,string[] roles,StudentAccessKind kind,bool duplicate=false)
        {
            var claims=roles.Select(r=>new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role,r)).ToList();
            if(id is not null)claims.Add(new(System.Security.Claims.ClaimTypes.NameIdentifier,id));
            if(duplicate)claims.Add(new(System.Security.Claims.ClaimTypes.NameIdentifier,"other"));
            var user=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims,"test"));
            var context=new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext([new StudentAccessRequirement(kind)],user,resource);
            await handler.HandleAsync(context);return context.HasSucceeded;
        }
        Check(await Allowed("owner",[UniversityRoles.Student],StudentAccessKind.Manage),"owner");
        Check(await Allowed("advisor",[UniversityRoles.Advisor],StudentAccessKind.Decide),"assigned");
        foreach(var id in new string?[]{null,""," ","other"})Check(!await Allowed(id,[UniversityRoles.Student,UniversityRoles.Advisor,UniversityRoles.StudentAffairs],StudentAccessKind.Read),"missing/unrelated id");
        Check(!await Allowed("owner",[UniversityRoles.Student],StudentAccessKind.Manage,true),"ambiguous identity");
        Check(!await Allowed("owner",[UniversityRoles.StudentAffairs],StudentAccessKind.Manage),"no staff bypass");
        Check(!await Allowed("advisor",[UniversityRoles.Advisor],StudentAccessKind.Manage),"advisor cannot manage");
        Check(!await Allowed("owner",[UniversityRoles.Student],StudentAccessKind.Decide),"owner cannot decide");
        var student=new Student();student.AssignAdvisor("account");student.AssignAdvisor("account");Check(student.AdvisorUserId=="account","assignment repeat");
        try{student.AssignAdvisor(" ");throw new BehaviorFailure("Invalid advisor allowed");}catch(ArgumentException){}
        Check(student.AdvisorUserId=="account","invalid assignment preserves state");student.AssignAdvisor(null);Check(student.AdvisorUserId is null,"clear");
        Check(!probe,"controlled failure probe");
        Console.WriteLine("PASS: pure resource requirement roles/ownership/assignment and missing/ambiguous identity; controlled advisor state");
    }
    public sealed class Approvals : IEnrollmentApprovedObserver
    {
        public int Count;
        public Task HandleAsync(EnrollmentApproved notification,CancellationToken cancellationToken) { Count++;return Task.CompletedTask; }
    }
    public static void Configure(WebApplicationBuilder b)
    {
        b.Services.AddSingleton(new EnrollmentPolicy(30));
        b.Services.AddScoped<EnrollmentOperations>();
        b.Services.AddScoped<IAcademicEligibilityEvaluator,AcademicEligibility>();
        b.Services.AddScoped<TermRegistrationSummaryFacade>();
        b.Services.AddScoped<EnrollmentApprovedPublisher>();
        b.Services.AddSingleton<Approvals>();
        b.Services.AddSingleton<IEnrollmentApprovedObserver>(sp=>sp.GetRequiredService<Approvals>());
    }
    public static async Task Run(WebApplication app,Func<HttpClient> clients,Func<UniversityDbContext> factory,
        Func<string,bool,Task<ApplicationUser>> create,string password,List<string> secrets,JwtChecks.SqlCounter counter)
    {
        var ownedRoles=new List<string>();var users=new List<string>();var students=new List<Student>();var offerings=new List<CourseOffering>();
        var tag="R32_"+Guid.NewGuid().ToString("N")[..12];
        var course=new Course{Code=tag,Name="Resource test",Credits=1};
        var term=new AcademicTerm{Name=tag,StartsOn=new(2032,1,1),EndsOn=new(2032,6,1)};
        var opened=new List<HttpClient>();
        async Task<ApplicationUser> Account(string suffix,params string[] roles)
        {
            var u=await create(tag+suffix,true);users.Add(u.Id);
            await using var scope=app.Services.CreateAsyncScope();var m=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach(var role in roles) Check((await m.AddToRoleAsync((await m.FindByIdAsync(u.Id))!,role)).Succeeded,"membership");return u;
        }
        async Task<string> Csrf(HttpClient c) {using var r=await c.GetAsync("/accounts/antiforgery");var t=JsonNode.Parse(await r.Content.ReadAsStringAsync())!["requestToken"]!.GetValue<string>();secrets.Add(t);return t;}
        async Task<(int Status,JsonNode? Json)> Send(HttpClient c,string method,string path,object? body=null,string? csrf=null,string? bearer=null)
        {
            using var q=new HttpRequestMessage(new HttpMethod(method),path);if(body is not null)q.Content=JsonContent.Create(body);
            if(csrf is not null)q.Headers.Add("X-CSRF-TOKEN",csrf);if(bearer is not null)q.Headers.Authorization=new("Bearer",bearer);
            using var r=await c.SendAsync(q);Check(r.Headers.Location is null || (int)r.StatusCode==201,"no redirect");
            var text=await r.Content.ReadAsStringAsync();Check(!text.Contains("<html",StringComparison.OrdinalIgnoreCase),"no HTML");
            return ((int)r.StatusCode,string.IsNullOrEmpty(text)?null:JsonNode.Parse(text));
        }
        async Task<(HttpClient C,string Csrf)> Login(ApplicationUser u)
        {var c=clients();opened.Add(c);Check((await Send(c,"POST","/accounts/login",new{email=u.Email,password},await Csrf(c))).Status==200,"login");return(c,await Csrf(c));}
        async Task<Student> Profile(ApplicationUser u,ApplicationUser? advisor)
        {var s=new Student{StudentNumber=tag+students.Count,FullName="Resource fixture"};Check(s.TryLinkAccount(u.Id),"link");s.AssignAdvisor(advisor?.Id);students.Add(s);await using var db=factory();db.Add(s);await db.SaveChangesAsync();return s;}
        async Task<string> Snapshot()
        {await using var db=factory();return string.Join('|',await db.Enrollments.Where(e=>students.Select(s=>s.Id).Contains(e.StudentId)).OrderBy(e=>e.Id).Select(e=>e.Id+":"+e.Status+":"+e.RejectionReason).ToListAsync());}
        try
        {
            await using(var db=factory()) {var ids=await db.Roles.Select(r=>r.Id).ToArrayAsync();Check(await RoleMaintenance.RunAsync(app.Services,app.Environment,["prepare"])==0,"roles");ownedRoles.AddRange(await db.Roles.Where(r=>!ids.Contains(r.Id)).Select(r=>r.Id).ToListAsync());db.AddRange(course,term);await db.SaveChangesAsync();for(var i=0;i<9;i++)offerings.Add(new(){CourseId=course.Id,AcademicTermId=term.Id,SectionCode="S"+i,Capacity=20});db.AddRange(offerings);await db.SaveChangesAsync();}
            var a=await Account("a",UniversityRoles.Student);var b=await Account("b",UniversityRoles.Student);
            var da=await Account("da",UniversityRoles.Advisor);var dbUser=await Account("db",UniversityRoles.Advisor);
            var none=await Account("none");var orphan=await Account("orphan",UniversityRoles.Student);
            var staff=await Account("staff",UniversityRoles.StudentAffairs);var instructor=await Account("instructor",UniversityRoles.Instructor);
            var multi=await Account("multi",UniversityRoles.Student,UniversityRoles.Advisor,UniversityRoles.StudentAffairs,UniversityRoles.Instructor);
            var sa=await Profile(a,da);var sb=await Profile(b,dbUser);var sn=await Profile(none,none);var sm=await Profile(multi,da);
            var ca=await Login(a);var cb=await Login(b);var cda=await Login(da);var cdb=await Login(dbUser);var cn=await Login(none);var co=await Login(orphan);var cm=await Login(multi);var cs=await Login(staff);var ci=await Login(instructor);
            using var anon=clients();
            var eA=new Enrollment{StudentId=sa.Id,CourseOfferingId=offerings[0].Id};var eB=new Enrollment{StudentId=sb.Id,CourseOfferingId=offerings[0].Id};
            await using(var db=factory()){db.AddRange(eA,eB);await db.SaveChangesAsync();}
            string[] Reads(Student s,int eid)=>[$"/students/{s.Id}",$"/students/{s.Id}/enrollments",$"/enrollments/{eid}",$"/students/{s.Id}/terms/{term.Id}/registration-summary"];
            foreach(var path in Reads(sa,eA.Id))
            {
                Check((await Send(ca.C,"GET",path)).Status==200,"owner read "+path);
                Check((await Send(cda.C,"GET",path)).Status==200,"assigned advisor read");
                Check((await Send(cb.C,"GET",path)).Status==404,"other student read");
                Check((await Send(cdb.C,"GET",path)).Status==404,"other advisor read");
                Check((await Send(co.C,"GET",path)).Status==404,"profileless");
                Check((await Send(cn.C,"GET",path)).Status==403,"linked without role");
                Check((await Send(anon,"GET",path)).Status==401,"anonymous");
                Check((await Send(cm.C,"GET",path)).Status==404,"multirole has no bypass");
            }
            var before=await Snapshot();var events=app.Services.GetRequiredService<Approvals>().Count;
            var denied=new (string Path,object Body)[]{("/enrollments",new{studentId=sb.Id,courseOfferingId=offerings[1].Id}),($"/students/{sb.Id}/enrollments/batch",new{studentId=sa.Id,courseOfferingIds=new[]{offerings[1].Id}}),($"/students/{sb.Id}/enrollments/eligibility",new{studentId=sa.Id,courseOfferingIds=new[]{offerings[1].Id}}),($"/enrollments/{eB.Id}/submit",new{studentId=sa.Id})};
            foreach(var d in denied) Check((await Send(ca.C,"POST",d.Path,d.Body,ca.Csrf)).Status==404,"IDOR/body mismatch "+d.Path);
            foreach(var path in new[]{"approve","reject"})
            {
                Check((await Send(cda.C,"POST",$"/enrollments/{eB.Id}/{path}",new{reason="test"},cda.Csrf)).Status==404,"unassigned decision");
                Check((await Send(ca.C,"POST",$"/enrollments/{eA.Id}/{path}",new{reason="test"},ca.Csrf)).Status==403,"student decision denied");
                Check((await Send(cm.C,"POST",$"/enrollments/{eA.Id}/{path}",new{reason="test"},cm.Csrf)).Status==404,"multi decision no bypass");
            }
            foreach(var c in new[]{cs,ci})foreach(var action in new[]{"approve","reject"})
                Check((await Send(c.C,"POST",$"/enrollments/{eA.Id}/{action}",new{reason="test"},c.Csrf)).Status==403,"staff/instructor no decision bypass");
            foreach(var d in denied)Check((await Send(cda.C,"POST",d.Path,d.Body,cda.Csrf)).Status==403,"advisor cannot manage");
            Check(before==await Snapshot() && events==app.Services.GetRequiredService<Approvals>().Count,"denials no writes/event (new context)");
            var own=new (string Path,object Body)[]{("/enrollments",new{studentId=sa.Id,courseOfferingId=offerings[1].Id}),($"/students/{sa.Id}/enrollments/batch",new{courseOfferingIds=new[]{offerings[2].Id,offerings[3].Id}}),($"/students/{sa.Id}/enrollments/eligibility",new{courseOfferingIds=new[]{offerings[4].Id}}),($"/enrollments/{eA.Id}/submit",new{})};
            foreach(var d in own)
            {
                Check((await Send(anon,"POST",d.Path,d.Body)).Status==401,"anonymous writes");
                Check((await Send(cn.C,"POST",d.Path,d.Body,cn.Csrf)).Status==403,"roleless writes");
            }
            foreach(var d in own)foreach(var token in new string?[]{null,"invalid"})Check((await Send(ca.C,"POST",d.Path,d.Body,token)).Status==400,"CSRF "+d.Path);
            Check(before==await Snapshot(),"CSRF no writes");
            Check((await Send(ca.C,"POST",own[0].Path,own[0].Body,ca.Csrf)).Status==201,"owner create");
            Check((await Send(ca.C,"POST",own[1].Path,own[1].Body,ca.Csrf)).Status==201,"owner batch");
            var previewBefore=await Snapshot();Check((await Send(ca.C,"POST",own[2].Path,own[2].Body,ca.Csrf)).Status==200 && previewBefore==await Snapshot(),"preview no write");
            Check((await Send(ca.C,"POST",own[3].Path,own[3].Body,ca.Csrf)).Status==200,"submit");
            Check((await Send(cda.C,"POST",$"/enrollments/{eA.Id}/approve",new{})).Status==400,"decision CSRF");
            Check((await Send(cda.C,"POST",$"/enrollments/{eA.Id}/approve",new{},cda.Csrf)).Status==200 && app.Services.GetRequiredService<Approvals>().Count==events+1,"authorized approve event");
            Check((await Send(cda.C,"POST",$"/enrollments/{eA.Id}/approve",new{},cda.Csrf)).Status==409,"state preserved");
            Check((await Send(cb.C,"POST",$"/enrollments/{eB.Id}/submit",new{},cb.Csrf)).Status==200,"B submit");
            Check(await StudentMaintenance.RunAsync(app.Services,app.Environment,["advisor",sb.Id.ToString(),da.Id])==0,"reassign");
            Check((await Send(cdb.C,"POST",$"/enrollments/{eB.Id}/reject",new{reason="ok"},cdb.Csrf)).Status==404,"old advisor denied");
            Check((await Send(cda.C,"POST",$"/enrollments/{eB.Id}/reject",new{reason=" "},cda.Csrf)).Status==400,"reason rule");
            Check((await Send(cda.C,"POST",$"/enrollments/{eB.Id}/reject",new{reason=" valid "},cda.Csrf)).Status==200,"new advisor reject");
            Check((await Send(cm.C,"POST","/enrollments",new{studentId=sm.Id,courseOfferingId=offerings[0].Id},cm.Csrf)).Status==201,"multi owns own profile");
            Check((await Send(cn.C,"POST",$"/enrollments/{eA.Id}/approve",new{},cn.Csrf)).Status==403,"assignment without role does not grant permission");
            var start=counter.Reads;Check((await Send(ca.C,"GET",$"/students/{sa.Id}/enrollments")).Status==200,"list");var small=counter.Reads-start;
            await using(var db=factory()){db.Add(new Enrollment{StudentId=sa.Id,CourseOfferingId=offerings[5].Id});await db.SaveChangesAsync();}
            start=counter.Reads;Check((await Send(ca.C,"GET",$"/students/{sa.Id}/enrollments")).Status==200,"larger list");Check(counter.Reads-start==small && small>0,"bounded list query count");
            var mint=await Send(ca.C,"POST","/accounts/dev-token",new{},ca.Csrf);Check(mint.Status==200,"mint");var access=mint.Json!["accessToken"]!.GetValue<string>();secrets.Add(access);secrets.Add(mint.Json["refreshToken"]!.GetValue<string>());
            foreach(var path in Reads(sa,eA.Id))Check((await Send(anon,"GET",path,bearer:access)).Status==401,"bearer cannot open cookie resources");
            Check((await Send(anon,"GET","/courses")).Status==200,"public catalog");
            Check((await Send(ca.C,"POST","/students",new{studentNumber=tag+"x",fullName="x",userId=a.Id,advisorUserId=da.Id},ca.Csrf)).Status==403,"provisioning staff only");
            var provision=await Send(cs.C,"POST","/students",new{studentNumber=tag+"p",fullName="Provision fixture",userId=staff.Id,advisorUserId=da.Id},cs.Csrf);
            Check(provision.Status==201,"staff provisioning");var provisionId=provision.Json!["id"]!.GetValue<int>();
            await using(var db=factory()){var p=await db.Students.SingleAsync(s=>s.Id==provisionId);students.Add(p);Check(p.UserId is null && p.AdvisorUserId is null,"provision DTO ignores security relationships");}
            Check(await StudentMaintenance.RunAsync(app.Services,app.Environment,["advisor",sa.Id.ToString(),none.Id])==1,"roleless assignment rejected");
            Check(await StudentMaintenance.RunAsync(app.Services,app.Environment,["advisor",sa.Id.ToString(),"missing"])==1,"missing account rejected");
            Check(await StudentMaintenance.RunAsync(app.Services,app.Environment,["link",sa.Id.ToString(),a.Id])==0,"link repeat");
            Check(await StudentMaintenance.RunAsync(app.Services,app.Environment,["link",sa.Id.ToString(),b.Id])==1,"relink denied");
            var production=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Production"});Check(await StudentMaintenance.RunAsync(app.Services,production.Environment,["advisor",sa.Id.ToString(),da.Id])==1,"Production denied");
            async Task Constraint(Func<UniversityDbContext,Task> action,string name)
            {await using var db=factory();await using var tx=await db.Database.BeginTransactionAsync();try{await action(db);throw new BehaviorFailure("Constraint not enforced: "+name);}catch(SqlException ex)when(ex.Number==547 && ex.Message.Contains(name)){}finally{await tx.RollbackAsync();}}
            await Constraint(db=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Students SET AdvisorUserId={"missing"} WHERE Id={sa.Id}"),"FK_Students_AdvisorUser");
            await Constraint(db=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM AspNetUsers WHERE Id={da.Id}"),"FK_Students_AdvisorUser");
            Check(await StudentMaintenance.RunAsync(app.Services,app.Environment,["advisor",sm.Id.ToString(),"none"])==0,"null assignment");
            await using(var db=factory())Check(await db.Students.CountAsync(s=>s.AdvisorUserId==da.Id)==2 && (await db.Students.SingleAsync(s=>s.Id==sm.Id)).AdvisorUserId is null,"nonunique advisor/null loaded");
            Console.WriteLine("PASS: resource HTTPS ownership/read/manage/decision matrix; 401/403/404; IDOR/body mismatch; CSRF; no denied writes/events; reassignment; real advisor FK/NO ACTION/null/nonunique; list query count independent of rows");
        }
        finally
        {
            foreach(var c in opened)c.Dispose();
            await using var db=factory();var sids=students.Select(s=>s.Id).ToArray();var oids=offerings.Select(o=>o.Id).ToArray();
            await db.Enrollments.Where(e=>sids.Contains(e.StudentId)).ExecuteDeleteAsync();
            await db.Students.Where(s=>sids.Contains(s.Id)).ExecuteDeleteAsync();
            await db.CourseOfferings.Where(o=>oids.Contains(o.Id)).ExecuteDeleteAsync();
            await db.Courses.Where(c=>c.Id==course.Id).ExecuteDeleteAsync();await db.AcademicTerms.Where(t=>t.Id==term.Id).ExecuteDeleteAsync();
            await db.UserRoles.Where(r=>users.Contains(r.UserId)).ExecuteDeleteAsync();
            await db.Roles.Where(r=>ownedRoles.Contains(r.Id)).ExecuteDeleteAsync();
            // The surrounding Identity fixture owns and cleans these accounts and refresh sessions.
        }
    }
}
