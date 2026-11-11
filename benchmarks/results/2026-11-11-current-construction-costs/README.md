# Current construction costs — 2026-11-11

Two short allocation-tick captures complement the
[current CPU profiles](../2026-11-11-current-storage-costs/README.md). They reuse
the exact fully qualified `8f8628a4` core, fixed worker and optional adapter 0.4
on Windows x64/.NET 11 RC3. No runtime prototype or elapsed comparison is involved.

| Complete script / API | Marked capture | Events inside command | Prominent triggering types |
| --- | ---: | ---: | --- |
| Captured text / Run, 256 invocations | 2.343 s | 2,680 | PyString 1,349; private iteration carrier 425; BigInteger 385; byte[] 327; reservation 167 |
| JSON-to-Markdown / RunAsync, 256 invocations | 6.460 s | 6,605 | byte[] 2,923; PyString 894; BigInteger 761; PyDict 695; object[] 543; byte builder 433 |

An event names the allocation that triggers a CLR threshold notification.
**Event shares and bytes since the previous tick are not exact per-type byte
totals or removable CPU shares.** All threads and events outside the marked
command remain in the raw archive. These captures include reset, I/O, validation
and prior-result cleanup; there are no allocation stacks separating those phases.
They exclude warmup, compilation and final teardown. Both collectors lose zero
events; output/files, ownership balances, worker/collector joins and owned-fixture
cleanup pass. There is no forced collection, rundown, heap dump or new collector.

The useful source-backed lead is the private text iteration carrier. All sampled
instances are 64 bytes. Current source creates one per each of 3,072 input lines:
196,608 bytes per script, about 17.5% of the prior 1,120,747.5-byte complete desktop
allocation observation. This quantifies existing construction, **not** achieved
savings or a predicted speedup. Word strings have real Python values/aliases;
they cannot simply be recycled with the carrier.

A coherent next phase can assess direct private return transport into existing
execution-owned iteration state, consolidating list/tuple positions with cased
word state. It must eliminate the per-line carrier and displaced position-array
representation, preserve eager admission of eventual words, and handle native,
borrowed and framed returns, reentry, break, errors and awaited cleanup. A new
pool/cache, escaping mutable carrier or second ownership registry is not selected.
Larger fixed frame capacity and wider transport are explicit possible costs;
source proof and complete affected/control comparisons must decide them.

Other source checks avoid misleading directions: small object arrays include
adopted record values, not necessarily argument copies; invocation contexts
already share/defer frame construction and are not prominent triggering types.
Report byte arrays include 40–67,608-byte allocations, commonly 65,560 bytes, but
their exact call sites/guest-host split are unknown. The comparison host already
uses `FileStream`; no repeated whole-prefix append defect is established. Native
cell writes synchronize real locals and persistent namespace values, so a private
lazy integer alone cannot remove their boxes without a namespace-storage redesign.

[Assessment](assessment.json), [raw traces and checks](allocation-traces.tar.xz),
[records](records.json), [source identity](source-identity.json),
[canonical images](reference-images.json) and the existing collector's
[source](collector-Program.cs) preserve the evidence. Current CPython comparisons
remain in [BENCHMARKS](../../../docs/BENCHMARKS.md). Source qualification is reused;
these diagnostics neither change it nor resolve async report CPU debt. Publication
remains postponed.
