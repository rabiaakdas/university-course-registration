// Deliberately typed learning experiment, excluded from the web assembly.
// One fixed slot per request type: duplicate registrations are not representable.
internal sealed record DemoRegister(int Value);
internal sealed record DemoRead(string Prefix);
internal sealed record DemoReceipt(int Value);
internal sealed record DemoSummary(string Text);

internal sealed class DemoRegisterHandler(Func<DemoRegister, CancellationToken, Task<DemoReceipt>> operation)
{
    public Task<DemoReceipt> HandleAsync(DemoRegister request, CancellationToken ct) => operation(request, ct);
}
internal sealed class DemoReadHandler(Func<DemoRead, CancellationToken, Task<DemoSummary>> operation)
{
    public Task<DemoSummary> HandleAsync(DemoRead request, CancellationToken ct) => operation(request, ct);
}

internal sealed class DemoMediator(DemoRegisterHandler? register, DemoReadHandler? read)
{
    public Task<DemoReceipt> SendAsync(DemoRegister request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (register ?? throw new InvalidOperationException("DemoRegister handler missing.")).HandleAsync(request, ct);
    }
    public Task<DemoSummary> SendAsync(DemoRead request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (read ?? throw new InvalidOperationException("DemoRead handler missing.")).HandleAsync(request, ct);
    }
}
