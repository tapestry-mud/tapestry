// src/Tapestry.Engine/Persistence/AccountService.cs
namespace Tapestry.Engine.Persistence;

public class AccountService
{
    private readonly IAccountStore _store;
    private readonly Dictionary<Guid, Guid> _entityToAccount = new();

    public AccountService(IAccountStore store)
    {
        _store = store;
    }

    public async Task<AccountSaveData> CreateAccount(string email, string password)
    {
        var account = new AccountSaveData
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        await _store.SaveAsync(account);
        return account;
    }

    public async Task<AccountSaveData?> Authenticate(string email, string password)
    {
        var account = await _store.LoadByEmailAsync(email.Trim().ToLowerInvariant());
        if (account == null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
        {
            return null;
        }

        return account;
    }

    public async Task<AccountSaveData?> AuthenticateById(Guid accountId, string password)
    {
        var account = await _store.LoadByIdAsync(accountId);
        if (account == null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
        {
            return null;
        }

        return account;
    }

    public async Task AddCharacterToAccount(Guid accountId, string characterName)
    {
        var account = await _store.LoadByIdAsync(accountId);
        if (account == null)
        {
            return;
        }

        if (!account.Characters.Contains(characterName, StringComparer.OrdinalIgnoreCase))
        {
            account.Characters.Add(characterName);
            await _store.SaveAsync(account);
        }
    }

    public async Task RemoveCharacterFromAccount(Guid accountId, string characterName)
    {
        var account = await _store.LoadByIdAsync(accountId);
        if (account == null)
        {
            return;
        }

        account.Characters.RemoveAll(c => c.Equals(characterName, StringComparison.OrdinalIgnoreCase));
        await _store.SaveAsync(account);
    }

    public async Task<bool> ChangePassword(Guid accountId, string oldPassword, string newPassword)
    {
        var account = await _store.LoadByIdAsync(accountId);
        if (account == null)
        {
            return false;
        }

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, account.PasswordHash))
        {
            return false;
        }

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _store.SaveAsync(account);
        return true;
    }

    public void TrackOnlineEntity(Guid entityId, Guid accountId)
    {
        _entityToAccount[entityId] = accountId;
    }

    public void UntrackOnlineEntity(Guid entityId)
    {
        _entityToAccount.Remove(entityId);
    }

    public Guid? GetAccountForEntity(Guid entityId)
    {
        return _entityToAccount.TryGetValue(entityId, out var accountId) ? accountId : null;
    }
}
