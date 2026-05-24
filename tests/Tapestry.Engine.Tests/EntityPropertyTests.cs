using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class EntityPropertyTests
{
    // JS (Jint) stores numbers as double. C# readers want int. These must interop so
    // TryGetProperty can be the standard accessor everywhere.

    [Fact]
    public void TryGetProperty_Int_ReadsStoredDouble()
    {
        var e = new Entity("player", "T");
        e.SetProperty("sustenance", 80.0); // JS-written number arrives as double

        e.TryGetProperty<int>("sustenance", out var v).Should().BeTrue();
        v.Should().Be(80);
    }

    [Fact]
    public void TryGetProperty_Double_ReadsStoredInt()
    {
        var e = new Entity("player", "T");
        e.SetProperty("rate", 5); // C#-written int

        e.TryGetProperty<double>("rate", out var v).Should().BeTrue();
        v.Should().Be(5.0);
    }

    [Fact]
    public void GetProperty_Int_ReadsStoredDouble()
    {
        var e = new Entity("player", "T");
        e.SetProperty("sustenance", 80.0);

        e.GetProperty<int>("sustenance").Should().Be(80);
    }

    [Fact]
    public void TryGetProperty_Int_FastPath_StillWorks()
    {
        var e = new Entity("player", "T");
        e.SetProperty("n", 42);

        e.TryGetProperty<int>("n", out var v).Should().BeTrue();
        v.Should().Be(42);
    }

    [Fact]
    public void TryGetProperty_Int_DoesNotCoerceString()
    {
        var e = new Entity("player", "T");
        e.SetProperty("s", "hello");

        e.TryGetProperty<int>("s", out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetProperty_Int_DoesNotCoerceBool()
    {
        var e = new Entity("player", "T");
        e.SetProperty("flag", true);

        e.TryGetProperty<int>("flag", out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetProperty_String_NotAffectedByNumericCoercion()
    {
        var e = new Entity("player", "T");
        e.SetProperty("name", "Rocky");

        e.TryGetProperty<string>("name", out var v).Should().BeTrue();
        v.Should().Be("Rocky");
    }
}
