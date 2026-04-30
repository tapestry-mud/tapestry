using JintEngine = Jint.Engine;

namespace Tapestry.Scripting;

public interface IJintApiModule
{
    string Namespace { get; }
    object Build(JintEngine engine);
}
