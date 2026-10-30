using System.Text.Json.Nodes;
using UniversityRegistration.Application;

// Test process output observer: retains only numeric timing fields and request-finish
// counters, never SQL, request bodies, personal data or connection strings.
internal sealed class EvaluationLogObserver
{
    readonly object gate = new();
    readonly List<(string Outcome,int? Count)> entries=[];
    readonly Dictionary<string,int> finished=[];
    string? invalid;
    public int Audited { get; private set; }
    public void Receive(string? line)
    {
        if(string.IsNullOrWhiteSpace(line)) return;
        JsonNode? json;
        try { json=JsonNode.Parse(line); } catch(System.Text.Json.JsonException){return;}
        lock(gate)
        {
            if(json?["EventId"]?.GetValue<int>()==1701 && json["Category"]?.GetValue<string>()==typeof(TimingAcademicEligibilityEvaluator).FullName)
            {
                var state=json["State"]!;
                var elapsed=state["ElapsedMs"]!.GetValue<double>();
                if(!double.IsFinite(elapsed)||elapsed<0)invalid="nonfinite/negative elapsed";
                entries.Add((state["Outcome"]!.GetValue<string>(),state["FailureCount"]?.GetValue<int>()));
            }
            if(json?["EventId"]?.GetValue<int>()==2 && json["Category"]?.GetValue<string>()=="Microsoft.AspNetCore.Hosting.Diagnostics")
            {
                var path=json["State"]?["Path"]?.GetValue<string>();
                if(path is not null)finished[path]=finished.GetValueOrDefault(path)+1;
            }
        }
    }
    public (int Entries,int Finished) Before(string path){lock(gate)return(entries.Count,finished.GetValueOrDefault(path));}
    public async Task Verify(string method,string path,int status,JsonNode body,(int Entries,int Finished) before)
    {
        // The console queue emits calculation before RequestFinished. Wait for that
        // explicit completion barrier, not a guessed sleep duration. Audited mode is serial.
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while(true)
        {
            lock(gate){if(finished.GetValueOrDefault(path)>before.Finished)break;}
            try {await Task.Delay(10,timeout.Token);}catch(OperationCanceledException){throw new BehaviorFailure("Decorator HTTP log completion missing: "+path);}
        }
        var evaluated=method=="POST" &&
            (path.EndsWith("/eligibility")&&status==200 ||
             (path=="/enrollments"||path.EndsWith("/batch"))&&(status==201||body["failures"] is not null) ||
             (path.EndsWith("/submit")||path.EndsWith("/approve"))&&(status==200||body["failures"] is not null));
        lock(gate)
        {
            if(invalid is not null)throw new BehaviorFailure("Decorator log: "+invalid);
            var observed=entries.Skip(before.Entries).ToArray();
            if(observed.Length!=(evaluated?1:0))throw new BehaviorFailure($"Decorator HTTP {path}: expected {(evaluated?1:0)} logs, actual {observed.Length}");
            if(evaluated)
            {
                var count=body["failures"]?.AsArray().Count??0;
                if(observed[0]!=(count==0?"eligible":"ineligible",count))throw new BehaviorFailure("Decorator HTTP outcome/count mismatch: "+path);
                Audited++;
            }
            if(body.ToJsonString().Contains("elapsedMs",StringComparison.OrdinalIgnoreCase))throw new BehaviorFailure("Timing leaked to HTTP");
        }
    }
}
