using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Interop;

/// <summary>
/// Late-bound namespace proxy returned by <c>tapestry.packs.require('@scope/pack')</c>.
/// Member access resolves against <see cref="PackExportRegistry"/> at USE time (not at
/// require() time), and the dependency edge is enforced against a LIVE read of
/// <c>__currentPack</c> — so a proxy captured at load keeps gating correctly inside
/// deferred handlers, and an importer file that sorts before its exporter still works.
/// Function exports are wrapped so they run attributed to the exporter's pack
/// (InvokeAsPack); namespace/data exports are returned as-is (single shared realm) and
/// should be treated as read-only by convention. Game-loop thread only, like all interop.
/// Enumeration (Object.keys, for...in, JSON.stringify) yields no keys — the proxy is a
/// read-accessor, not a data container; use tapestry.packs.getExportRegistry() for introspection.
/// </summary>
public sealed class RequireProxy : ObjectInstance
{
    private readonly JintEngine _jsEngine;
    private readonly string _targetPack;
    private readonly PackExportRegistry _exports;
    private readonly Action<string, string> _enforceEdge;

    public RequireProxy(
        JintEngine engine,
        string targetPack,
        PackExportRegistry exports,
        Action<string, string> enforceEdge)
        : base(engine)
    {
        _jsEngine = engine;
        _targetPack = targetPack;
        _exports = exports;
        _enforceEdge = enforceEdge;
        // Give the proxy Object.prototype so JS operator checks (typeof, instanceof, etc.)
        // work correctly. engine.Intrinsics is the public surface; Realm is internal.
        Prototype = engine.Intrinsics.Object.PrototypeObject;
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (property.Type != Types.String)
        {
            // Symbols and other engine-internal protocol probes: not export members.
            return base.GetOwnProperty(property);
        }

        var member = property.ToString();

        // Live read of __currentPack at USE time, never snapshotted at require() time.
        var caller = PackLoader.PackNamespace(_jsEngine.GetValue("__currentPack").ToString());
        _enforceEdge(caller, _targetPack);

        if (!_exports.TryResolve(_targetPack, member, out var entry))
        {
            return PropertyDescriptor.Undefined;
        }

        if (entry.Handler is Jint.Native.Function.Function)
        {
            // A fresh wrapper per access (proxy.fn !== proxy.fn is fine: the wrapper is the
            // attribution boundary and cannot be cached across callers or call sites).
            var fn = new ClrFunction(
                _jsEngine,
                member,
                (thisObj, args) => _jsEngine.InvokeAsPack(entry.Pack, entry.Handler, null, args));

            return new PropertyDescriptor(fn, writable: false, enumerable: true, configurable: false);
        }

        // Namespace/data export: return the stored JsValue raw (same shared realm).
        return new PropertyDescriptor(entry.Handler, writable: false, enumerable: true, configurable: false);
    }

    /// <summary>
    /// Assignment via the virtual 3-arg Set path (used by Jint's property-set bytecode).
    /// Throws an <see cref="InteropException"/> so callers see a CLR exception, not a
    /// silent no-op or a generic Jint TypeError.
    /// </summary>
    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        throw new InteropException(
            $"Pack exports are read-only: cannot assign '{property}' on require('{_targetPack}').");
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        throw new InteropException(
            $"Pack exports are read-only: cannot assign '{property}' on require('{_targetPack}').");
    }
}
