// src/Tapestry.Engine/Persistence/IAccountStore.cs
namespace Tapestry.Engine.Persistence;

public interface IAccountStore
{
    Task<AccountSaveData?> LoadByIdAsync(Guid accountId);
    Task<AccountSaveData?> LoadByEmailAsync(string email);
    Task SaveAsync(AccountSaveData data);
    Task DeleteAsync(Guid accountId);
    bool ExistsByEmail(string email);
}
