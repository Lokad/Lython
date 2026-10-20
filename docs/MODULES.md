# Builtin modules

The [module catalog](../src/Lokad.Lython/Runtime/LythonRuntime.BuiltinModuleCatalog.cs)
is an explicit allowlist. Importability means the contained API below, not every
CPython member. Unsupported operations fail explicitly. `AllowedBuiltinModules`
can narrow the inventory; dotted grants include registered ancestors. Host
capabilities and local-import authorization remain separate.

## Data and text

| Module | Supported use and boundaries |
| --- | --- |
| `json` | loads/dumps/load/dump, decoder/encoder objects, hooks, stream protocols, incremental encoding and selected custom classes. Guest callbacks and real stream I/O are awaited by RunAsync. |
| `csv` | reader/DictReader and writer/DictWriter, dialect options and quoted/multiline fields. Readers are single-pass iterators, not indexed collections. A writer with no destination buffers text and exposes `getvalue()`; supplied write members are captured once. |
| `re` | Python pattern/match operations through an explicitly supplied provider; see [regex](REGEX.md). |
| `fnmatch`, `glob` | Native filename wildcard matching and mediated path enumeration; no regex provider required. |
| `shlex` | Contained shell-like splitting/quoting; it does not execute a shell. |
| `difflib` | Sequence matching, unified/context/line diffs, Differ/HtmlDiff and byte diffs. Some producer APIs materialize results eagerly. |
| `textwrap` | dedent/indent, including guest predicates; wrapping APIs are unsupported. |
| `html` | escape/unescape, including HTML5 entities; no HTML parser or DOM. |
| `urllib.parse` | URL split/parse, structured components/reassembly, quoting/unquoting and query helpers; no network client. |
| `xml.etree.ElementTree` | Contained parsing/serialization, elements, namespaces and child selection. Descendant/parent axes, predicates and namespace-wildcard selectors are unsupported. No external entity fetching. |
| `struct` | Standard binary layouts with explicit byte orders, pack/unpack/calcsize and lazy iter_unpack; native platform-layout assumptions are excluded. |

## Files and streams

| Module | Supported use and boundaries |
| --- | --- |
| `os`, `os.path`, `pathlib` | Lexical paths and mediated filesystem work, directory traversal, stat, text and optional binary I/O. Hosts authorize physical effects. |
| `shutil`, `filecmp` | Contained copy/move/removal/comparison workflows through the host. |
| `io` | StringIO, BytesIO, UnsupportedOperation and seek constants. Capacity and exported results retain their owners; close does not refund escaped aliases. |
| `zipfile` | Read/create/append, ordered duplicate listings, STORED/DEFLATED data with CRC checks, staged writes, one open member writer and contained extraction. Traversal, symlinks, passwords, other codecs, exclusive creation, random access and archive imports fail explicitly. |
| `zlib` | Zlib-wrapped byte compression/decompression with framing validation; the full native zlib API is not exposed. |
| `gzip` | Sequential GzipFile binary wrappers, lazy header/trailer validation and mediated filename publication. Supplied stream ownership and native cleanup are explicit. |
| `openpyxl` | Contained XLSX workbook automation, cells/styles, formulas as stored values, common tables/validation/comments/chart/image metadata and save/load workflows. It is not the full external Python package or a spreadsheet calculation engine. |

`open()` supplies mediated text and sequential binary handles. Text operations
honor supported encodings/newline policies. Host ranged-read and append defaults
can acquire/rewrite whole files; implement efficient overrides for streaming.
Use [bounded host-driven batches](MEMORY.md#streaming-and-approximation) for long
workloads rather than assuming arbitrary guest-reference reclamation.

## Functions, collections and numbers

| Module | Supported use and boundaries |
| --- | --- |
| `builtins` | Core Python types, iteration, arithmetic, conversion, formatting, exceptions and mediated open/input/print. Unsupported builtins are explicit errors. |
| `collections`, `collections.abc` | Contained named tuples, defaultdict/Counter and collection protocol helpers. |
| `itertools`, `functools`, `operator`, `copy` | Supported iterator/combinator, function, operator-protocol and copy helpers with guest callback semantics. |
| `dataclasses` | Decorators/runtime wrapping, field metadata, fields/asdict/astuple/replace/make_dataclass and ordinary inheritance. Layout-changing slots options are unsupported. |
| `typing` | Annotation metadata, common aliases and helpers; no runtime type/protocol enforcement. |
| `decimal`, `math`, `statistics` | Contained decimal, numeric and statistical helpers with Python-shaped errors. KDE helpers are unsupported. |
| `random` | Deterministic module/Random state, common distributions and sampling/state helpers. SystemRandom has no supplied entropy capability and is unsupported. |
| `datetime`, `time` | Contained dates/times, host-controlled clocks and optional mediated timing/delay operations. |
| `hashlib` | Contained hashing over supplied values/files; no native module loading. |

## Runtime and processes

| Module | Supported use and boundaries |
| --- | --- |
| `sys` | Explicit argv/environment-related runtime metadata, mediated streams and supported exception/runtime helpers. Recursion depth is configured by the host, not sys.setrecursionlimit. |
| `argparse` | Contained command-line parsing for supplied arguments. |
| `importlib`, `importlib.util`, `pkgutil` | Discovery/import helpers constrained by the builtin catalog and host-local module allowlist; no unrestricted path search. |
| `subprocess` | Available only with a host subprocess runner. The host authorizes execution and owns the process boundary; no ambient shell access. |
| `__future__` | Supported future directives, including postponed annotations. |

Container namespaces (`urllib`, `xml`, `xml.etree`) and registered submodules
provide Python-shaped dotted imports. Local modules use the same executable
engine, canonical globals, provider, token and owners as the main script.
See [runtime](RUNTIME.md) for grants and [compatibility](COMPATIBILITY.md) for
language behavior; public workflow/module tests are under
[`tests/Lokad.Lython.PublicApi.Tests`](../tests/Lokad.Lython.PublicApi.Tests).
