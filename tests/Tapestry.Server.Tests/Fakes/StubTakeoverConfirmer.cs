using Tapestry.Server.Login;

namespace Tapestry.Server.Tests.Fakes;

public class StubTakeoverConfirmer : ITakeoverConfirmer
{
    private readonly bool _result;
    public int CallCount { get; private set; }

    public StubTakeoverConfirmer(bool result) { _result = result; }

    public Task<bool> ConfirmAsync(CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(_result);
    }
}
