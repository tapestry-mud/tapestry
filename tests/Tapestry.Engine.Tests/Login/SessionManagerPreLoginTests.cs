using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Login;

namespace Tapestry.Engine.Tests.Login;

public class SessionManagerPreLoginTests
{
    private static LoginContext MakeCtx(string id = "c1")
    {
        return new LoginContext(id, new FakeConnection(id), LoginPhase.Name);
    }

    [Fact]
    public void RegisterPreLogin_MakesContextRetrievable()
    {
        var sut = new SessionManager();
        var ctx = MakeCtx("c1");

        sut.RegisterPreLogin(ctx);

        sut.GetPreLogin("c1").Should().BeSameAs(ctx);
    }

    [Fact]
    public void RemovePreLogin_RemovesContext()
    {
        var sut = new SessionManager();
        var ctx = MakeCtx("c1");
        sut.RegisterPreLogin(ctx);

        sut.RemovePreLogin("c1");

        sut.GetPreLogin("c1").Should().BeNull();
    }

    [Fact]
    public void AllPreLoginConnections_ReturnsAllRegisteredContexts()
    {
        var sut = new SessionManager();
        sut.RegisterPreLogin(MakeCtx("c1"));
        sut.RegisterPreLogin(MakeCtx("c2"));

        sut.AllPreLoginConnections.Should().HaveCount(2);
    }

    [Fact]
    public void ConnectionCount_IncludesPreLoginConnections()
    {
        var sut = new SessionManager();
        sut.RegisterPreLogin(MakeCtx("c1"));

        sut.ConnectionCount.Should().Be(1);
    }

    [Fact]
    public void AllConnectionsByPhase_FiltersCorrectly()
    {
        var sut = new SessionManager();
        var nameCtx = MakeCtx("c1");
        nameCtx.Phase = LoginPhase.Name;
        var passCtx = MakeCtx("c2");
        passCtx.Phase = LoginPhase.Password;
        sut.RegisterPreLogin(nameCtx);
        sut.RegisterPreLogin(passCtx);

        var result = sut.AllConnectionsByPhase(LoginPhase.Name).ToList();

        result.Should().HaveCount(1);
        result[0].Should().BeSameAs(nameCtx);
    }
}
