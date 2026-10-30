using UniversityRegistration.DemoNotifications;
using UniversityRegistration.Application;

internal static class AdapterChecks
{
    static void Check(bool condition, string label)
    { if (!condition) throw new BehaviorFailure("Adapter: " + label); }
    static async Task<T> Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T error) { return error; }
        throw new BehaviorFailure("Adapter: expected " + typeof(T).Name);
    }
    // One shared harness; provider-specific completion/setup stays at this boundary.
    sealed record Harness(INotificationSender Sender, Func<int> Calls,
        Action<NotificationMessage,CancellationToken> VerifyFields,
        Action<string> Complete, Action<Exception> Fault, Action<CancellationToken> Cancel);
    static Harness Arrange(bool mail)
    {
        if (mail)
        {
            var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var provider = new FakeMailProvider((_,_,_,_) => pending.Task);
            return new(new MailNotificationAdapter(provider), () => provider.Calls,
                (message, token) => Check(provider.LastCall == (message.Recipient,message.Subject,message.Body,token),"mail field/token mapping"),
                outcome => { if(outcome=="accepted") pending.SetResult(" ref/Ö-123 ");
                    else pending.SetException(outcome=="rejected" ? new FakeRecipientRejectedException() : new FakeProviderUnavailableException()); },
                pending.SetException, pending.SetCanceled);
        }
        else
        {
            var pending = new TaskCompletionSource<FakePublishResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var provider = new FakePublishProvider((_,_) => pending.Task);
            return new(new PublishNotificationAdapter(provider), () => provider.Calls,
                (message, token) => Check(provider.LastCall == (new FakePublishRequest(message.Recipient,message.Subject,message.Body),token),"publish field/token mapping"),
                outcome => pending.SetResult(outcome switch { "accepted" => new(true," ref/Ö-123 ",null),
                    "rejected" => new(false,null,"INVALID_ADDRESS"), _ => new(false,null,"SERVICE_UNAVAILABLE") }),
                pending.SetException, pending.SetCanceled);
        }
    }
    public static async Task Run(bool probe = false)
    {
        var message = new NotificationMessage(" student@example.test "," Türkçe başlık "," Satır 1\nSatır 2 ");
        foreach(var mail in new[]{true,false})
        {
            foreach(var outcome in new[]{"accepted","rejected","unavailable","unexpected","cancelled"})
            {
                var h=Arrange(mail); using var cancellation=new CancellationTokenSource();
                var task=NotificationDemoCaller.SendAsync(h.Sender,message,cancellation.Token);
                Check(!task.IsCompleted && h.Calls()==1,"pending async call, exactly once");
                h.VerifyFields(message,cancellation.Token);
                if(outcome=="unexpected")
                {
                    var expected=new InvalidOperationException("Synthetic unexpected failure");h.Fault(expected);
                    var actual=await Throws<InvalidOperationException>(async()=>await task);
                    Check(ReferenceEquals(expected,actual),"unexpected exception identity preserved");
                }
                else if(outcome=="cancelled")
                {
                    cancellation.Cancel();h.Cancel(cancellation.Token);
                    var error=await Throws<OperationCanceledException>(async()=>await task);
                    Check(task.IsCanceled && error.CancellationToken==cancellation.Token,"canceled Task and same token, not unavailable");
                }
                else
                {
                    h.Complete(outcome);var result=await task;
                    var expected=outcome switch {"accepted"=>NotificationSendStatus.Accepted,"rejected"=>NotificationSendStatus.RecipientRejected,_=>NotificationSendStatus.ProviderUnavailable};
                    Check(result.Status==expected,"exact known status "+outcome);
                    Check(result.ProviderReference==(outcome=="accepted"?" ref/Ö-123 ":null),"lossless reference, none on failure");
                }
                Check(h.Calls()==1,"no retry/fallback");
            }
            var nullCase=Arrange(mail);
            var argument=await Throws<ArgumentNullException>(()=>nullCase.Sender.SendAsync(null!,default));
            Check(argument.ParamName=="message" && nullCase.Calls()==0,"null message rejected before provider");
        }
        foreach(var value in new string?[]{null,""," \t\r\n"})foreach(var field in new[]{"recipient","subject","body"})
        {
            var error=await Throws<ArgumentException>(()=>{
                _=new NotificationMessage(field=="recipient"?value!:"a@example.test",field=="subject"?value!:"s",field=="body"?value!:"b");
                return Task.CompletedTask;
            });
            Check(error.ParamName==field,"controlled input field "+field);
        }
        foreach(var invalid in new FakePublishResult?[]{
            new(true,"ref","INVALID_ADDRESS"),new(true,null,""),new(false,null,null),
            new(false,null,"UNKNOWN"),new(false,null,""),new(false,"ref","INVALID_ADDRESS"),
            new(false,"","SERVICE_UNAVAILABLE"),new(true," ",null),null })
        {
            var provider=new FakePublishProvider((_,_)=>Task.FromResult(invalid!));
            await Throws<InvalidOperationException>(()=>new PublishNotificationAdapter(provider).SendAsync(message,default));
            Check(provider.Calls==1,"malformed/unknown response not retried");
        }
        var noReference=new FakePublishProvider((_,_)=>Task.FromResult(new FakePublishResult(true,null,null)));
        var accepted=await NotificationDemoCaller.SendAsync(new PublishNotificationAdapter(noReference),message,default);
        Check(accepted.Status==NotificationSendStatus.Accepted && accepted.ProviderReference is null,"no invented reference");
        foreach(var reference in new string?[]{null,""," "})
            await Throws<InvalidOperationException>(()=>new MailNotificationAdapter(new FakeMailProvider((_,_,_,_)=>Task.FromResult(reference!))).SendAsync(message,default));
        var web=typeof(AcademicEligibility).Assembly;
        Check(web.GetType(typeof(NotificationMessage).FullName!) is null && web.GetType(typeof(MailNotificationAdapter).FullName!) is null,
            "demo excluded from web assembly");
        if(probe)Check(false,"controlled failure propagation probe");
        Console.WriteLine("PASS: both fake adapters/shared caller: fields/token/one call; accepted/rejected/unavailable; lossless or absent reference; null/input guards; delayed exceptions and cancellation; unknown/malformed results; no web assembly inclusion (no DB/network)");
    }
}
