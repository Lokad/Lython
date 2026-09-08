using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ModuleContractParityTests
{
    [Fact]
    public void StaticBuiltinModuleInventories_AreCanonicalSnapshots()
    {
        Assert.Same(StaticContracts.GetKnownBuiltinModuleNames(), StaticContracts.GetKnownBuiltinModuleNames());
        Assert.Same(StaticContracts.GetModuleMemberNames("hashlib"), StaticContracts.GetModuleMemberNames("hashlib"));
        Assert.Same(StaticContracts.GetModuleExportedMemberNames("hashlib"), StaticContracts.GetModuleExportedMemberNames("hashlib"));

        Assert.Equal(
            StaticContracts.GetKnownBuiltinModuleNames().Order(StringComparer.Ordinal),
            StaticContracts.GetKnownBuiltinModuleNames());
        Assert.Equal(
            StaticContracts.GetModuleMemberNames("hashlib").Order(StringComparer.Ordinal),
            StaticContracts.GetModuleMemberNames("hashlib"));
    }

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
    public void ImportStarSucceedsForEveryBuiltinModule()
    {
        var failed = new List<string>();

        foreach (var moduleName in StaticContracts.GetKnownBuiltinModuleNames())
        {
            var host = new MockLythonHost();
            host.EnableSubprocess();
            var result = new LythonEngine().Run(
                "from " + moduleName + " import *\nreturn 1\n",
                host);
            if (!result.Success)
            {
                failed.Add(moduleName + ": " + (result.Failure?.Message
                    ?? string.Join(",", result.Diagnostics.Select(diagnostic => diagnostic.Code))));
            }
        }

        Assert.Empty(failed);
    }

    [Theory]
    [InlineData("operator.truth", "import operator\nreturn operator.truth([])", "Boolean")]
    [InlineData("sys.getdefaultencoding", "import sys\nreturn sys.getdefaultencoding()", "String")]
    [InlineData("math.floor", "import math\nreturn math.floor(1.5)", "Integer")]
    [InlineData("operator.length_hint", "import operator\nreturn operator.length_hint([1, 2])", "Integer")]
    [InlineData("operator.ge", "import operator\nreturn operator.ge(1, 2)", "Boolean")]
    [InlineData("math.fabs", "import math\nreturn math.fabs(1)", "Float")]
    [InlineData("statistics.variance", "import statistics\nreturn statistics.variance([1, 2, 3])", "Float")]
    [InlineData("statistics.harmonic_mean", "import statistics\nreturn statistics.harmonic_mean([1, 2, 4])", "Float")]
    [InlineData("random.randrange", "import random\nreturn random.randrange(0, 5)", "Integer")]
    [InlineData("os.path.normpath", "import os.path\nreturn os.path.normpath(\"a/./b\")", "String")]
    public void KnownCallReturnShapesAgreeWithRuntime(string targetName, string source, string expectedShape)
    {
        Assert.True(
            StaticContracts.TryGetKnownCallContract(targetName, out var contract),
            "Missing static contract for " + targetName);
        Assert.Equal(expectedShape, contract.ReturnShape.ToString());

        var result = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expectedShape, ClassifyRuntimeShape(result.ReturnValue));
    }

    private static string ClassifyRuntimeShape(object? value)
        => value switch
        {
            bool => "Boolean",
            string or PyString => "String",
            BigInteger or int or long => "Integer",
            double => "Float",
            null or PyNone => "None",
            _ => value.GetType().Name,
        };

    [Fact]
    public void DirEntriesResolveAsMembers()
    {
        var result = new LythonEngine().Run(
            """
targets = ["", b"", [], {}, set(), type([])]
try:
    {}["k"]
except KeyError as caught:
    targets.append(caught)
import math
targets.append(math)
missing = []
for target in targets:
    for name in dir(target):
        try:
            getattr(target, name)
        except AttributeError:
            missing.append(name)
return "|".join(missing)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("", result.ReturnValue);
    }

    [Fact]
    public void ExportedNamesAreSubsetOfMemberNames()
    {
        var extras = new List<string>();

        foreach (var moduleName in StaticContracts.GetKnownBuiltinModuleNames())
        {
            var members = new HashSet<string>(StaticContracts.GetModuleMemberNames(moduleName), StringComparer.Ordinal);
            foreach (var exported in StaticContracts.GetModuleExportedMemberNames(moduleName))
            {
                if (!members.Contains(exported))
                {
                    extras.Add(moduleName + ":" + exported);
                }
            }
        }

        Assert.Empty(extras);
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
    [Fact]
    public void ExceptionAncestryEntriesResolveAtRuntime()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var missing = new List<string>();

        foreach (var identity in LythonRuntime.ExceptionBaseIdentities.Keys)
        {
            if (identity.IsBuiltin)
            {
                continue;
            }

            var module = LythonRuntime.ResolveBuiltinModule(identity.ModuleName, context);
            if (module is null)
            {
                missing.Add(identity.QualifiedName + ":<module>");
                continue;
            }

            if (!PyMemberAccess.TryResolve(module, identity.TypeName, context, span, out _))
            {
                missing.Add(identity.QualifiedName);
            }
        }

        Assert.Empty(missing);
    }
}
