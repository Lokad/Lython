using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
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

    // R18: set call shapes agree across layers. Each snippet below executes
    // through BOTH the static contract checker and the runtime member
    // factories: accepted shapes run cleanly end to end (static silence plus
    // runtime success), rejected shapes fail statically with the contract
    // diagnostic and fail at runtime with TypeError on direct member
    // invocation. The tests execute behavior; they never compare the two
    // copies of the shape facts textually.
    [Theory]
    [InlineData("s = {1, 2}\ns.add(3)\nreturn s == {1, 2, 3}")]
    [InlineData("s = {1}\nreturn s.union({2}, {3}) == {1, 2, 3}")]
    [InlineData("s = {1, 2}\nreturn s.isdisjoint({3})")]
    [InlineData("s = {1}\ns.update({2})\nreturn s == {1, 2}")]
    [InlineData("s = {1, 2}\ns.discard(1)\ns.remove(2)\nreturn len(s) == 0")]
    [InlineData("d = {\"a\": 1}\nreturn d.keys().isdisjoint({\"b\"})")]
    public void SetCallAcceptedShapesRunCleanly(string source)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(true, result.ReturnValue);
    }

    [Theory]
    [InlineData("s = {1}\ns.add()", "add", 0, "LA3135")]
    [InlineData("s = {1}\ns.add(1, 2)", "add", 2, "LA3135")]
    [InlineData("s = {1}\ns.isdisjoint()", "isdisjoint", 0, "LA3139")]
    public void SetCallRejectedShapesFailBothLayers(string source, string member, int argumentCount, string expectedCode)
    {
        var staticResult = new LythonEngine().Run(source, new MockLythonHost());
        Assert.False(staticResult.Success);
        Assert.Null(staticResult.Failure);
        Assert.Contains(staticResult.Diagnostics, diagnostic => diagnostic.Code == expectedCode);

        Assert.Equal("TypeError", InvokeSetMember(member, argumentCount));
    }

    private static string InvokeSetMember(string member, int argumentCount)
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var set = new PySet(context.MemoryGovernor, span);
        Assert.True(PyMemberAccess.TryResolve(set, member, context, span, out var resolved));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(resolved);
        var arguments = new CallArgumentValue[argumentCount];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = CallArgumentValue.Positional(PyNone.Instance);
        }

        try
        {
            callable.Invoke(arguments, span, context);
            return "ok";
        }
        catch (LythonRuntimeException exception) when (exception.ExceptionType == "TypeError")
        {
            return "TypeError";
        }
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
