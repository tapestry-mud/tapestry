// tests/Tapestry.Engine.Tests/Persistence/AccountSaveDataTests.cs
using FluentAssertions;
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Tests.Persistence;

public class AccountSaveDataTests
{
    [Fact]
    public void NewAccount_HasEmptyDefaults()
    {
        var account = new AccountSaveData();

        account.Id.Should().Be(Guid.Empty);
        account.Email.Should().Be("");
        account.PasswordHash.Should().Be("");
        account.Characters.Should().BeEmpty();
        account.EmailVerified.Should().BeFalse();
        account.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public void Characters_CanAddMultiple()
    {
        var account = new AccountSaveData();
        account.Characters.Add("mallek");
        account.Characters.Add("siron");

        account.Characters.Should().HaveCount(2);
        account.Characters.Should().ContainInOrder("mallek", "siron");
    }
}
