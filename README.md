# Lython

Lython is an embeddable, contained-by-design Python runtime implemented in managed C# on .NET. It compiles a supported Python subset through a handwritten front-end and executes it through a pure managed interpreter, with file and path effects mediated by an async host-provided interface.

```powershell
dotnet add package Lokad.Lython
```

Release notes are tracked in [CHANGELOG.md](CHANGELOG.md).

It is meant for the kind of Python a coding agent naturally writes when it needs to:

- read and rewrite text files
- scan directories
- reshape CSV or TSV data
- apply regex-based edits
- produce derived output files

The runtime is intentionally host-mediated. Scripts do not get ambient access to the local machine. File and path effects go through `ILythonHost`, and the supported module surface stays small and explicit.

## Representative Script

This is the sort of concise Python Lython is meant to run:

```python
import csv
import re
from json import dumps

rows = csv.reader(read_text("inventory.tsv").splitlines(), delimiter="\t")

def clean_name(text, *, pattern=r"\s+"):
    return re.sub(pattern=pattern, repl=" ", string=text.strip())

selected = []
for sku, name, qty in rows[1:]:
    if (count := int(qty)) > 0:
        selected.append({"sku": sku, "name": clean_name(name), "qty": count})
else:
    selected = sorted(selected, key=lambda item: item["sku"])

writer = csv.writer(delimiter="\t")
writer.writerow(["sku", "name", "qty"])
writer.writerows([[item["sku"], item["name"], item["qty"]] for item in selected])

write_text("available.tsv", writer.getvalue())
write_text("available.json", dumps(obj=selected))
```

On the host side, the plumbing is deliberately small:

```csharp
var engine = new LythonEngine();
var result = engine.Run(script, host);

if (!result.Success)
{
    Console.WriteLine($"{result.Failure!.ExceptionType}: {result.Failure.Message}");
}
```

The example elides the host implementation on purpose. In practice, `host` is your controlled bridge to files and directories through `ILythonHost`.

## Python Surface

Lython supports a broad, practical subset of Python. Ordinary control flow, functions, exceptions, collections, comprehensions, strings, regex, classes and dataclasses, structural pattern matching, and host-mediated file/path work are expected to work.

The builtin module surface is explicitly allowlisted:

- `argparse`
- `collections`
- `copy`
- `csv`
- `dataclasses`
- `datetime`
- `decimal`
- `difflib`
- `fnmatch`
- `functools`
- `glob`
- `itertools`
- `json`
- `math`
- `operator`
- `openpyxl` for contained `.xlsx` workbook automation
- `os`
- `pathlib`
- `pkgutil`
- `random`
- `re`
- `statistics`
- `subprocess` when the host provides a subprocess capability
- `sys`

Local script imports are separate from builtin modules. Bare `import helper` can resolve through the host as `helper.py` only when `LythonRunOptions.AllowedLocalModules` contains `helper`, so embedders provide an explicit dependent-script list.

`pkgutil` follows the same contained model: it discovers builtins and explicitly allowed host-backed `.py` files or package directories, and it does not expose ambient importers or binary resource reads.

Intentionally unsupported or constrained:

- arbitrary package loading
- unrestricted imports from disk
- sockets and HTTP
- broad shell/process authority beyond the host-mediated `subprocess` surface
- `yield` and async/await
- parts of Python metaprogramming and object-model edge behavior outside the contained runtime model
- a full general-purpose Python standard library

When a construct is outside the supported subset, Lython fails explicitly rather than silently drift away from Python semantics.

## Static Analysis

`Compile(...)` runs an error-only static analyzer before a script can execute. The analyzer is conservative: it rejects provable mistakes, but does not try to be a complete Python type checker.

It is aimed at the failures coding agents are most likely to introduce in small automation scripts: unsupported imports, bad call shapes, wrong statically-known argument types for supported built-ins and modules, sealed member typos, dataclass and argparse shape mistakes, regex match/group misuse, and exact literal dictionary key misses. Unknown or data-dependent cases are left to runtime instead of guessed.

## Public API

The main entry point is [`LythonEngine`](src/Lokad.Lython/Public/LythonEngine.cs). The public surface is intentionally small:

- `Compile(...)` returns a `LythonCompiledScript` and performs no host effects
- `Run(...)` and `RunAsync(...)` return a `LythonExecutionResult`
- failures are reported as structured `LythonRuntimeFailure`
- file and path effects are mediated through `ILythonHost`
- CLI-style arguments and script origin can be passed through `LythonRunOptions`
- `print(...)` is captured deterministically through `LythonExecutionResult.StandardOutput`
- `stderr` is captured through `LythonExecutionResult.StandardError`
- projected return values stay CLR-friendly, including `byte[]` for Python bytes values
- execution limits are configured through `LythonRunOptions`

