using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinModulePolicyTests
{
    public static IEnumerable<object[]> DenialRoutes()
    {
        string[] routes =
        [
            "import json",
            "from json import loads",
            "import importlib\nimportlib.import_module('json')",
            "import os\nvalue = os.path",
            "from os import path",
            "from os import *",
            "import xml\nvalue = xml.etree",
            "import openpyxl\nvalue = openpyxl.styles",
            "import collections\nvalue = getattr(collections, 'abc')",
            "import importlib\nvalue = importlib.util",
            "import pkgutil\nvalue = pkgutil.resolve_name('os:path')",
            "import pkgutil\nvalue = pkgutil.resolve_name('xml:etree')",
        ];
        foreach (var route in routes)
        foreach (var asynchronous in new[] { false, true })
            yield return [route, asynchronous];
    }

    [Theory]
    [MemberData(nameof(DenialRoutes))]
    public async Task DeniedRoutesFailBeforeLocalFallback(string source, bool asynchronous)
    {
        var host = new MockLythonHost();
        host.SeedFile("/json.py", "raise AssertionError('shadow executed')");
        var options = Options("importlib", "pkgutil", "os", "xml", "openpyxl", "collections");
        var result = asynchronous
            ? await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(source, host, options)
            : new LythonEngine(Utf8RegexProvider.Instance).Run(source, host, options);
        Assert.False(result.Success);
        Assert.Equal("ImportError", result.Failure?.ExceptionType);
        Assert.Contains("AllowedBuiltinModules", result.Failure?.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoveryAndRegistryDoNotRecoverDeniedModules(bool asynchronous)
    {
        var source = """
import sys, importlib, importlib.util, pkgutil, os
assert 'json' not in sys.builtin_module_names
assert 'json' not in sys.stdlib_module_names
assert 'json' not in sys.modules
assert 'os.path' not in sys.modules
assert 'path' not in vars(os)
assert importlib.util.find_spec('json') is None
assert importlib.util.find_spec('json.child') is None
assert pkgutil.find_loader('json') is None
assert pkgutil.get_loader('json') is None
assert 'json' not in [m.name for m in pkgutil.iter_modules()]
assert 'os.path' not in [m.name for m in pkgutil.walk_packages()]
sys.modules['json'] = os
try:
    importlib.import_module('json')
except ImportError:
    return 7
return -1
""";
        var options = Options("sys", "importlib", "importlib.util", "pkgutil", "os");
        var result = asynchronous
            ? await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(source, new MockLythonHost(), options)
            : new LythonEngine(Utf8RegexProvider.Instance).Run(source, new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(new BigInteger(7), result.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrantedHierarchyAndCacheKeepIdentity(bool asynchronous)
    {
        var source = """
import os.path, sys, importlib
from os import path
assert path is os.path
assert path is sys.modules['os.path']
assert path is importlib.import_module('os.path')
import os.path as again
assert again is path
return path.basename('/a/b.txt')
""";
        var options = Options("os", "os.path", "sys", "importlib");
        var result = asynchronous
            ? await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(source, new MockLythonHost(), options)
            : new LythonEngine(Utf8RegexProvider.Instance).Run(source, new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("b.txt", result.ReturnValue);
    }

    [Fact]
    public void EmptyPolicyKeepsIntrinsicsAndFutureDirectives()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        var result = engine.Run("from __future__ import annotations\nreturn sum(range(5))", new MockLythonHost(), Options());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(10), result.ReturnValue);
        Assert.Equal("ImportError", engine.Run("import __future__", new MockLythonHost(), Options()).Failure?.ExceptionType);
        Assert.Equal("ImportError", engine.Run("import builtins", new MockLythonHost(), Options()).Failure?.ExceptionType);
        Assert.False(engine.Run("__import__('json')", new MockLythonHost(), Options()).Success);
        Assert.True(engine.Run("import json\nreturn json.loads('7')", new MockLythonHost()).Success);
    }

    [Theory]
    [InlineData("os.path")]
    [InlineData("Json")]
    [InlineData("json.")]
    [InlineData("unsupported")]
    [InlineData(null)]
    public async Task InvalidGrantsAreRejectedBeforeGuestCode(string? grant)
    {
        var options = Options(grant!);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("raise AssertionError('executed')");
        Assert.Throws<ArgumentException>(() => script.Run(new MockLythonHost(), options));
        await Assert.ThrowsAsync<ArgumentException>(() => script.RunAsync(new MockLythonHost(), options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapabilityAndGrantAreBothRequired(bool capable)
    {
        var host = new MockLythonHost();
        if (capable) host.EnableSubprocess();
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        const string source = "import importlib\nreturn importlib.import_module('subprocess').__name__";
        Assert.Equal("ImportError", engine.Run(source, host, Options("importlib")).Failure?.ExceptionType);
        var result = await engine.RunAsync(source, host, Options("importlib", "subprocess"));
        Assert.Equal(capable, result.Success);
        if (capable) Assert.Equal("subprocess", result.ReturnValue);
        Assert.Empty(host.SubprocessRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalCodeObeysBuiltinPolicy(bool asynchronous)
    {
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "import json\nvalue = 7");
        var options = new LythonRunOptions
        {
            AllowedBuiltinModules = new HashSet<string>(),
            AllowedLocalModules = new HashSet<string> { "helper" },
        };
        var result = asynchronous
            ? await new LythonEngine(Utf8RegexProvider.Instance).RunAsync("import helper\nreturn helper.value", host, options)
            : new LythonEngine(Utf8RegexProvider.Instance).Run("import helper\nreturn helper.value", host, options);
        Assert.Equal("ImportError", result.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CompiledScriptAndSnapshotsStayIsolatedAcrossSuspendedRuns()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import time\ntime.sleep(1)\nimport math\nreturn math.sqrt(value)");
        using var cancellation = new CancellationTokenSource();
        var timings = new[] { new HeldTiming(), new HeldTiming() };
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "time", "math" };
        var denied = new HashSet<string> { "time" };
        var calls = new List<Task<LythonExecutionResult>>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var host = new MockLythonHost();
                host.SetTiming(timings[i]);
                calls.Add(script.RunAsync(host, new LythonRunOptions
                {
                    AllowedBuiltinModules = i == 0 ? allowed : denied,
                    Globals = new Dictionary<string, object?> { ["value"] = i == 0 ? 49 : 81 },
                }, cancellation.Token));
            }
            await Task.WhenAll(timings.Select(t => t.Started.Task)).WaitAsync(TimeSpan.FromSeconds(5));
            allowed.Remove("math");
            denied.Add("math");
            Assert.All(calls, call => Assert.False(call.IsCompleted));
        }
        finally
        {
            foreach (var timing in timings) timing.Release.TrySetResult();
        }
        var results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(results[0].Success, results[0].Failure?.Message);
        Assert.Equal(7d, results[0].ReturnValue);
        Assert.Equal("ImportError", results[1].Failure?.ExceptionType);
        var repeatedHost = new MockLythonHost();
        var completedTiming = new HeldTiming();
        completedTiming.Release.SetResult();
        repeatedHost.SetTiming(completedTiming);
        var repeated = script.Run(repeatedHost, new LythonRunOptions
        {
            AllowedBuiltinModules = new HashSet<string> { "time", "math" },
            Globals = new Dictionary<string, object?> { ["value"] = 100 },
        });
        Assert.True(repeated.Success, repeated.Failure?.Message);
        Assert.Equal(10d, repeated.ReturnValue);
    }

    private static LythonRunOptions Options(params string[] grants) => new()
    {
        AllowedBuiltinModules = new HashSet<string>(grants, StringComparer.Ordinal),
        AllowedLocalModules = new HashSet<string> { "json", "json.child" },
    };

    private sealed class HeldTiming : ILythonTiming, ILythonSynchronousHostCapability
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public long MonotonicNanoseconds => 0;
        public bool CompletesSynchronously => Release.Task.IsCompleted;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
