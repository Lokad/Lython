# Identifier and title scan cancellation

2026-10-20 UTC. Baseline runtime `f0cd695fd883915f0989a6fb88844c35f3499d57`
from source `3db1b963`, followed by the C03 change committed with this evidence.
Exact loaded assembly and common driver SHA256, UTC times, machine, runtime and
allowance are recorded in [before.json](before.json) and [after.json](after.json).
Qualified runtime is `bf45be829668b7e10996f30599eae799f3c16a27`.
[CI 39365847054](https://github.com/Lokad/Lython/actions/runs/39365847054) is
terminal green for that exact revision: Windows/Ubuntu Release, original Stress
and both explicit Release package consumers pass. All eight required final steps
were inspected. [Cached proof](delivery.json) is authoritative forever; never
query again. 4 status queries were at least 180 seconds apart. The following
documentation/evidence-only commit preserves runtime/test/build/CI inputs.
No publication or new producer qualification. All timings remain unqualified.

The two no-argument delegates discarded context/span. `IsIdentifier` additionally
converted the entire UTF-8 subject to UTF-16 before enumerating runes; both loops
lacked cancellation checks. Two added controls fail against the earlier runtime,
while eleven existing controls pass. The change forwards the existing arguments,
uses the existing execution guard between small UTF-8 chunks, and decodes
identifiers directly from UTF-8. Unicode predicates and early exits are preserved.
No per-rune counter, watchdog, option, allowance or dispatch framework is added.

| Method/API | Earlier uninterrupted | Current uninterrupted | Earlier request-to-cleanup | Current request-to-cleanup |
| --- | ---: | ---: | ---: | ---: |
| isidentifier / Run | 130.371 ms | 106.639 ms | 123.168 ms | 5.692 ms |
| isidentifier / RunAsync | 130.159 ms | 109.547 ms | 129.107 ms | 1.847 ms |
| istitle / Run | 186.860 ms | 194.802 ms | 184.697 ms | 0.067 ms |
| istitle / RunAsync | 179.424 ms | 193.004 ms | 167.690 ms | 0.090 ms |

These are single **unqualified observations**, with one 32 MiB ASCII subject per
method, under both execution APIs. Identifier input is all `a`; title input is
`A` followed by lowercase `a`, so both scan the complete subject. They are not
a CPython comparison, throughput claim, CPU attribution or latency guarantee.
The uninterrupted title observations are nominally 4.3–7.6% higher. No repeated
timing, full lane, forced GC or new benchmark framework was used.

The boundary starts at the host stdout callback for `print('ready')`, after
startup, compilation and input normalization. It includes the predicate, output
or failure processing and awaited cleanup. Cancellation follows a nominal 10 ms
delay, with actual scheduler delays recorded. All eight cancelled observations
were still pending and funded; they returned Cancelled and published only
`ready`, without a predicate result. The script has no guest I/O or callbacks
between that signal and the scan result. Public observations complement the
context-forwarding regressions and the existing deterministic active shared-loop
callback control, without adding test hooks or counting checkpoint locations.

Each process used an explicit shared 256 MiB allowance; every execution returned
to its compiled baseline and compilation disposal returned the root to zero.
All users were awaited before disposal; both owned processes exited zero in
1.63–2.20 seconds including startup and cleanup. No local build/test overlapped
the captures. The identical driver bytes were reused with immutable old/new
core copies; tiered compilation was disabled. Physical collection is independent.

Focused Release checks pass 91 internal and 93 public text/cancellation tests,
plus the existing representative/shared-budget/provider selection (counts and
overlap in [checks.json](checks.json)). [24 independent CPython 3.13.2 goldens](goldens.json)
cover direct and stored methods, both execution APIs, delayed I/O, empty/early
exits, keywords, combining marks, titlecase and astral/byte-boundary cases.
The [reference generator](goldens.py) preserves their independent source.
No assertion, timeout, allowance or original Stress scale was relaxed.

Reproduction uses the [one-off driver](Program.cs) and [project](PredicateDiagnostic.csproj),
with `-p:CoreDirectory` pointing at an immutable core DLL plus Lokad.Parsing.
The project links the existing MockLythonHost. Reuse driver bytes with each core,
set DOTNET_TieredCompilation=0 and pass revision label and output JSON path.
Use the existing owned 28-second work/1-second cleanup harness and await users.
See [owned receipts](processes.json) and the primary external performance account
in [BENCHMARKS.md](../../../BENCHMARKS.md). Stop C03 at these two methods.
