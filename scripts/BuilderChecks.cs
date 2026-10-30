using UniversityRegistration.Application;
using UniversityRegistration.Models;
using UniversityRegistration.Specifications;

internal static class BuilderChecks
{
    static void Check(bool ok,string label){if(!ok)throw new BehaviorFailure("Builder: "+label);}
    static void Throws<T>(Action action,string label) where T:Exception
    {try{action();}catch(T error){Check(!string.IsNullOrWhiteSpace(error.Message),"explanatory error "+label);return;}throw new BehaviorFailure("Builder: expected "+typeof(T).Name+" for "+label);}
    public static void Run(bool probe=false)
    {
        var monday=new WeeklyTimeSlot(DayOfWeek.Monday,new(9,0),new(10,0));
        var externalSlots=new List<WeeklyTimeSlot>{monday};var required=new List<int>{7,8};var completed=new List<int>{7};
        var builder=new EligibilityScenarioBuilder(30)
            .Existing(99,2,24,EnrollmentStatus.Approved).WithSlots(99,externalSlots)
            .Existing(98,3,4,EnrollmentStatus.Submitted).WithSlots(98,externalSlots)
            .Existing(97,2,100,EnrollmentStatus.Draft).WithSlots(97,externalSlots)
            .Existing(96,2,100,EnrollmentStatus.Rejected).WithSlots(96,externalSlots)
            .Request(20,2,7).WithSlots(20,externalSlots).Requires(20,required).Completed(completed)
            .Request(10,3,5);
        // Mutation before Build must not be observed either.
        externalSlots.Clear();required.Clear();completed.Add(8);
        var first=builder.Build();
        externalSlots.Add(new(DayOfWeek.Friday,new(12,0),new(13,0)));required.Add(42);completed.Clear();
        var second=builder.Build();
        Check(!ReferenceEquals(first,second)&&!ReferenceEquals(first.Requested,second.Requested)&&!ReferenceEquals(first.CurrentCredits,second.CurrentCredits),"fresh outputs, no reset");
        Check(first.Requested.Select(x=>x.Schedule.Id).SequenceEqual(new[]{20,10})&&second.Requested.Select(x=>x.Schedule.Id).SequenceEqual(new[]{20,10}),"request order, stable builds");
        Check(first.Requested[0].Schedule.Slots.Single()==monday&&first.Existing.Single(e=>e.Id==99).Slots.Single()==monday,"immutable slot reuse after copying containers");
        Check(first.Requested[0].Prerequisites.RequiredCourseIds.SetEquals(new[]{7,8})&&first.Requested[0].Prerequisites.CompletedCourseIds.SetEquals(new[]{7}),"external prerequisite/completion copies");
        Check(first.CurrentCredits[2]==24&&first.CurrentCredits[3]==4&&first.Existing.Select(e=>e.Id).Order().SequenceEqual(new[]{98,99}),"only submitted/approved prepare active load, periods separate");
        Check(first.Requested[0].Credits==7&&first.CreditLimit==30&&first.Requested[0].Schedule.Slots[0].Overlaps(first.Existing.Single(e=>e.Id==99).Slots[0]),"over-limit/missing-prerequisite/overlap preserved, not evaluated or repaired");
        builder.Existing(95,2,2,EnrollmentStatus.Submitted).Completed([8]).WithSlots(20,[new(DayOfWeek.Tuesday,new(9,0),new(10,0))]).Requires(20,[9]).Request(30,2,2);
        var third=builder.Build();
        Check(first.CurrentCredits[2]==24&&second.CurrentCredits[2]==24&&third.CurrentCredits[2]==26&&first.Existing.Count==2&&third.Existing.Count==3,"credit dictionary/existing snapshots independent");
        Check(first.Requested.Count==2&&second.Requested.Count==2&&third.Requested.Count==3,"builder mutation cannot change old outputs");
        Check(first.Requested[0].Schedule.Slots.Count==1&&second.Requested[0].Schedule.Slots.Count==1&&third.Requested[0].Schedule.Slots.Count==2,"nested schedule copies");
        Check(first.Requested[0].Prerequisites.RequiredCourseIds.SetEquals(new[]{7,8})&&!first.Requested[0].Prerequisites.CompletedCourseIds.Contains(8)&&third.Requested[0].Prerequisites.CompletedCourseIds.Contains(8),"nested frozen sets independent");
        Throws<NotSupportedException>(()=>((IList<OfferingEligibility>)first.Requested).Clear(),"outer list read-only");
        Throws<NotSupportedException>(()=>((IList<WeeklyTimeSlot>)first.Requested[0].Schedule.Slots).Clear(),"nested slots read-only");
        Throws<NotSupportedException>(()=>((IList<OfferingSchedule>)first.Existing).Clear(),"existing list read-only");
        Throws<NotSupportedException>(()=>((IDictionary<int,long>)first.CurrentCredits)[2]=0,"credits dictionary read-only");
        var changedRecord=first.Requested[0] with {Credits=1};Check(changedRecord.Credits==1&&first.Requested[0].Credits==7,"with makes a new value, cannot replace read-only element");
        var empty=new EligibilityScenarioBuilder(30).Build();Check(empty.Requested.Count==0&&empty.Existing.Count==0&&empty.CurrentCredits.Count==0,"separate builder has no shared state");
        Check(new EligibilityScenarioBuilder(30).Request(1,1,1).Build().Requested[0].Prerequisites.CompletedCourseIds.Count==0,"no auto-completion defaults");
        foreach(var bad in new[]{0,-1})
        {
            Throws<ArgumentOutOfRangeException>(()=>new EligibilityScenarioBuilder(bad),"limit");
            Throws<ArgumentOutOfRangeException>(()=>new EligibilityScenarioBuilder(30).Request(bad,1,1),"ID");
            Throws<ArgumentOutOfRangeException>(()=>new EligibilityScenarioBuilder(30).Request(1,bad,1),"term");
            Throws<ArgumentOutOfRangeException>(()=>new EligibilityScenarioBuilder(30).Request(1,1,bad),"credits");
            Throws<ArgumentOutOfRangeException>(()=>new EligibilityScenarioBuilder(30).Completed([bad]),"completed ID");
            Throws<ArgumentOutOfRangeException>(()=>new EligibilityScenarioBuilder(30).Request(1,1,1).Requires(1,[bad]),"required ID");
        }
        var structure=new EligibilityScenarioBuilder(30).Request(1,1,4).WithSlots(1,[monday]);
        Throws<ArgumentException>(()=>structure.Request(1,2,5),"conflicting definition");
        Throws<ArgumentException>(()=>structure.Existing(1,1,4,EnrollmentStatus.Approved),"request/existing duplicate");
        Throws<ArgumentException>(()=>structure.Request(1,1,4),"identical definition also rejected");
        Throws<ArgumentException>(()=>structure.WithSlots(77,[monday]),"undefined slot target");
        Throws<ArgumentException>(()=>structure.Requires(77,[1]),"undefined prerequisite target");
        Throws<ArgumentException>(()=>structure.WithSlots(1,[monday]),"exact duplicate slot");
        Throws<ArgumentException>(()=>structure.WithSlots(1,[null!]),"null slot element");
        Throws<ArgumentNullException>(()=>structure.WithSlots(1,null!),"null slot collection");
        Throws<ArgumentNullException>(()=>structure.Completed(null!),"null completion collection");
        Throws<ArgumentNullException>(()=>structure.Requires(1,null!),"null prerequisite collection");
        Throws<ArgumentOutOfRangeException>(()=>structure.Existing(2,1,1,(EnrollmentStatus)99),"unknown status");
        Throws<ArgumentOutOfRangeException>(()=>structure.WithSlots(1,[new((DayOfWeek)7,new(9,0),new(10,0))]),"invalid day via production constructor");
        foreach(var end in new[]{new TimeOnly(9,0),new TimeOnly(8,0)})
            Throws<ArgumentOutOfRangeException>(()=>structure.WithSlots(1,[new(DayOfWeek.Monday,new(9,0),end)]),"invalid interval");
        Check(structure.Build().Requested.Count==1&&structure.Build().Requested[0].Schedule.Slots.Count==1,"failed structural mutations leave preparation intact");
        // Multiple different overlapping slots are deliberately supported, without normalization.
        structure.WithSlots(1,[new(DayOfWeek.Monday,new(9,30),new(10,30))]);
        Check(structure.Build().Requested[0].Schedule.Slots.Count==2,"multiple valid slots preserved");
        Check(typeof(AcademicEligibility).Assembly.GetType(typeof(EligibilityScenarioBuilder).FullName!) is null,"builder excluded from web assembly");
        if(probe)Check(false,"controlled runner failure");
        Console.WriteLine("PASS: builder independent builds/no reset, input and nested copies, read-only containers, explicit ordering/state/term/credit values, structural errors, no repair/evaluation or DB fixture; absent from web assembly");
    }
}
