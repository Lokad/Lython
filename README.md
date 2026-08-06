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

with open("inventory.tsv") as handle:
    rows = csv.reader(handle.read().splitlines(), delimiter="\t")

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

with open("available.tsv", "w") as handle:
    handle.write(writer.getvalue())

with open("available.json", "w") as handle:
    handle.write(dumps(obj=selected))
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

Parenthesized, bracketed, and braced expressions may span physical lines using Python's implicit line joining, including inside compound-statement headers such as `if`, `for`, and `while`.

Tuple expression lists follow ordinary Python spelling in statement contexts: assignment values, `return`, expression statements, and `for` iterables may omit parentheses, including the one-item trailing-comma form.

Dataclasses include direct decorators, runtime `dataclasses.dataclass(cls)` wrapping, helper APIs such as `fields`, `asdict`, `astuple`, `replace`, and `make_dataclass`, and ordinary inheritance. Layout-changing options such as `slots=True` and `weakref_slot=True` are diagnosed as unsupported.

`typing` is compatibility-oriented: common aliases and helpers are importable, subscriptable, printable, and usable in annotations, but they do not enforce runtime types or protocol checks.

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
- `shutil`
- `statistics`
- `subprocess` when the host provides a subprocess capability
- `sys`
- `typing`

Local script imports are separate from builtin modules. Bare `import helper` can resolve through the host as `helper.py` only when `LythonRunOptions.AllowedLocalModules` contains `helper`, so embedders provide an explicit dependent-script list.

`pkgutil` follows the same contained model: it discovers builtins and explicitly allowed host-backed `.py` files or package directories, and it does not expose ambient importers or binary resource reads.

`datetime` covers the common `date`, `time`, `datetime`, `timedelta`, `timezone`, and `tzinfo` surface with CPython-shaped formatting and ISO parsing for fixed-offset timezones. Host-clock APIs such as `today()`, `now()`, `fromtimestamp()`, `timestamp()` for naive values, and `astimezone()` are mediated through `ILythonHost`; Lython does not expose an ambient IANA timezone database.

`statistics` covers common averages, medians, modes, variance, standard deviation, quantiles, covariance, correlation, linear regression, and `NormalDist`. Numeric summary helpers coerce supported real inputs, including `decimal.Decimal`, to `double` when needed; empty data, singleton sample statistics, invalid quantile parameters, degenerate correlation/regression, and zero-sigma distribution methods raise CPython-shaped errors. `NormalDist.samples(...)` is deterministic when no seed is supplied, and KDE helpers fail explicitly.

`random` is deterministic by design. It exposes module-level helpers and independent `Random` instances, state snapshot/restore, `randbytes`, `sample(..., counts=...)`, stricter `choices` validation, and common distribution helpers. `SystemRandom` remains explicitly unsupported unless a future host entropy abstraction is added.

`sys` is also contained: metadata, `path`, `modules`, builtin module names, `exc_info()`, `getsizeof(...)`, and std streams describe Lython and host-mediated handles rather than the host process or an ambient CPython installation.

`collections` covers the common agent-authored container helpers: `defaultdict`, `Counter`, `deque`, `namedtuple`, insertion-ordered `OrderedDict` as a dict-shaped alias, `ChainMap`, and inert `collections.abc` import names. `Counter` arithmetic follows positive-count CPython rules, bounded `deque(maxlen=...)` evicts consistently, and `UserDict`, `UserList`, and `UserString` fail explicitly.

`copy` supports `copy`, `deepcopy(x, memo=None)`, `copy.replace(obj, **changes)`, `Error`/`error`, and a compatibility `dispatch_table`. Deep copies preserve cycles and explicit memo dictionaries, `replace` works for dataclasses, namedtuple-like values, and `__replace__` hooks, and pickle-style reduce/state protocols fail explicitly unless a direct copy hook is provided.

`shutil` covers host-mediated file-copy and move workflows: `copyfile`, `copy`, `move`, text-only `copyfileobj`, `Error`, and `SameFileError`. Metadata-preserving `copy2`, symlink behavior, recursive tree helpers, archive helpers, permission helpers, and raw host inspection helpers remain outside the contained path model.

`functools` covers wrapper metadata helpers, `total_ordering`, `reduce`, `partial`, `partialmethod`, `cmp_to_key`, `lru_cache`, `cache`, `cached_property`, simple `singledispatch` and `singledispatchmethod` registration, and `recursive_repr`. Cache keys use Lython's hashable-value rules; `functools.Placeholder` is exposed only to fail explicitly because placeholder partial application is outside the supported subset.

`re` is Unicode text-only and backed by `Utf8Regex.PythonRe`. It exposes common module helpers, compiled patterns, lazy `finditer`, Python-shaped `Pattern` and `Match` metadata, named and optional group helpers, callable and template replacements, catchable `re.error`/`PatternError`, and the usual integer flags. `re.LOCALE` and `re.DEBUG` fail explicitly; bytes patterns and subjects remain outside the public bytes boundary.

