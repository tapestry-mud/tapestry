using Acornima;
using Acornima.Ast;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Interop;

/// <summary>
/// Parses a pack script with Acornima (the parser Jint already bundles) and extracts every
/// statically resolvable <c>tapestry.packs.call</c>/<c>has</c> site — i.e. those whose first two
/// arguments are string literals. Dynamic-dispatch sites (computed pack/export) are skipped.
/// A parse failure yields no sites: Jint's own Execute surfaces the canonical syntax error, so we
/// must not preempt it here.
/// </summary>
public static class InteropCallSiteScanner
{
    public static IReadOnlyList<InteropCallSite> Extract(
        string source, string callerPackNamespace, string sourceFile)
    {
        var sites = new List<InteropCallSite>();

        Script ast;
        try
        {
            ast = new Parser().ParseScript(source, sourceFile, strict: false);
        }
        catch
        {
            return sites; // let Jint.Execute report the real syntax error
        }

        new Visitor(callerPackNamespace, sourceFile, sites).Visit(ast);
        return sites;
    }

    private sealed class Visitor : AstVisitor
    {
        private readonly string _caller;
        private readonly string _sourceFile;
        private readonly List<InteropCallSite> _sites;

        public Visitor(string caller, string sourceFile, List<InteropCallSite> sites)
        {
            _caller = caller;
            _sourceFile = sourceFile;
            _sites = sites;
        }

        protected override object? VisitCallExpression(CallExpression node)
        {
            if (TryMatch(node, out var targetLiteral, out var exportName, out var kind))
            {
                _sites.Add(new InteropCallSite(
                    _caller,
                    PackLoader.PackNamespace(targetLiteral),
                    exportName,
                    kind,
                    _sourceFile,
                    node.Location.Start.Line));
            }

            // Recurse into callee + arguments so nested interop calls are found.
            return base.VisitCallExpression(node);
        }

        // Matches `tapestry.packs.call(...)` / `tapestry.packs.has(...)` with two string-literal args,
        // and `tapestry.packs.require(...)` with one string-literal arg.
        private static bool TryMatch(
            CallExpression node, out string targetLiteral, out string exportName, out InteropCallKind kind)
        {
            targetLiteral = "";
            exportName = "";
            kind = InteropCallKind.Call;

            if (node.Callee is not MemberExpression methodAccess || methodAccess.Computed) { return false; }
            if (methodAccess.Property is not Identifier method) { return false; }
            if (method.Name != "call" && method.Name != "has" && method.Name != "require") { return false; }

            if (methodAccess.Object is not MemberExpression packsAccess || packsAccess.Computed) { return false; }
            if (packsAccess.Property is not Identifier packsId || packsId.Name != "packs") { return false; }
            if (packsAccess.Object is not Identifier rootId || rootId.Name != "tapestry") { return false; }

            var args = node.Arguments;

            if (method.Name == "require")
            {
                if (args.Count < 1 || args[0] is not StringLiteral requireLit) { return false; }
                targetLiteral = requireLit.Value;
                kind = InteropCallKind.Require;
                return true; // exportName stays "" — edge check only
            }

            if (args.Count < 2) { return false; }
            if (args[0] is not StringLiteral packLit) { return false; }
            if (args[1] is not StringLiteral exportLit) { return false; }

            targetLiteral = packLit.Value;
            exportName = exportLit.Value;
            kind = method.Name == "has" ? InteropCallKind.Has : InteropCallKind.Call;
            return true;
        }
    }
}
