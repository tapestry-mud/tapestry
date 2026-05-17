// tests/Tapestry.Engine.Tests/Login/EmailValidatorTests.cs
using FluentAssertions;
using Tapestry.Engine.Login;

namespace Tapestry.Engine.Tests.Login;

public class EmailValidatorTests
{
    [Theory]
    [InlineData("travis@example.com", true)]
    [InlineData("user+tag@sub.domain.com", true)]
    [InlineData("a@b.co", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("noatsign", false)]
    [InlineData("two@@signs.com", false)]
    [InlineData("@nodomain.com", false)]
    [InlineData("nolocal@", false)]
    [InlineData("nodot@domain", false)]
    public void Validate_ReturnsExpected(string email, bool expectedValid)
    {
        var (valid, _) = EmailValidator.Validate(email);
        valid.Should().Be(expectedValid);
    }

    [Fact]
    public void Validate_Invalid_ReturnsErrorMessage()
    {
        var (valid, error) = EmailValidator.Validate("");
        valid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("Travis@Example.COM", "travis@example.com")]
    [InlineData("  user@test.com  ", "user@test.com")]
    public void Normalize_LowercasesAndTrims(string input, string expected)
    {
        EmailValidator.Normalize(input).Should().Be(expected);
    }
}
