// src/Tapestry.Engine/Combat/ICombatPhase.cs
using Tapestry.Engine.Heartbeat;

namespace Tapestry.Engine.Combat;

public interface ICombatPhase
{
    string Name { get; }
    int Priority { get; }
    void Execute(PulseContext context);
}