`fnmatch` is deterministic and platform-independent. `fnmatch.fnmatch`, `fnmatch.fnmatchcase`, and `filter` use POSIX-like case-sensitive matching with `*`, `?`, bracket classes, negated classes, and ranges; `translate` returns an anchored regex string compatible with Lython `re`.

`json` covers text-only `load`/`dump` for Lython file handles, `loads`/`dumps`, catchable `JSONDecodeError` fields, parse/object hooks, formatting controls, key sorting/conversion, `skipkeys`, `default`, `allow_nan`, and circular-reference checks. `JSONEncoder` and `JSONDecoder` are exposed as explicit unsupported custom-class stubs.

`itertools` covers common lazy data-wrangling helpers: `chain`, `islice`, `product`, `zip_longest`, `count`, `repeat`, `cycle`, combinatorics, `accumulate`, selectors/predicates, `starmap`, `pairwise`, `groupby`, `tee`, and `batched`. Unbounded iterators remain lazy; functions that must cache inputs or buffers are still subject to Lython's execution limits.

`os` follows the same contained path and environment model. Path helpers use Lython's normalized POSIX-like `/` semantics; `os.environ`, `getenv`, `putenv`, `unsetenv`, `get_exec_path`, and `expandvars` read only the optional `LythonRunOptions.Environment` map and never the ambient process environment. Permission, symlink, raw file descriptor, process identity, signal, `chdir`, and shell helpers fail explicitly.

`decimal` is compatibility-oriented over .NET's fixed-precision `decimal`, not CPython's arbitrary-precision engine. Common `Decimal`, `DecimalTuple`, context, rounding constant, predicate, tuple-conversion, and integral-rounding APIs are available for agent-authored scripts. `NaN`, `sNaN`, `Infinity`, and precision beyond the .NET decimal range fail explicitly.

`math` tracks the common CPython 3.13 scalar and aggregate helpers used in generated scripts, including integer combinatorics, `dist`, variadic `hypot`, `frexp`/`ldexp`/`modf`, IEEE-adjacent helpers, `gamma`/`lgamma`, `fma`, and `sumprod`. Exact combinatorics are arbitrary-size where practical, but computations that imply unbounded local loops fail explicitly under Lython's contained execution model.

`operator` covers direct-function equivalents for supported unary, binary, comparison, item, sequence, in-place, and callable operations, including `itemgetter`, `attrgetter`, `methodcaller`, and `operator.call`. The helpers reuse Lython's existing expression and augmented-assignment semantics; `matmul` fails explicitly because Lython does not support the matrix-multiplication operator.

`glob` is host-mediated over the same contained path model. Module-level `glob.glob(...)` returns Python strings, `glob.iglob(...)` returns a one-shot iterator over materialized string results, and relative patterns return relative paths. `root_dir`, `recursive`, `include_hidden`, `escape`, `has_magic`, and `translate` are supported; `dir_fd`, `glob0`, and `glob1` fail explicitly.

`pathlib` uses Lython's normalized `/`-separated path model over host-mediated files and directories. `Path`, `PurePath`, `PurePosixPath`, and `PosixPath` share that model; Windows path classes fail explicitly. `Path.cwd()` uses the host cwd, `home()` and `expanduser()` stay unsupported, globbing APIs materialize lists eagerly, and file handles are UTF-8 text-only with explicit unsupported diagnostics for binary, symlink, permission, and random-access operations.

`argparse` covers ordinary agent-authored CLI scripts: `ArgumentParser`, `Namespace`, text-only `FileType`, common formatter classes and constants, `parse_args`, `parse_known_args`, defaults, help/error formatting, short and long options, `--name=value`, compact short flags, choices, required options, typed values, and the usual `store`, `append`, `store_const`, `store_true`, `store_false`, `count`, and `version` actions. Advanced parser composition features such as from-file expansion, parent parsers, subparsers, conflict handlers, and custom `Action` subclasses are rejected explicitly.

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
- CLI-style arguments, script origin, and a contained environment map can be passed through `LythonRunOptions`
- `print(...)` is captured deterministically through `LythonExecutionResult.StandardOutput`
- `stderr` is captured through `LythonExecutionResult.StandardError`
- unsuccessful results carry a process-compatible non-zero `ExitCode`; explicit
  `SystemExit` values retain their Python exit-code semantics
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
- `subprocess.CompletedProcess`, `subprocess.CalledProcessError`, and `subprocess.SubprocessError`
- `subprocess.PIPE`, `subprocess.STDOUT`, and `subprocess.DEVNULL`
- `subprocess.list2cmdline(...)`
- string and `pathlib.Path` command parts
- `shell=True` as an explicit host request, not ambient shell authority
- text-oriented captured I/O
- no `Popen`, `TimeoutExpired`, `getoutput`, or `getstatusoutput`
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
