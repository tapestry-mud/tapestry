using Tapestry.Server.Login;

namespace Tapestry.Server.Tests.Login;

public class PasswordValidatorTests
{
    [Fact]
    public void Validate_ReturnsOk_WhenPasswordAtFloor()
    {
        var (ok, error) = PasswordValidator.Validate("abcdef", 6);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsOk_WhenPasswordAboveFloor()
    {
        var (ok, error) = PasswordValidator.Validate("abcdefgh", 6);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenPasswordBelowFloor()
    {
        var (ok, error) = PasswordValidator.Validate("abc", 6);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("6", error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenPasswordEmpty()
    {
        var (ok, error) = PasswordValidator.Validate("", 6);
        Assert.False(ok);
        Assert.NotNull(error);
    }
}
