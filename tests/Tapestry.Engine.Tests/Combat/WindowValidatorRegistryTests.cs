using Tapestry.Engine.Combat;

namespace Tapestry.Engine.Tests.Combat;

public class WindowValidatorRegistryTests
{
    private static CombatContext SampleContext(string verb)
    {
        return new CombatContext
        {
            Actor = new ActorView(Guid.NewGuid(), "perfect"),
            Target = new ActorView(Guid.NewGuid(), "wounded"),
            Phase = "swell",
            Swell = new SwellView("sweep", "sidestep", "full", true),
            Command = new CommandView(verb, null)
        };
    }

    [Fact]
    public void Register_ThenGet_InvokesValidator()
    {
        var registry = new WindowValidatorRegistry();
        registry.Register("telegraph-rung", ctx =>
            new ValidationResult
            {
                Outcome = ctx.Command.Verb == ctx.Swell!.RequiredCounter
                    ? WindowOutcome.Countered
                    : WindowOutcome.Whiffed,
                NarrationKey = "countered"
            });

        var validator = registry.Get("telegraph-rung");
        Assert.NotNull(validator);

        var hit = validator!(SampleContext("sidestep"));
        var miss = validator!(SampleContext("brace"));

        Assert.Equal(WindowOutcome.Countered, hit.Outcome);
        Assert.Equal(WindowOutcome.Whiffed, miss.Outcome);
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownName()
    {
        var registry = new WindowValidatorRegistry();
        Assert.Null(registry.Get("nope"));
    }
}
