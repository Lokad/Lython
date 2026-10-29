# Builtin modules

The [module catalog](../src/Lokad.Lython/Runtime/LythonRuntime.BuiltinModuleCatalog.cs)
is an explicit allowlist. Importability means the contained API below, not every
CPython member. Unsupported operations fail explicitly. `AllowedBuiltinModules`
can narrow the inventory; dotted grants include registered ancestors. Host
capabilities and local-import authorization remain separate.

## Data and text

| Module | Supported use and boundaries |
| --- | --- |
| `json` | loads/dumps/load/dump, decoder/encoder objects, hooks, stream protocols, incremental encoding and selected custom classes. RunAsync awaits hooks, option truth/access/initialization and separator iteration, as well as stream I/O. Encoders use mutable attributes; decoders capture scanner options at construction. Separators borrow immutable UTF-8 storage and substantial output uses shared admission. Namedtuples encode as arrays; Decimal requires an explicit default hook (for example, `default=str`), as in Python. Sorted-key comparison and custom key hashing can require synchronous completion. Managed nesting admission remains active. |
| `csv` | reader/DictReader and writer/DictWriter, dialect options and quoted/multiline fields. Readers are single-pass iterators, not indexed collections. Excel defaults use CRLF; dictionary readers infer headers lazily and retain supplied field-name aliases. Dictionary writers use mapping `keys`/`get` protocols and reject extra keys with `ValueError`. RunAsync awaits iterator, header, option-truth, cell and mapping callbacks; custom key hashing still requires synchronous completion. A writer with no destination buffers text and exposes `getvalue()`; supplied write members are captured once. Dialect registration and Sniffer are unsupported. |
| `re` | Python pattern/match operations through an explicitly supplied provider; see [regex providers](RUNTIME.md#regex-providers). |
| `fnmatch`, `glob` | Native filename wildcard matching and mediated path enumeration; no regex provider required. |
| `shlex` | Shell-like splitting/quoting and lazy lexing of strings, native StringIO and authorized text streams. The lexer shares the exposed stream cursor; eof/source comparisons use Python protocols, including awaited callbacks. Automatic source inclusion is unsupported; push_source accepts already-authorized input. It does not execute a shell. |
| `difflib` | Sequence matching, unified/context/line diffs, Differ and byte diffs. SequenceMatcher borrows native indexed inputs and exposes its actual mutable indexes and cached block/opcode lists. Match uses ordinary namedtuples. RunAsync awaits matcher comparison/truth callbacks and construction/reindexing predicates. Unified/context diffs, restore and grouped opcodes return lazy iterators; ndiff, Differ and byte diffs still materialize eagerly. Producer and custom sequence/key protocols can require synchronous completion. HtmlDiff renders line-level changes; `wrapcolumn`/`charjunk` must be `None`. |
| `textwrap` | dedent/indent, including guest predicates; wrapping APIs are unsupported. |
| `html` | escape/unescape, including HTML5 entities; no HTML parser or DOM. |
| `urllib.parse` | URL split/parse, structured components/reassembly, quoting/unquoting and query helpers; no network client. |
| `xml.etree.ElementTree` | fromstring, parsed elements/attributes and namespace-aware child selection. Element construction, field/tree mutation and serialization are unsupported; attribute dictionaries remain mutable. Descendant/parent axes, predicates and namespace-wildcard selectors are unsupported. No external entity fetching. |
| `struct` | Standard binary layouts with explicit byte orders, pack/unpack/calcsize and lazy iter_unpack; native platform-layout assumptions are excluded. |

## Files and streams

| Module | Supported use and boundaries |
| --- | --- |
| `os`, `os.path`, `pathlib` | Lexical paths and mediated filesystem work, directory traversal, stat, text and optional binary I/O. Hosts authorize physical effects. |
| `shutil`, `filecmp` | Contained copy/move/removal/comparison workflows through the host. |
| `io` | StringIO, BytesIO, UnsupportedOperation and seek constants. Capacity and exported results retain their owners; close does not refund escaped aliases. |
| `zipfile` | Read/create/append, ordered duplicate listings, STORED/DEFLATED data with CRC checks, staged writes, one open member writer and contained extraction. Traversal, symlinks, passwords, other codecs, exclusive creation, random access and archive imports fail explicitly. |
| `zlib` | Zlib-wrapped byte compression/decompression with framing validation; the full native zlib API is not exposed. |
| `gzip` | open and GzipFile share sequential binary compression, lazy validation and execution-end cleanup; open text modes reuse file codecs/newline decoding. Flush publishes incremental data; close finishes the member. Caught write errors preserve earlier valid writes. Supplied stream ownership stays explicit. |
| `openpyxl` | Contained XLSX workbook automation, cells/styles, formulas as stored values, common tables/validation/comments/chart/image metadata and save/load workflows. Cells retain their own identity and metadata after deletion or overwrite; blank cells and appended rows keep their positions. `iter_rows`/`iter_cols` and `rows`/`columns`/`values` yield rows or columns on demand; subscripts and slices return tuples. RunAsync awaits append-source callbacks. Style IDs share serialization registration, including saved edits to loaded styles. It is not the full external Python package or a spreadsheet calculation engine. |

`open()` supplies mediated text and sequential binary handles. Text operations
honor supported encodings/newline policies. Host ranged-read and append defaults
can acquire/rewrite whole files; implement efficient overrides for streaming.
Write-open creates or truncates immediately after handle admission; later
cancellation does not restore prior contents. Append-open creates a missing file.
Flush and close append staged data and never repeat opening's truncation. Untouched
UTF-8-sig/UTF-16 writers emit no BOM. Readers check file identity/kind at open and
fetch/decode on consumption; `read(0)` and unread close perform no read. Read and
permission failures therefore surface when the host is actually asked for data.
Text read sizes use Python's index protocol, including suspended callbacks under
RunAsync. `read(None)` and `readlines(None)` mean unbounded; `readline(None)` rejects.
Encoded flush payloads remain admitted through host completion, then return their
private allowance. Returned text/bytes retain funding after file close.
Use [bounded host-driven batches](RUNTIME.md#streaming-and-approximation) for long
workloads rather than assuming arbitrary guest-reference reclamation.

`shutil.copyfileobj` drains text or binary streams until EOF, with bounded default
read requests or the supplied length. Supported read/write/member callbacks await
under RunAsync. Guest callbacks can retain chunks, which remain charged to their
existing owner. `filecmp.cmp` evaluates flag protocols after stat and compares
bounded native windows through host ranged reads; efficient host overrides remain
necessary. Its private working buffers are admitted and released at completion.

`Path`/`PosixPath` construct concrete POSIX paths; `PurePath`/`PurePosixPath`
construct lexical-only paths. Derived values preserve that kind, including
`type()` and `repr()`. Exactly two leading slashes are preserved lexically;
host operations still canonicalize and validate the contained namespace.
Public result projection represents paths as strings. Windows path flavors
remain explicitly unsupported.

`glob.iglob`, `Path.glob` and `Path.rglob` return lazy iterators using shared
component matching, including nested patterns, recursive `**`, directories and
the relevant hidden-file rules. `glob.glob` collects that iterator into a list.
`Path.iterdir` returns an iterator over a snapshot acquired at the call, as in
Python 3.13. Hosts supply a full direct-child list; this does not promise streaming
within one directory. Traversal uses funded explicit frames rather than CLR
recursion. Native preparation may remain charged until the execution batch ends;
active frame snapshots are relinquished on exhaustion or failure.

## Functions, collections and numbers

| Module | Supported use and boundaries |
| --- | --- |
| `builtins` | Core Python types, iteration, arithmetic, conversion, formatting, exceptions and mediated open/input/print. Unsupported builtins are explicit errors. |
| `collections`, `collections.abc` | Contained named tuples, defaultdict/Counter and collection protocol helpers. |
| `itertools`, `functools`, `operator`, `copy` | Supported iterator/combinator, function, operator-protocol and copy helpers with guest callback semantics. Copy memo edits remain visible; reduction registries and protocols are unsupported. Lazy adapters preserve acquisition/demand order; supported source, truth, equality and addition callbacks await real I/O under RunAsync. Function wrappers share ordinary descriptors and async forwarding, canonical cache/property metadata and a live readonly dispatch registry. |
| `dataclasses` | Decorators/runtime wrapping, field metadata, fields/asdict/astuple/replace/make_dataclass and ordinary inheritance. Layout-changing slots options are unsupported. |
| `typing` | Annotation metadata, common aliases and helpers; no runtime type/protocol enforcement. |
| `decimal`, `math`, `statistics` | Contained decimal, numeric and statistical helpers with Python-shaped errors. Decimal keeps finite CLR storage. Basic arithmetic, unary signs/abs, Counter arithmetic and fma use context precision/rounding and Inexact/Rounded signals. Nonfinite Decimal values and KDE helpers are unsupported. |
| `random` | Deterministic Lython module/Random state, common distributions and sampling/state helpers; sequences/state format differ from CPython. randrange/getrandbits/randbytes await conversion hooks in RunAsync. Native virtual ranges stay indexed for selections; custom guest populations are unsupported. SystemRandom has no supplied entropy capability and is unsupported. |
| `datetime`, `time` | Contained dates/times, host-controlled clocks and optional mediated timing/delay operations. |
| `hashlib` | md5/sha1/sha256/sha384/sha512 over supplied bytes, incremental update, digest/hexdigest and independent copies. State is disposed at execution end. file_digest is unsupported; read mediated binary streams in chunks and call update. No native module loading. |

DecimalTuple shares tuple equality, hashing, comparisons, methods, concatenation,
repetition and slicing. Its contained constructor validates sign/digits/exponent
and materializes digits into an immutable tuple, including supported asynchronous
iterables. This differs from CPython's unrestricted constructor, which retains
arbitrary supplied fields. `Decimal.as_tuple()` supplies the canonical shape.
Explicit Decimal rounding helpers use the supplied context's rounding mode.
`quantize` and `round(value, ndigits)` enforce that context's precision and
Inexact/Rounded signals, preserve the requested quantum and reject exponents
outside the supported -28..28 scale. `to_integral_exact` signals discarded digits;
`to_integral_value` and `to_integral` remain quiet. All integral variants ignore
precision and preserve signed zeros and nonnegative input exponents.
`round(value)` returns a half-even integer independently of the context.
`normalize` applies the context before removing trailing zeros. Selection methods
round the chosen value and resolve equal magnitudes by finite total ordering.
`compare` and `compare_signal` compare finite values exactly, without applying
arithmetic precision. `compare_total` also distinguishes signs and quantum.
`scaleb`, `remainder_near` and `logb` use the chosen context. Nearest remainders
select an exact half-even quotient before rounding the result; an oversized
quotient signals DivisionImpossible. Scaling avoids overflowing intermediate
CLR factors when the final value fits. Nonzero results must fit finite storage;
zero-exponent clamping outside the default context bounds is unsupported.
`shift` and `rotate` transform a coefficient of context precision width, preserve
sign/quantum and accept an optional context. Their counts follow Python's
one-width operand domain, and digit truncation does not signal rounding.
Counter unary minus filters negative source counts before applying Decimal
subtraction and its active context; unary plus retains the selected count aliases.
Contexts and mutable default templates are isolated per execution. `setcontext`
installs an ordinary context directly; installing a template copies it and clears
its flags. `localcontext` captures the parent and copies its child when creating
the manager, then restores the exact parent on exit. Copies own independent flag
and trap mappings; `clear_flags` updates the live mapping without breaking aliases.
Signal maps expose the nine standard keys with Boolean values. Assigning flags
or traps preserves the destination mapping's identity. Constructors and assignments
reject malformed maps; direct dictionary edits are validated on context use.
Custom signal keys and non-Boolean signal values are unsupported. Templates are
created on first use and retain their Python default trap values.
Context conversion applies the selected precision and rounding. Text and integer
sources must first fit CLR decimal exactly; unsupported source precision/scale
raises `InvalidOperation`. Unlike `Decimal`, `Context.create_decimal` rejects
surrounding whitespace with `InvalidOperation` carrying `ConversionSyntax`.
`create_decimal_from_float`
rounds the exact binary float and bypasses `FloatOperation`; implicit float
conversion sets that signal and honors its trap. Ordinary `Decimal(float)` keeps
CLR's finite conversion behavior. All results must fit CLR decimal's coefficient
and scale: precision 28 cannot represent every once-rounded float below one.
These representation failures are errors regardless of Decimal traps.
Exact native addition, subtraction and multiplication preserve their quantum
without ratio working storage when no digits were discarded and precision fits.
Rounding and uncertain native results use admitted exact ratios.
`sqrt` uses context precision, Inexact/Rounded signals and half-even rounding
independently of the context rounding setting, as Python requires. Exact roots
preserve quantum and zero sign. The once-rounded result must fit native scale.
`exp` supports zero, `ln` supports one, and `log10` supports positive powers of
ten, including half-even context rounding of their integer logarithms. Other
valid transcendental requests raise `NotImplementedError`. Logarithms of zero
require unsupported infinity without a DivisionByZero signal; negative domains
honor InvalidOperation. These boundaries do not imply full Decimal compatibility.
Floor division and remainder enforce quotient precision with `DivisionImpossible`;
remainder rounds once and uses context rounding/signals. Decimal `divmod` preserves
truncated quotients and selects compound zero-divisor errors from enabled traps.
Disabling a relevant trap permits finite results; operations requiring unsupported
NaN or Infinity results raise `NotImplementedError` and set their signal flags.
Integer Decimal powers compute admitted exact temporary ratios and round once;
large working sets may be refused even when the rounded result would be small.
Binary power/reduction loops observe cancellation between CLR operations.
Zero to a negative power requires unsupported infinity and fails explicitly
without inventing a DivisionByZero signal. Modular power preserves truncated
remainder signs and requires modulus digits to fit context precision.
Only the default `Emin=-999999`, `Emax=999999`, `capitals=1` and `clamp=0` settings
are supported. Valid nondefault values raise `NotImplementedError` during context
construction or assignment; invalid ranges retain `ValueError`. Accepted settings
do not extend CLR decimal's finite representation range.

`collections.namedtuple` validates names before consuming defaults. Excess
defaults retain their iterator effects and errors; the runtime drains the tail
without keeping it. Infinite defaults require cancellation. Namedtuple `_replace`
preserves the source and unchanged field aliases, admits replacement storage
before copying and rejects unknown fields with `TypeError` (Python 3.13).
Construction admits its value buffer before binding. `_make` drains its input
under admission/cancellation and checks exact field count before publishing;
arity and binding errors relinquish temporary storage. Defaults remain aliases.

Functional and class-based `typing.NamedTuple` use the same tuple representation
and helpers as `collections.namedtuple`. Their evaluated annotations form a live,
admitted dictionary shared with `__new__.__annotations__`; metadata reads preserve
aliases. Functional fields follow Python's two traversals, including exhaustion
of one-shot generators. RunAsync awaits field iteration and cleanup. Class syntax
supports annotated fields, optional trailing defaults and ClassVar exclusion;
additional bases, methods and other class-body members remain unsupported.

`typing.NewType` retains its supplied `__supertype__`, qualified name and caller
module. Factory `name`/`tp` keywords follow ordinary binding; the resulting
callable returns its single positional argument unchanged. Dotted names expose
their final component as `__name__`. Name storage and rendering use admission.
TypeVar and ParamSpec variance flags use ordinary truth protocols; RunAsync
awaits callbacks that perform host I/O. Lazy alias/bound evaluation remains lazy.

Functional `typing.TypedDict` accepts a dict of evaluated annotations (or keyword
fields) and boolean `total`. Class syntax supports a lone, non-generic TypedDict
base, annotations, docstrings and boolean `total`. Constructors produce ordinary
dicts and follow dict's mapping/pair-stream/keyword behavior; RunAsync awaits input
protocols. Annotations are one live admitted map. Missing keys and mismatched
values receive no runtime schema checks; isinstance, issubclass and class patterns
raise TypeError. Immutable required/optional key-set inspection, Required and
NotRequired, inheritance, additional class members and non-boolean total values
remain explicitly unsupported.

Legacy aliases such as `typing.List[T]` share generic argument storage and
substitution with builtin aliases while retaining their distinct equality and
display. Ordinary `get_args` reads reuse the funded immutable tuple; Annotated
and Callable expose their Python inspection shapes with admitted materialization.
Origins are stable. Union equality/hashing ignores argument order, including
legacy/PEP 604 comparisons; display and `get_args` preserve first-seen order.
Literal alternatives distinguish value types. Rendering uses admitted UTF-8
storage and managed nesting admission. These helpers remain annotation metadata,
without a general type checker or runtime enforcement of container element types.

String translation
tables use Unicode character positions, and byte tables use byte positions.

`dataclasses.field` returns the canonical Field later exposed by `fields` and
`__dataclass_fields__`; inherited fields retain identity. Dictionary metadata is
a live readonly view. Reflection helpers follow the public field map, while
generated method plans stay fixed at decoration. `asdict`/`astuple` recursively
convert containers and use deepcopy hooks for mutable leaves; result factories
apply at dataclass boundaries. These conversions use managed nesting admission
and shared storage funding. RunAsync awaits factories, post-init, replacement
constructors, supported getters and option truth hooks. General custom hashing
and class-member initialization callbacks can still require synchronous completion.

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

`pkgutil.iter_modules` and `walk_packages` return lazy iterators. Path inputs and
host operations are consumed on demand; a path must be an iterable, not a bare
string. Walking initializes contained packages, follows current `__path__`, and
applies `onerror` to package-initialization errors. Directory-operation errors
propagate. Per-directory sorting and deduplication use admitted working storage;
partially consumed iterators retain their funding until relinquished or their
execution owner ends. `ModuleInfo` uses the ordinary namedtuple implementation,
preserving raw fields and untouched `_replace` values.

Loader lookup uses live module/spec metadata. Resolution, loader source reads,
discovery and guest attribute/truth/callback operations can suspend under
`RunAsync`; cancellation and refused admission remain terminal. `resolve_name`
accepts `module:` to return the module itself. `extend_path` shallow-copies lists
and returns non-lists unchanged; it does not add ambient namespace paths.
Arbitrary finder/loader execution, namespace packages and unrestricted search
remain unsupported.

Subprocess captures and pipe operations return `bytes` by default; binary input
requires bytes. `text`, `universal_newlines`, `encoding` or `errors` select text
mode, which requires string input. UTF-8 and UTF-8-sig are supported, with the
selected error handler and universal output newlines. Conflicting explicit
text/universal-newlines truth values raise SubprocessError. Flags use Python
truth protocols; RunAsync awaits supported guest callbacks. `check` is evaluated
after command completion. Timeouts are numeric seconds (including fractions),
forwarded to the host; host timeout failures become TimeoutExpired. Negative
timeouts request immediate expiry. Nonfinite/out-of-range TimeSpan values reject.

`subprocess.Popen` is a deferred buffered facade. Construction stores a funded
request; wait, communicate, pipe reads or context exit execute it. Poll returns
`None` until completion is forced. Live-process progress/poll loops and retrying a
timed-out live process are outside this contract. Use run/helpers for ordinary
completed commands; the host owns actual execution and authorization.

`argparse` retains default/constant aliases and updates supplied namespaces as
arguments are consumed, including progress before errors. Append copies on use;
absent append values are `None`. String defaults convert when still applicable.
Choices use normal membership, including guest hooks and native range operations.
Namespace exposes a live `__dict__` through `vars`, ordinary deletion/equality and
type identity. `SUPPRESS` is a canonical string: only that object suppresses data;
ordinary strings with the same spelling remain values.
RunAsync awaits supported converters, FileType opening, flag truth, count addition,
namespace equality, argument iteration and output writers. Print methods accept
ordinary text-stream protocols, including StringIO and guest writers. Default parse
failures write mediated usage/error diagnostics before SystemExit; `exit_on_error=False`
preserves ArgumentError. Exit status is forwarded as the SystemExit payload.
Parser namespace fields stay dynamic; unproved field errors occur at runtime.
Known parser methods and call shapes still receive frontend checks.

`add_argument` returns its existing action metadata. Supported mutable fields are
`dest`, `default`, `type`, `required`, `choices`, `const`, `help` and `metavar`;
version actions also expose `version`. `nargs` is readable. Layout/option-string
edits, Action class hierarchies/subclassing and custom formatter factories are
unsupported. The four advertised builtin formatter choices keep a finite help
layout; unsupported factories fail explicitly when used. Nested help/repr leaf
rendering can still require synchronous callbacks.
See [runtime](RUNTIME.md) for grants and [compatibility](COMPATIBILITY.md) for
language behavior; public workflow/module tests are under
[`tests/Lokad.Lython.PublicApi.Tests`](../tests/Lokad.Lython.PublicApi.Tests).
