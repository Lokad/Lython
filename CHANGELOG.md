# Changelog

## Unreleased

This pending release closes the Studio compatibility findings gathered from coding-agent workloads while preserving Lython's explicit, host-mediated execution model.

### Python Language Compatibility

- Added unparenthesized tuple expression lists, collection-display unpacking, dotted imports with Python package binding, and implicit continuation throughout compound-statement headers.
- Added old-style string interpolation and the ordinary non-mutating and mutating set method families.
- Improved static flow analysis for short-circuit `None` guards and branch-local sequence length bounds, and made `Counter` report its number of stored keys through `len(...)`.
- Made `pathlib` construction, joining, and `absolute()` preserve lexical parent segments while `resolve()` and host-mediated operations continue to canonicalize them.

### Standard Library Surface

- Added context-correct `builtins`, contained `importlib` discovery, host-mediated `filecmp.cmp`, managed incremental `hashlib`, bounded in-memory and host-backed `gzip`, functional and iterable `shlex`, and host-clock-backed `time` support.
- Added Latin-1 codecs across the supported text APIs while continuing to route non-UTF-8 file bytes through the host's bounded binary capability.
- Added a lazy, bounded `subprocess.Popen` facade and in-memory pipeline composition without exposing live processes, PIDs, unmanaged pipes, signals, or ambient shell authority.

### Reliability And Embedding

- Made every unsuccessful execution carry a non-zero process-compatible exit status and aligned synchronous/asynchronous execution behavior with differential coverage.
- Reject asynchronous host effects before synchronous execution can perform preceding side effects.
- Hardened captured subprocess output, gzip buffers, incremental hash state, governed byte accumulation, pipe lifecycle, and exception-handler stack restoration.
- Made executable context managers invoke `__exit__` exactly once when an exception is not suppressed.
- Made zero-valued execution limits enforce real zero budgets, reject negative limits explicitly, and retain omitted limits as the only default/unlimited case.
- Removed avoidable buffered-output copies and quadratic work from text handles, sized Unicode reads, `functools` caches, gzip CRC processing, traceback collection, and large-integer base formatting.

### Engineering

- Tightened public state contracts, nullability invariants, host implementor documentation, and optional-parameter usage to follow the repository's C# guidelines.
- Removed friend-assembly access and unnecessary unsafe compilation, enabled warnings as errors, made benchmarks part of the solution, and normalized source formatting.
- Centralized invariants shared by synchronous and asynchronous execution while keeping their effect-awaiting loops explicit, removed the duplicate memory estimator, and cached stable builtin member bindings per execution.
- Replaced nullable/string-tagged call arguments and generic executable instruction payloads with explicit domain types, and unified static user-call binding across functions and methods.
- Canonicalized builtin module inventories and datetime operator contracts, and split oversized runtime, static-analysis, and lowering units by responsibility.
- Typed callable categories, dataclass options, and CSV argument layouts; shared lowered statement dispatch and `try` control flow across synchronous and asynchronous execution.
- Split the remaining responsibility-mixed parser, evaluator, OS, functools, difflib, itertools, statistics, random, JSON, and shlex implementation units without changing the pending package version.
- Introduced a black-box public-assembly smoke suite alongside the source-linked white-box suite, establishing the boundary that later compatibility scenarios can exercise without source linking.
- Removed unused runtime-specialization scaffolding and consolidated static value descriptions and text-boundary proofs so diagnostics cannot drift between analysis passes.
- Replaced loosely nullable abrupt-execution and subprocess policies with closed domain types, made worksheet range rewrites linear, and removed proportional allocations from common text slices and formatting.
- Shared lowered expression dispatch and formatted-string assembly, then partitioned the remaining module, member, collection, static-analysis, and OpenPyXL outlier files by responsibility.
- Preserved successful-path abstract facts through caught `try` bodies, centralized structural syntax traversal, and documented eager versus deferred expression boundaries.
- Replaced ambiguous iteration, abstract-value, cached-resolution, and workbook-save states with closed domain contracts; separated readable from mutable dynamic attributes.
- Centralized host-exception translation, localized incidental single-caller helpers, and split path-host dispatch and executable-frame interpretation into cohesive components.
- Added a maintainer architecture guide covering compilation, execution paths, containment invariants, runtime protocols, and verification boundaries.

## 0.7.0 - 2026-07-02

This release improves Lython's behavior as a non-surprising Python substitute for fresh coding agents within Lython's contained scope.

### Language And Parser Compatibility

- Added the scoped bytes value model needed by contained OpenXML workflows without adding binary file handles or unmanaged byte I/O.
- Added set comprehension support, including nested clauses, condition filters, async execution coverage, and static flow handling.
- Closed parser gaps around grouped multiline layouts, adjacent string literal concatenation, and common repairable syntax failures.

