namespace Tapestry.Engine.Persistence;

public interface IPlayerStore
{
    Task<PlayerSaveData?> LoadAsync(string playerName);
    Task SaveAsync(PlayerSaveData data);
    Task DeleteAsync(string playerName);
    bool Exists(string playerName);
}
