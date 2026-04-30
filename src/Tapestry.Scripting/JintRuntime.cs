using System.Dynamic;
using Jint;
using Microsoft.Extensions.Logging;

using JintEngine = Jint.Engine;

namespace Tapestry.Scripting;

public class JintRuntime
{
    private readonly IEnumerable<IJintApiModule> _modules;
    private readonly JintEngine _engine;
    private readonly ILogger<JintRuntime> _logger;

    public JintRuntime(IEnumerable<IJintApiModule> modules, ILogger<JintRuntime> logger)
    {
        _modules = modules;
        _logger = logger;
        _engine = new JintEngine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
            options.LimitRecursion(100);
            options.Strict();
        });

        SetupApi();
    }

    public void Execute(string script, string packName)
    {
        _engine.SetValue("__currentPack", packName);
        _engine.SetValue("__currentSource", "");
        _engine.Execute(script);
    }

    public void Execute(string script, string packName, string sourceFile)
    {
        _engine.SetValue("__currentPack", packName);
        _engine.SetValue("__currentSource", sourceFile);
        _engine.Execute(script, source: sourceFile);
    }

    /// <summary>
    /// Execute a script without a pack name. Convenience for tests.
    /// </summary>
    public void Execute(string script)
    {
        _engine.Execute(script);
    }

    /// <summary>
    /// Evaluate an expression and return the result as a CLR object (or null).
    /// </summary>
    public object? Evaluate(string expression)
    {
        var result = _engine.Evaluate(expression);
        if (result == null || result.IsNull() || result.IsUndefined())
        {
            return null;
        }
        return result.ToObject();
    }

    /// <summary>
    /// No-op — API is built in the constructor. Kept for explicit-initialization tests.
    /// </summary>
    public void Initialize()
    {
    }

    private void SetupApi()
    {
        var tapestry = new ExpandoObject() as IDictionary<string, object?>;
        foreach (var module in _modules)
        {
            tapestry[module.Namespace] = module.Build(_engine);
        }
        _engine.SetValue("tapestry", tapestry);
    }
}
