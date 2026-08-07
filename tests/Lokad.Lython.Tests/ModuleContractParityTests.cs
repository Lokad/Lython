using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ModuleContractParityTests
{
    [Fact]
    public void StaticBuiltinModuleSurfaceIsResolvableAtRuntime()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var missing = new List<string>();

        foreach (var moduleName in StaticContracts.GetKnownBuiltinModuleNames())
        {
            var module = LythonRuntime.ResolveBuiltinModule(moduleName, context);
            if (module is null)
            {
                missing.Add(moduleName + ":<module>");
                continue;
            }

            foreach (var memberName in StaticContracts.GetModuleMemberNames(moduleName))
            {
                if (!PyMemberAccess.TryResolve(module, memberName, context, span, out _))
                {
                    missing.Add(moduleName + ":" + memberName);
                }
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void ImportStarUsesTheSharedBuiltinModuleSurface()
    {
        var result = new LythonEngine().Run(
            """
from math import *
from hashlib import *
from shlex import *

return [sqrt(81), sha256(b"abc").hexdigest()[:8], split("a 'b c'")]
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            new object?[] { 9d, "ba7816bf", new List<object?> { "a", "b c" } },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }
}
