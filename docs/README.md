# Lython documentation

These guides describe the current repository implementation. A published package
may precede it; use the documentation from that package's source revision.

| Guide | Contents |
| --- | --- |
| [Compatibility](COMPATIBILITY.md) | Python semantics, extensions and explicit limits |
| [Syntax](SYNTAX.md) | Grammar inventory and regression corpus |
| [Modules](MODULES.md) | Builtin module surface and specific boundaries |
| [Example](EXAMPLES.md) | Executable CSV/text/JSON file-processing recipe |
| [Runtime](RUNTIME.md) | Embedding, results, host authority, cancellation and stack safety |
| [Memory](MEMORY.md) | Shared admission, ownership, cleanup and bounded batches |
| [Regex](REGEX.md) | Provider contract, UTF-8 semantics and adapter capabilities |
| [Benchmarks](BENCHMARKS.md) | CPython comparisons, boundaries and uncertainty |
| [Architecture](ARCHITECTURE.md) | Frontend, executable engine, values and ownership |
| [Testing](TESTING.md) | Focused regressions, platform checks and package qualification |

Keep this directory concise and about supported behavior. Put plans, handoffs,
delivery receipts, optimization narratives and rejected designs elsewhere.
Measurement snapshots retain their actual revision and machine; they must not
be presented as measurements of current HEAD.