### Callable Compatibility

- Aligned high-value builtin keyword binding with Python spellings and common aliases.
- Tightened stdlib and method call shapes across commonly generated agent code, including CSV writer method signatures.

### Text IO Compatibility

- Normalized builtin `open(...)` and `Path.open(...)` text-option contracts for `mode`, `encoding`, `errors`, and `newline`.
- Made text handles behave like linear text streams: `read`, `readline`, `readlines`, and iteration now share one cursor.
- Made text handle `tell()` report deterministic UTF-8 byte positions and made `flush()` publish writable buffers without duplicating append writes.

## 0.6.0 - 2026-06-15

This release tightens Lython's Python-shaped scripting surface for fresh coding agents while preserving host-mediated containment.

### Python-Shaped Script Surface

- Removed Lython-specific filesystem globals from default script globals; scripts should use `open`, `pathlib`, `os`, and `shutil` instead.
- Added a contained `shutil` subset for common file-copy and move workflows, including `copyfile`, `copy`, `move`, `copyfileobj`, `SameFileError`, and `Error`.
- Kept `__file__` as conditional source-backed script/module state rather than a regular builtin.

### Builtin Compatibility

- Added numeric and text builtins including `abs`, `pow`, `round`, `bin`, `oct`, `hex`, `chr`, `ord`, `ascii`, `format`, `callable`, and `hash`.
- Added iterator and sequence helpers including `iter`, `reversed`, `map`, `filter`, and explicit `slice` objects.
- Added object inspection helpers `getattr`, `hasattr`, `setattr`, `delattr`, `dir`, and `vars` for supported Lython values.
- Expanded builtin exception names and catch behavior with Python-shaped categories such as `BaseException`, `ArithmeticError`, `LookupError`, `ModuleNotFoundError`, `RecursionError`, and `MemoryError`.

### Static Analysis And Documentation

- Synchronized runtime builtin registration, static builtin-name inventories, `sys` discovery, `SPEC.md`, and tests.
- Clarified agent guidance around Lython as a strict, extensive Python subset and disabled the .NET terminal logger by default through repository configuration.
- Made NuGet packaging explicit and Release-only so Debug build/test outputs cannot be mistaken for release artifacts.

## 0.5.0 - 2026-06-10

This release expands Python compatibility for coding-agent scratch scripts and workbook automation while preserving Lython's host-mediated containment model.

### OpenPyXL Compatibility

- Added built-in `openpyxl` compatibility for common workbook, worksheet, cell, style, comment, table, chart, drawing, validation, protection, and package-metadata workflows.
- Added fixture coverage for Excel-authored workbooks and openpyxl round trips, including formulas, shared strings, styles, relationships, worksheet XML, comments, tables, validations, conditional formatting, protection, VBA preservation, drawings, images, named styles, and structural metadata.
- Added static contracts and sealed member surfaces for the supported `openpyxl` import paths and objects.

### Python Language And Runtime Compatibility

- Expanded list compatibility with `index`, `count`, `insert`, `remove`, indexed `pop`, `reverse`, stable `sort`, repetition, augmented repetition, lexicographic sequence ordering, slice assignment, and slice deletion.
- Generalized assignment and augmented-assignment targets to support subscript, attribute, nested, slice, and openpyxl-style cell-value writes while preserving single-evaluation target behavior.
- Added `global` and `nonlocal` parsing, diagnostics, runtime binding, lowered execution, executable execution, closure-cell writes, and directive-aware static analysis.
- Locked down baseline helper behavior for generated scripts, including `sum(...)`, `subprocess.check_output(...)`, and `Path.read_text(...)` option handling.

### Standard Library Surface

