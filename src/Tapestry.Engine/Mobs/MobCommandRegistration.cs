// src/Tapestry.Engine/Mobs/MobCommandRegistration.cs
namespace Tapestry.Engine.Mobs;

public class MobCommandRegistration
{
    public required Action<MobContext, string> Handler { get; init; }
    public string? GmcpChannel { get; init; }
    public bool PrependSender { get; init; }
}
