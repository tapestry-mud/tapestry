// tests/Tapestry.Engine.Tests/Persistence/AccountServiceTests.cs
using FluentAssertions;
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Tests.Persistence;

public class AccountServiceTests
{
    private readonly FakeAccountStore _store;
    private readonly AccountService _svc;

    public AccountServiceTests()
    {
        _store = new FakeAccountStore();
        _svc = new AccountService(_store);
    }

    [Fact]
    public async Task CreateAccount_ReturnsAccountWithHashedPassword()
    {
        var account = await _svc.CreateAccount("test@example.com", "password123");

        account.Email.Should().Be("test@example.com");
        account.PasswordHash.Should().NotBe("password123");
        account.PasswordHash.Should().StartWith("$2");
        account.Characters.Should().BeEmpty();
        account.Id.Should().NotBe(Guid.Empty);
        _store.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Authenticate_ValidCredentials_ReturnsAccount()
    {
        await _svc.CreateAccount("auth@test.com", "secret");

        var result = await _svc.Authenticate("auth@test.com", "secret");
        result.Should().NotBeNull();
        result!.Email.Should().Be("auth@test.com");
    }

    [Fact]
    public async Task Authenticate_WrongPassword_ReturnsNull()
    {
        await _svc.CreateAccount("auth@test.com", "secret");

        var result = await _svc.Authenticate("auth@test.com", "wrong");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_NonExistentEmail_ReturnsNull()
    {
        var result = await _svc.Authenticate("nobody@test.com", "password");
        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateById_ValidCredentials_ReturnsAccount()
    {
        var account = await _svc.CreateAccount("byid@test.com", "mypass");

        var result = await _svc.AuthenticateById(account.Id, "mypass");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateById_WrongPassword_ReturnsNull()
    {
        var account = await _svc.CreateAccount("byid@test.com", "mypass");

        var result = await _svc.AuthenticateById(account.Id, "wrong");
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddCharacterToAccount_UpdatesCharacterList()
    {
        var account = await _svc.CreateAccount("chars@test.com", "pass");

        await _svc.AddCharacterToAccount(account.Id, "mallek");
        await _svc.AddCharacterToAccount(account.Id, "siron");

        var loaded = await _store.LoadByIdAsync(account.Id);
        loaded!.Characters.Should().ContainInOrder("mallek", "siron");
    }

    [Fact]
    public async Task RemoveCharacterFromAccount_UpdatesList()
    {
        var account = await _svc.CreateAccount("remove@test.com", "pass");
        await _svc.AddCharacterToAccount(account.Id, "mallek");
        await _svc.AddCharacterToAccount(account.Id, "siron");

        await _svc.RemoveCharacterFromAccount(account.Id, "mallek");

        var loaded = await _store.LoadByIdAsync(account.Id);
        loaded!.Characters.Should().ContainSingle().Which.Should().Be("siron");
    }

    [Fact]
    public void TrackAndGetOnlineEntity_Works()
    {
        var entityId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        _svc.TrackOnlineEntity(entityId, accountId);
        _svc.GetAccountForEntity(entityId).Should().Be(accountId);

        _svc.UntrackOnlineEntity(entityId);
        _svc.GetAccountForEntity(entityId).Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_UpdatesHash()
    {
        var account = await _svc.CreateAccount("change@test.com", "oldpass");

        var result = await _svc.ChangePassword(account.Id, "oldpass", "newpass");
        result.Should().BeTrue();

        var check = await _svc.AuthenticateById(account.Id, "newpass");
        check.Should().NotBeNull();

        var oldCheck = await _svc.AuthenticateById(account.Id, "oldpass");
        oldCheck.Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_WrongOldPassword_ReturnsFalse()
    {
        var account = await _svc.CreateAccount("change@test.com", "oldpass");

        var result = await _svc.ChangePassword(account.Id, "wrong", "newpass");
        result.Should().BeFalse();
    }
}

internal class FakeAccountStore : IAccountStore
{
    public List<AccountSaveData> Saved { get; } = new();
    private readonly Dictionary<Guid, AccountSaveData> _byId = new();
    private readonly Dictionary<string, Guid> _byEmail = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(AccountSaveData data)
    {
        Saved.Add(data);
        _byId[data.Id] = data;
        _byEmail[data.Email] = data.Id;
        return Task.CompletedTask;
    }

    public Task<AccountSaveData?> LoadByIdAsync(Guid accountId)
    {
        return Task.FromResult(_byId.GetValueOrDefault(accountId));
    }

    public Task<AccountSaveData?> LoadByEmailAsync(string email)
    {
        if (_byEmail.TryGetValue(email, out var id))
        {
            return LoadByIdAsync(id);
        }
        return Task.FromResult<AccountSaveData?>(null);
    }

    public Task DeleteAsync(Guid accountId)
    {
        if (_byId.TryGetValue(accountId, out var data))
        {
            _byEmail.Remove(data.Email);
            _byId.Remove(accountId);
        }
        return Task.CompletedTask;
    }

    public bool ExistsByEmail(string email)
    {
        return _byEmail.ContainsKey(email);
    }
}
