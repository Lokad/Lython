using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ImportArchitectureSubsystemTests
{
    [Fact]
    public void ScriptAndModulesExposePythonNameMetadata()
    {
        var host = new CountingHost();
        host.SeedFile("/helper.py", "seen_name = __name__\n");

        var result = new LythonEngine().Run(
            """
import helper
import math
return [__name__, helper.__name__, helper.seen_name, math.__name__]
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            new object?[] { "__main__", "helper", "helper", "math" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void BuiltinImports_DoNotTouchHostModuleResolution()
    {
        var host = new CountingHost();

        var result = new LythonEngine().Run(
            """
import json, re
return json.dumps({"ok": True}) + "|" + str(re.search("a+", "caaab").span())
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{\"ok\": true}|(1, 4)", result.ReturnValue);
        Assert.Empty(host.ExistsCalls);
        Assert.Empty(host.ReadCalls);
    }

    [Fact]
    public void ModuleAttributesPreserveIdentityAndReflectScriptAssignments()
    {
        var host = new CountingHost();
        host.SeedFile("/helper.py", "value = 1\n");

        var result = new LythonEngine().Run(
            """
import hashlib
import helper

before = helper.value
helper.value = 2
return [
    hashlib.sha256 is hashlib.sha256,
    hashlib.__name__ is hashlib.__name__,
    before,
    helper.value,
]
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            new object?[] { true, true, new System.Numerics.BigInteger(1), new System.Numerics.BigInteger(2) },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void DottedBuiltinImports_FollowPythonPackageBindingRules()
    {
        var result = new LythonEngine().Run(
            """
import os.path, openpyxl.styles as styles
import os
import sys
return [
    os.path.basename("/repo/file.txt"),
    os.__name__,
    styles.__name__,
    os.path is sys.modules["os.path"],
    styles is sys.modules["openpyxl.styles"],
]
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            new object?[] { "file.txt", "os", "openpyxl.styles", true, true },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void DottedLocalImports_LoadCacheAndAttachAllowedPackageModules()
    {
        var host = new CountingHost();
        host.SeedFile("/repo/pkg/__init__.py", "package_value = 1\n");
        host.SeedFile("/repo/pkg/child.py", "value = 42\n");

        var result = new LythonEngine().Run(
            """
import pkg.child
import pkg.child as leaf
import sys
return [pkg.package_value, pkg.child.value, leaf is pkg.child, leaf is sys.modules["pkg.child"]]
""",
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/main.py",
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal)
                {
                    "pkg",
                    "pkg.child",
                    "/repo/pkg/__init__.py",
                    "/repo/pkg/child.py"
                }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            new object?[] { new System.Numerics.BigInteger(1), new System.Numerics.BigInteger(42), true, true },
            Assert.IsType<List<object?>>(result.ReturnValue));
        Assert.Equal(1, host.ReadCalls["/repo/pkg/__init__.py"]);
        Assert.Equal(1, host.ReadCalls["/repo/pkg/child.py"]);
    }

    [Fact]
    public async Task DottedLocalImports_RunThroughAsyncPackageLoading()
    {
        var host = new MockLythonHost();
        host.SeedFile("/repo/pkg/__init__.py", "package_value = 1\n");
        host.SeedFile("/repo/pkg/child.py", "value = 42\n");

        var result = await new LythonEngine().RunAsync(
            "import pkg.child as leaf\nreturn leaf.value\n",
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/main.py",
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal)
                {
                    "pkg",
                    "pkg.child",
                    "/repo/pkg/__init__.py",
                    "/repo/pkg/child.py"
                }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(new System.Numerics.BigInteger(42), result.ReturnValue);
    }

    [Fact]
    public void DottedImports_ReportMalformedNamesAtCompileTime()
    {
        var result = new LythonEngine().Compile("import os.\n");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "LA1001" && diagnostic.Message.Contains("after '.'", StringComparison.Ordinal));
    }

    [Fact]
    public void LocalModuleImports_AreCachedWithinRun_ButReloadAcrossRuns()
    {
        var host = new CountingHost();
        host.SeedFile(
            "/helper.py",
            """
count = 0
count += 1
value = count
""");

        const string source =
            """
import helper
import helper as again
return (helper.value, again.value)
""";

        var engine = new LythonEngine();

        var options = new LythonRunOptions
        {
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
        };
        var first = engine.Run(source, host, options);
        var second = engine.Run(source, host, options);

        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(2, host.ReadCalls["/helper.py"]);
        Assert.Equal(2, host.ExistsCalls["/helper.py"]);
        Assert.Equal(new object?[] { new System.Numerics.BigInteger(1), new System.Numerics.BigInteger(1) }, Assert.IsType<object?[]>(first.ReturnValue));
        Assert.Equal(new object?[] { new System.Numerics.BigInteger(1), new System.Numerics.BigInteger(1) }, Assert.IsType<object?[]>(second.ReturnValue));
    }

    [Fact]
    public void ScriptModuleImports_HaveStableIdentityWithinRun()
    {
        var host = new CountingHost();
        host.SeedFile("/helper.py", "value = 1");

        var result = new LythonEngine().Run("""
import helper
import helper as again
return helper is again
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(true, result.ReturnValue);
        Assert.Equal(1, host.ReadCalls["/helper.py"]);
    }

    [Fact]
    public void ImportedModules_CanResolveSiblingModules()
    {
        var host = new CountingHost();
        host.SeedFile("/shared.py", "value = 41");
        host.SeedFile("/helper.py", """
import shared
value = shared.value + 1
""");

        var result = new LythonEngine().Run("""
import helper
return helper.value
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper", "shared" }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(42), result.ReturnValue);
    }

    [Fact]
    public void LocalModuleAllowlist_PermitsReferencedScriptModules()
    {
        var host = new CountingHost();
        host.SeedFile("/helper.py", "value = 42");

        var result = new LythonEngine().Run(
            """
import helper
return helper.value
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(42), result.ReturnValue);
        Assert.Equal(1, host.ExistsCalls["/helper.py"]);
    }

    [Fact]
    public void LocalModuleAllowlist_DeniesUnreferencedScriptModulesBeforeHostLookup()
    {
        var host = new CountingHost();
        host.SeedFile("/other.py", "value = 1");

        var result = new LythonEngine().Run(
            "import other",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.False(result.Success);
        Assert.Equal("ModuleNotFoundError", result.Failure?.ExceptionType);
        Assert.Contains("No module named 'other'", result.Failure?.Message);
        Assert.Empty(host.ExistsCalls);
        Assert.Empty(host.ReadCalls);
    }

    [Fact]
    public void LocalModuleImports_AreResolvedRelativeToSourcePathDirectory()
    {
        var host = new CountingHost();
        host.SeedFile("/repo/tools/helper.py", "value = 42");
        host.SeedFile("/helper.py", "value = 1");

        var result = new LythonEngine().Run(
            """
import helper
return helper.value
""",
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/tools/main.py",
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper.py" }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(42), result.ReturnValue);
        Assert.Equal(1, host.ExistsCalls["/repo/tools/helper.py"]);
        Assert.Equal(1, host.ReadCalls["/repo/tools/helper.py"]);
        Assert.False(host.ExistsCalls.ContainsKey("/helper.py"));
        Assert.False(host.ReadCalls.ContainsKey("/helper.py"));
    }

    [Fact]
    public void ImportedModuleFailures_ReportModuleSourcePath()
    {
        var host = new CountingHost();
        host.SeedFile("/repo/tools/helper.py", """
def fail():
    raise RuntimeError("boom")

fail()
""");

        var result = new LythonEngine().Run(
            "import helper",
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/tools/main.py",
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper.py" }
            });

        Assert.False(result.Success);
        Assert.Equal("/repo/tools/helper.py", result.Failure?.SourcePath);
        Assert.NotNull(result.Failure);
        Assert.All(result.Failure.StackTrace, frame => Assert.Equal("/repo/tools/helper.py", frame.SourcePath));
    }

    [Fact]
    public void LocalModuleImports_CanBeDisabledWithoutDisablingBuiltinModules()
    {
        var host = new CountingHost();
        host.SeedFile("/helper.py", "value = 1");

        var builtin = new LythonEngine().Run(
            "import json\nreturn json.dumps({'ok': True})",
            host,
            new LythonRunOptions
            {
                DisableLocalModuleImports = true
            });

        Assert.True(builtin.Success, builtin.Failure?.Message);
        Assert.Equal("{\"ok\": true}", builtin.ReturnValue);

        var local = new LythonEngine().Run(
            "import helper",
            host,
            new LythonRunOptions
            {
                DisableLocalModuleImports = true
            });

        Assert.False(local.Success);
        Assert.Equal("ModuleNotFoundError", local.Failure?.ExceptionType);
        Assert.Contains("No module named 'helper'", local.Failure?.Message);
        Assert.Empty(host.ExistsCalls);
        Assert.Empty(host.ReadCalls);
    }

    [Fact]
    public void ImportFailures_KeepStablePythonShapedMessages()
    {
        var missingModule = new LythonEngine().Run("import missing", new MockLythonHost());
        Assert.False(missingModule.Success);
        Assert.Equal("ModuleNotFoundError", missingModule.Failure?.ExceptionType);
        Assert.Contains("No module named 'missing'", missingModule.Failure?.Message);

        var host = new CountingHost();
        host.SeedFile("/helper.py", "value = 1");
        var missingMember = new LythonEngine().Run(
            "from helper import missing",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });
        Assert.False(missingMember.Success);
        Assert.Equal("ImportError", missingMember.Failure?.ExceptionType);
        Assert.Contains("Cannot import name 'missing' from 'helper'", missingMember.Failure?.Message);
    }

    private sealed class CountingHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => true;

        private readonly MockLythonHost _inner = new();
        public Dictionary<string, int> ExistsCalls { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> ReadCalls { get; } = new(StringComparer.Ordinal);

        public string Cwd => _inner.Cwd;

        public DateTimeOffset LocalNow => _inner.LocalNow;

        public DateTimeOffset UtcNow => _inner.UtcNow;

        public void SeedFile(string path, string text) => _inner.SeedFile(path, text);

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
        {
            Count(ReadCalls, path);
            return _inner.ReadTextUtf8Async(path, cancellationToken);
        }

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.WriteTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            Count(ExistsCalls, path);
            return _inner.ExistsAsync(path, cancellationToken);
        }

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirAsync(path, cancellationToken);

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
            => _inner.MkDirAsync(path, cancellationToken);

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
            => _inner.RemoveAsync(path, cancellationToken);

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.CopyAsync(source, destination, cancellationToken);

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.MoveAsync(source, destination, cancellationToken);

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
            => _inner.StatAsync(path, cancellationToken);

        private static void Count(Dictionary<string, int> counts, string path)
        {
            counts[path] = counts.TryGetValue(path, out var count) ? count + 1 : 1;
        }
    }
}
