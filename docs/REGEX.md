# Python regex providers

Core owns a Python `re` contract and has no Utf8Regex dependency/backend types.
Supply a borrowed [`ILythonRegexProvider`](../src/Lokad.Lython/Host/ILythonRegexProvider.cs)
to `LythonEngine`. One provider handles constant validation/capture analysis,
runtime operations and imports. It must outlive its scripts and executions.
Missing functionality reports an explicit capability error, without another dialect.

## Contract

Providers receive borrowed UTF-8 pattern/subject data and a shared memory owner.
Pattern metadata is immutable and concurrent-safe: effective Python flag bits
(including leading inline flags and implicit Unicode), capture slots including
zero, and named-group indexes. Frontend analysis uses those same effective flags.

Prepared subjects implement match/search/fullmatch, Python finditer progression,
string-template substitution and capture-preserving split. Captures expose UTF-8
values, byte ranges and Unicode-scalar character offsets. Replacement validation,
unmatched groups, empty-match progression and Python errors must agree with
compilation. Lython supplies its runtime `re` objects, callable replacements and
result publication over this boundary.

Every operation receives the current token: compilation, preparation, matching,
enumeration, replacement, splitting and result processing. Cached patterns save
no prior caller token. Entry/exit checks do not prove in-flight interruption.
Interrupt cooperatively or report the capability limitation; do not abandon work
or substitute a private timeout/work quota.

Retained patterns/caches use the supplied longer-lived owner. Admit substantial
working storage and replacement peaks before allocation. Dispose cursors before
subjects, finish users before refunds, and propagate `MemoryAdmission` refusal
without retry. Disposal is synchronous/nonthrowing. Borrowed result validity is
limited to the [interface's operation lifetime](../src/Lokad.Lython/Host/ILythonRegexProvider.cs).

`fnmatch`/`glob` use native wildcard matching and need no regex backend. They
preserve their Python wildcard/path semantics and host authorization.

## Optional Utf8Regex adapter

```csharp
using Lokad.Lython;
using Lokad.Lython.Utf8Regex;
var engine = new LythonEngine(Utf8RegexProvider.Instance);
```

`Lokad.Lython.Utf8Regex` adapts pinned PythonRe 0.2.0. It is optional and separately
qualified; installing it does not certify backend safety or couple core releases
to a producer release.

| Capability | Current adapter behavior |
| --- | --- |
| In-flight backend cancellation | The pinned API cannot support it. Cancellable operations fail explicitly; already-cancelled tokens cancel. No background matching or private quota. |
| UTF-8 and offsets | Normal captures use UTF-8/scalar ranges; one-shot supplementary Unicode uses a validated coordinate fallback. |
| Buffered supplementary Unicode | Affected multi-scalar operations fail before backend paths known not to terminate. |
| Capture metadata | Mixed named/unnamed numbering and nested last-capture selection retain backend limitations. |
| Iteration/storage | Buffered operations pre-admit possible captures/overlap; the `.*?` workaround is adapter-local. Large inputs may reject early. |

See the [adapter README](../src/Lokad.Lython.Utf8Regex/README.md) and
[capability tests](../tests/Lokad.Lython.PublicApi.Tests/RegexAdapterCapabilityTests.cs).
The [cooperating-provider tests](../tests/Lokad.Lython.PublicApi.Tests/RegexProviderContractTests.cs)
separately qualify real in-flight forwarding, refusal and funded unwind. A passing
boundary test does not prove a backend can interrupt matching.
