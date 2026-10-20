# Lython

Lython is an embeddable Python runtime for .NET 10, implemented in managed C#.
It supports a broad Python 3.13 subset for text, CSV, JSON and file-processing
scripts. File, process and stream effects pass through an application-supplied
host; scripts have no ambient machine access.

```powershell
dotnet add package Lokad.Lython
```

```csharp
using Lokad.Lython;

// host implements ILythonHost; the application supplies its capabilities.
var engine = new LythonEngine();
var budget = new LythonMemoryBudget(16 * 1024 * 1024);
using var compiled = engine.Compile("""
    import json
    values = json.loads(payload)
    return sum(values)
    """, budget);
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
using var result = await compiled.RunAsync(host, new LythonRunOptions
{
    Globals = new Dictionary<string, object?> { ["payload"] = "[1,2,3]" },
    AllowedBuiltinModules = new HashSet<string> { "json" },
    DisableLocalModuleImports = true,
    CancellationToken = cancellation.Token,
});
if (result.Success) Console.WriteLine(result.ReturnValue); // 6
else Console.WriteLine(result.Failure?.Message);
```

Compiled programs can be reused. Dispose results after consuming their values,
and await all executions before disposing compiled programs or their owners.
Storage admission borrows the host budget; no budget means no implicit memory
cap. Cancellation is cooperative, and the host must arrange finite deadlines.
This managed runtime is an embedding boundary, not an OS isolation boundary.

Regular expressions require an explicitly supplied Python regex provider.
The core has no Utf8Regex dependency. An optional adapter is available, with
documented cancellation and compatibility limits.

## Documentation

These guides describe the current repository implementation. For a published
package, use the documentation from that package's source revision.

| Guide | Contents |
| --- | --- |
| [Compatibility](docs/COMPATIBILITY.md) | Grammar inventory, Python semantics and explicit limits |
| [Modules](docs/MODULES.md) | Builtin module surface and specific boundaries |
| [Runtime](docs/RUNTIME.md) | Embedding, results, host authority, cancellation and stack safety |
| [Memory](docs/MEMORY.md) | Shared admission, ownership, cleanup and bounded batches |
| [Regex](docs/REGEX.md) | Provider contract, UTF-8 semantics and adapter capabilities |
| [Benchmarks](docs/BENCHMARKS.md) | CPython comparisons, boundaries and uncertainty |
| [Testing](tests/README.md) | Focused regressions, platform checks and package qualification |

[Release notes](CHANGELOG.md) record API changes. Benchmark artifacts and
experimental history live under `benchmarks/`; plans and handoffs are outside
`docs/`.
