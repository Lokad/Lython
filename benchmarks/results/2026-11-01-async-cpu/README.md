# Bounded async guest-CPU attribution

Source **1b99e1ad**, 2026-11-01 Paris / 2026-11-01 UTC. Two fixed complete scripts,
512 warm RunAsync jobs per case, **8.6468 seconds** total capture. Both quiet gates
and complete stdout/file goldens pass; every worker/profiler closes and joins.
No old/new elapsed or CPython comparison, new runtime path or speedup claim.

Question: after compiled functions/comprehensions/direct ordinary calls, do
completed async operation helpers dominate native CPU work sufficiently to
justify a broad async-engine rewrite? The earlier synchronous capture cannot
answer this async hosting question. This is one new bounded question; previous
candidates are not retimed and no missing regex release capability is involved.

## Fixed collection and boundary

Dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, pinned
.NET 11 RC3. Actual worker/library/input identities remain in each record and
[raw archive](raw-capture.tar.gz). Ordinary worker GC/tiering; the supervisor alone
disables tiering for idle-machine checks. Perf cpu-clock at 199 Hz with frame-
pointer caller chains and CLR perf maps; all native profiling is external to
Lython. No unsafe code, pointers, P/Invoke or native stack probing enters core.

The [adapted existing driver](ops-profile.py) reuses the maintained complete CSV/
text-helper scripts and file host, lane warm-async. Compilation/startup are
excluded; declared fixture reset, host I/O, full validation and cleanup remain
inside the batch. Host operations genuinely support async but complete synchronously
here; these captures do **not** measure delayed/suspended I/O. Existing gated
protocol/cancellation/ownership tests qualify real suspension separately.

A shared exclusive VM lease lasts through gates, collection and cleanup inside
one 180-second owned service. Total capture was capped at 60 seconds; each case
runs once. Profiler readiness uses the existing acknowledged control FIFO. Every
512-job response is complete; before/after golden verification and worker quit
succeed. [Collector](collect.sh), [guest leaves](analyze-cpu-guest.py),
[initial caller chains](ops-chains-guest.py) retain exact operations.

| Complete async script | Capture wall | Guest CPU | Background JIT CPU | Guest samples | Unknown leaves |
| --- | ---: | ---: | ---: | ---: | ---: |
| CSV helpers | 4.6947 s | 3.86 s | 0.62 s | 768 | 58 |
| Text helpers | 3.9521 s | 3.12 s | 0.59 s | 620 | 40 |

The guest is selected from its recorded Lython execution-thread identity, never
the max-CPU thread. All thread CPU deltas remain visible. Background JIT/tiering
continues during captures, so these are not steady-state elapsed measurements.
Each map has one explicit truncated trailing record; no IP/symbol is silently
replaced. Caller chains can omit inlined or managed/native transition frames,
even with no empty/single-frame chains in these observations.

## Findings and stopping point

Source already routes exact native binary/unary/subscript work through synchronous
operations, and user functions through the explicit frame runner. Common text,
dictionary, file and CSV calls avoid bound-argument packaging. The async loop
retains generic helpers only for remaining operations/protocol-capable cases.

Measured self leaves, not inclusive time or removable CPU fractions:

| Complete script | Guest samples | Main async dispatch | Async value/call helper | Async operand helper |
| --- | ---: | ---: | ---: | ---: |
| csv-async | 768 | 87 (11.3%) | 11 (1.4%) | 0 |
| text-async | 620 | 79 (12.7%) | 11 (1.8%) | 0 |

Value/call helper leaves are about 1.4–1.8%; there are no operand-helper self
leaves in these fixed samples. Main dispatch contains 11–13%, including required
fetch/cancellation/admission and ordinary opcode work; it is not all async
bookkeeping. These samples **do not justify a wholesale async engine rewrite**,
removing protections or introducing another dispatch backend/tagged-value system.

CSV iteration/parsing/materialization, native text work, dictionary operations
and ownership/admission remain distributed costs. Text still records 54 sigma-
property samples; that does not reopen the rejected casing prototype. Numeric
protocol dispatch is visible in CSV, but its self samples alone do not establish
a dominant removable component. Inclusive frame/cursor samples include their
actual work and must not be described as wrapper overhead.

One bounded additional **offline** origin derivation reads the same saved events,
adding no collection or timing. [Source](native-origins.py) and
[saved chains/origins](offline-origins.tar.gz) preserve every parsed chain and full
leaf counter. TLS/barrier/monitor/unknown native leaves are assigned their first
recorded Lython caller with the missing/inlined-frame limitation explicit.
Origins include CSV record work, frame construction, stack admission, dictionary
storage and text builders. They do not isolate one universal ownership/boxing
bottleneck or justify dropping stack/cycle/cancellation/admission checks.

Stop B22: the async question has a bounded answer and remaining uncertainty.
Do not recapture for a favorable result or claim current CPython multipliers.
A separate dictionary ordering probe identifies an independent observable bug
above the small-storage boundary; any ordered-storage consolidation needs its
own semantics/ownership and fixed whole-job controls before implementation.