- Expanded `csv` with `DictReader`, `DictWriter`, file-backed reader/writer support, multiline quoted records, common dialect options, quoting constants, and `csv.Error`.
- Expanded `difflib` with junk predicates, `Differ`, `HtmlDiff`, `diff_bytes`, richer `SequenceMatcher`, grouped opcodes, close-match scoring, and documented eager materialization where Lython deliberately differs from lazy generators.
- Expanded module and introspection helpers across `pkgutil`, `sys`, `argparse`, `dataclasses`, and `typing` for agent-generated scripts and static validation.
- Expanded contained filesystem and path helpers across `pathlib`, `os`, `os.path`, and `glob`, including path-like handling, host-mediated traversal, environment maps, cwd/stat/scandir-style values, hidden-file globbing, `root_dir`, `include_hidden`, and `glob.translate`.
- Expanded numeric, date/time, and data helpers across `decimal`, `math`, `datetime`, `statistics`, and `random`, including contexts, rounding modes, combinatorics, distributions, fixed-offset timezone behavior, grouped medians, quantiles, regression, deterministic `Random`, state round trips, and distribution helpers.
- Expanded `collections`, `itertools`, and `copy` with common containers, lazy data-wrangling helpers, explicit memo deep copies, `copy.replace`, hooks, cycle preservation, and compatibility aliases.
- Expanded `operator` with direct-function equivalents for supported unary, binary, comparison, item, sequence, in-place, and callable operations.
- Expanded `functools` with wrapper metadata helpers, caching decorators, `partialmethod`, `cached_property`, simple dispatch helpers, and recursive repr support.
- Expanded `re` with catchable regex errors, more flags, compiled-pattern and match metadata, group helpers, lazy `finditer`, range arguments, and richer replacement templates.
- Expanded `fnmatch` with `fnmatchcase`, `translate`, bracket character classes, negated classes, ranges, and deterministic case-sensitive matching.
- Expanded `json` with text-file `load`/`dump`, hooks, parse callbacks, formatting controls, `JSONDecodeError`, circular-reference checks, and explicit custom encoder/decoder diagnostics.
- Expanded `subprocess` with `CompletedProcess`, `CalledProcessError`, `SubprocessError`, `list2cmdline`, and checked-failure behavior shaped like CPython while keeping execution host-mediated.

### Host Surface And Containment

- Added `LythonRunOptions.Environment` so hosts can supply the contained environment used by `os.environ`, `os.getenv`, `os.putenv`, `os.unsetenv`, `os.get_exec_path`, and subprocess `env` handling without reading the ambient process environment.
- Added optional `ILythonHost.ReadBytesAsync(...)` and `ILythonHost.WriteBytesAsync(...)` hooks for host-mediated binary file workflows; the default interface implementations keep binary I/O unavailable unless a host opts in.

### Static Analysis And Documentation

- Expanded static contracts, return-shape flow, host-requirement checks, callable/member surfaces, and diagnostics across the new language and module surfaces.
- Updated README and SPEC coverage for the supported Python-shaped subsets and the explicit containment boundaries for filesystem, environment, and subprocess access.

## 0.4.1 - 2026-06-05

- Added `LythonSubprocessCompletion.CompleteBufferedAsync(...)`, a public host-side helper for applying subprocess stream modes to buffered stdout and stderr.
- Covered `PIPE`, inherited streams, `DEVNULL`, `STDOUT` redirection, and output-bound handling for host subprocess runners.

## 0.4.0 - 2026-06-04

This release broadens Lython's Python compatibility for coding-agent scratch scripts and text-editing workflows while preserving the host-mediated containment model.

### Python Language Compatibility

- Added multiline list, tuple, set, dictionary, call, and subscript syntax support for common Python formatting styles.
- Expanded f-string support, including format specifiers, conversions, debug expressions, raw/triple-quoted variants, and nested expressions.
- Added one-line compound suites for `if`, loops, `try`, `with`, `match`, class, and function definitions.
- Filled in common builtins used by scratch scripts, including practical `iter`, `next`, `sum`, `min`, `max`, `abs`, `round`, `enumerate`, `zip`, `reversed`, `all`, and `any` behavior.

### Text Editing And File Workflows

- Made file handles first-class for supported text I/O, including `close()`, closed-state checks, iteration, and context-manager behavior.
- Added richer path and filesystem compatibility across `pathlib` and `os`, including path-like arguments, path properties, globbing, stat/scandir-style objects, rename/replace/remove helpers, and directory creation variants.
- Added `sys.exit(...)` support with structured exit results.

### Standard Library Surface

- Added `difflib` support for unified/context diffs, ndiff/restore, close matches, and `SequenceMatcher`.
- Added `pkgutil` module discovery helpers for the supported import model.
- Expanded `datetime` compatibility for date/time constructors, parsing, formatting, arithmetic, comparisons, and timedeltas.
- Improved `re.findall(...)` list compatibility.

### Host-Mediated Subprocesses

- Broadened the optional `subprocess` module beyond `run(...)` with `call(...)`, `check_call(...)`, and `check_output(...)`.
- Added `subprocess.PIPE`, `STDOUT`, and `DEVNULL`, plus host request fields for stdin/stdout/stderr modes, `shell`, text mode, encoding, errors, environment, timeout, and output bounds.
- Added `CompletedProcess.args` and `CompletedProcess.check_returncode()`.
- Kept process execution explicit and host-mediated; embedders still decide whether subprocesses exist and how requests are authorized.

### Static Analysis And Packaging

- Expanded static contracts for the new Python, filesystem, datetime, module, and subprocess surfaces.
- Added package release notes metadata and packaged this changelog with the NuGet artifact.