The important runtime guarantees are:

- cooperative interruption through `CancellationToken`, with frequent checks across interpreter execution
- an explicit execution-step budget to stop runaway pure-Python loops
- a recursion limit plus an internal interpreter-stack guard so Python recursion cannot turn into CLR stack overflow
- conservative in-process size controls for strings, collections, host calls, and execution-memory growth

Unless `DisableDefaultLimits` is set, Lython applies practical defaults, including a 1 GiB execution-memory budget and a separate 1 GiB projection budget. Those guarantees are meant to make Lython safe to embed inside a host process, while keeping the programming model close to ordinary small Python.

## Host Integration

`ILythonHost` is the authority boundary of the runtime. Core filesystem and clock operations stay small:

- current working directory and wall-clock access
- UTF-8 text reads/writes/appends and binary reads/writes
- existence, stat, directory listing, mkdir, remove, copy, and move

All host effects are async and receive the run cancellation token. That base surface is enough for the built-in text/file/path workflows. The richer host-mediated features are optional and exposed through default interface members:

- `StandardInput`, `StandardOutput`, and `StandardError`
- `WalkAsync(...)` for `os.walk`
- `SubprocessRunner` for the host-mediated `subprocess` module surface

The stream capability is deliberately text-shaped:

- [`ILythonTextInput`](src/Lokad.Lython/Host/ILythonTextInput.cs) exposes `ReadToEndUtf8Async(...)` and `ReadLineUtf8Async(...)`
- [`ILythonTextOutput`](src/Lokad.Lython/Host/ILythonTextOutput.cs) exposes `WriteUtf8Async(...)` and `FlushAsync(...)`

When those are provided:

- `sys.stdin`, `sys.stdout`, and `sys.stderr` exist
- `input()` reads from host stdin
- `print()` writes to `sys.stdout`
- `print(..., file=sys.stderr)` works naturally

Process execution is also optional and host-mediated. [`ILythonSubprocessRunner`](src/Lokad.Lython/Host/ILythonSubprocessRunner.cs) receives a [`LythonSubprocessRequest`](src/Lokad.Lython/Host/LythonSubprocessRequest.cs) with:

- argv as `IReadOnlyList<string>`
- optional cwd
- optional environment
- UTF-8 stdin bytes
- stdin/stdout/stderr stream modes
- shell/text-mode flags
- optional encoding and error-mode requests
- optional timeout
- optional max-output bound

and returns a [`LythonSubprocessResult`](src/Lokad.Lython/Host/LythonSubprocessResult.cs) with return code plus captured UTF-8 stdout/stderr.

The subprocess contract remains host-mediated:

- `subprocess.run(...)`, `subprocess.call(...)`, `subprocess.check_call(...)`, and `subprocess.check_output(...)`
- `subprocess.PIPE`, `subprocess.STDOUT`, and `subprocess.DEVNULL`
- string and `pathlib.Path` command parts
- `shell=True` as an explicit host request, not ambient shell authority
- text-oriented captured I/O
- no `Popen`
- no background or async process model

This keeps Lython pipe-friendly without giving scripts ambient process authority. The embedding host decides whether subprocesses are available at all, and under what policy.

`import openpyxl` provides a vanilla-Python-shaped subset for common `.xlsx`
workbook automation. Workbook load/save uses host-mediated binary file
operations and does not add workbook-specific package dependencies.

## Safety Guarantees

Lython is designed to fail inside the runtime rather than by escaping into unmanaged process behavior in the normal supported envelope.

- file and path effects are host-mediated through `ILythonHost`; scripts do not get ambient machine authority
- an error-only static analyzer rejects many provable incompatibilities and structural mistakes before execution starts, instead of failing halfway through the script
- cancellation is cooperative and checked frequently enough to stop runaway pure-Python work
- execution steps, recursion depth, interpreter depth, host calls, string lengths, and collection sizes can all be bounded explicitly
- the main script-owned allocators and large `BigInteger` growth paths are governed by an execution-memory budget, so large runtime materializations fail as ordinary `RuntimeError` results
- host-facing projection is governed separately through an optional projection budget, and projection overflow surfaces distinctly as `ProjectionError`

The model is intentionally conservative, not omniscient. Lython does not try to mirror the CLR heap perfectly, but it does aim to keep the main script-owned growth paths inside explicit runtime control.

## Dependencies

- `Lokad.Parsing` for tokenization and the handwritten front-end infrastructure
- `Lokad.Utf8Regex` for UTF-8-native regex execution primitives
- `Lokad.Utf8Regex.PythonRe` for Python-shaped regular-expression semantics on top of UTF-8 regex execution
