# Executable generic function bodies, 2026-10-18 UTC

Eligible generic definitions in executable modules/functions now use the existing
function code and executable body. The unconditional generic statement fallback
and ordinary compiled-body definition bypass disappear. TypeScope still begins
after defaults, retains its namespace and supplies the function closure; original
enclosing cells are captured before it. Type-parameter names are excluded from
enclosing body-closure candidates, preserving shadowing while defaults still
read outer values. Metadata is attached before decorators/publication. No new
scope, lookup, cell, cache or ownership mechanism. Existing class, generator and
contextual fallbacks remain; their retained representations are still necessary.

Baseline: `99eb28bf457d835dc31ec1b6de02c72f4f2d3054`; candidate: that revision plus the
[frozen patch](candidate.patch), including permanent tests. The
[source screen](source-screen.json) and [implementation scope](implementation-scope.json)
precede edits. [Measurement scope](measurement-scope.json) follows the successful
source/correctness/lifetime gate and precedes all timing. Baseline sources were
restored byte-for-byte solely for comparison, then the exact qualified prototype
was restored. One before/after pair, no repeated capture or post-gate tuning.

The script defines a generic add helper returning value+1, performs 128 calls
in a summing loop and returns 8256. The ordinary control has the same body without
[T]. Two warmups, eight samples, eight complete invocations per sample/route.

| Route / boundary | Earlier Lython | Candidate | Measurement status |
| --- | ---: | ---: | --- |
| Generic sync / native warm allocation | 92,008 B | 37,088 B | 54,920 B less per 128-call invocation; stable arrays |
| Generic async / native warm allocation | 92,008 B | 37,128 B | 54,880 B less per 128-call invocation; stable arrays |
| Ordinary sync/async / native warm allocation | 35,944 /35,984 B | Same | All control arrays identical |
| Generic sync / native warm elapsed | 0.283 ms | 0.234 ms | Unqualified; IQR 3.19%/1.98% |
| Generic async / native warm elapsed | 0.410 ms | 0.279 ms | Unqualified; IQR 2.12%/2.90% |
| Generic sync / public fresh compile-to-result | 0.664 ms | 0.612 ms | Unqualified; IQR 2.20%/5.84% |
| Generic async / public fresh compile-to-result | 0.757 ms | 0.652 ms | Unqualified; IQR 3.17%/5.08% |
| Ordinary sync / public fresh control | 0.553 ms | 0.542 ms | Unqualified; IQR 2.46%/5.05% |
| Ordinary async / public fresh control | 0.608 ms | 0.615 ms | Unqualified; IQR 5.33%/5.35% |

Native warm excludes compilation, context construction, scheduling, projection and
startup; pure ValueTasks assert synchronous completion. It includes an explicit
1,000,000-step allowance. Public fresh includes compilation admission, run setup/
scheduling, projection, disposal and checks in a warmed process, with no implicit
fuel ceiling. It excludes process startup and host I/O. These are different
boundaries. Raw ordinary native elapsed controls changed 0.47%/1.87%; fresh
controls changed -2.02%/+1.08%. Samples are not quiet-machine or statistical
qualification; no corrected/normalized speedup, compilation-only, startup,
whole representative-job or CPython timing claim. Median and inclusive-quartile
IQR are computed from per-invocation samples. [Environment](environment.json)
reuses the original Windows x64/i7-14700KF/.NET 10.0.13 configuration timestamp.

Native generic steps change 2,204Ã¢â€ â€™2,460 per invocation; ordinary controls stay 2,460.
This reflects execution strategy, not identical fuel costs. The existing optional
limit remains enforced. [SPEC](../../../SPEC.md) explicitly documents that a
fixed allowance can now fail at a different point; [MEMORY](../../../MEMORY.md)
records unchanged scope/compiled-owner funding and conservative retention.

All 423 distinct focused Release checks pass, including 58 complete CSV/text/
JSON/file jobs. Four white-box source cases check actual PyExecutableFunction
identity under both APIs, including escaped captures, shadowing and nested helpers.
Nineteen public rows pass on both original and candidate sources, including actual
host-I/O suspension/cancellation and funded captured payloads. Four additional
candidate rows enforce recursion and work limits with cleanup to baseline/zero.
Eight independent CPython 3.13.2 programs supply [goldens](goldens.json).
[Negative control](negative-control.json) confirms both reject nonlocal type-
parameter rebinding; differing diagnostic forms are explicitly retained.
[Validation](validation.json) records filters, receipts and source hashes.

[Diagnostic](diagnostic.cs), [raw baseline](baseline.trx), [raw candidate](candidate.trx),
process receipts and [comparison](comparison.json) retain evidence. The temporary
diagnostic is removed. Original Stress scales/assertions/timeouts remain; Release/
platform qualification is recorded in PLAN. Regex remains planning only.
