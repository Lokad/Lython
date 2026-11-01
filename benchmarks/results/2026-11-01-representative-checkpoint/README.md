# Post-B27 representative performance checkpoint (B28)

One fixed collection on **2026-11-01**, current **830c6251**, earlier Lython
**4433986e**, pinned .NET 11 RC3 and CPython **3.13.16**. Seven jobs,
**58.219 seconds** total, under the prospective 90-second bound. No runtime
prototype, new benchmark framework, lane campaign or favorable retiming.

Dedicated Azure F4as_v7, Ubuntu 24.04.4 x86-64, four EPYC 9V45 vCPUs / 16 GiB,
no swap. Both quiet-machine gates pass. Ordinary worker GC/tiering; supervisor
tiering alone is disabled by the existing tool. Reference Python uses `-I -S`,
UTF-8 file mode, ordinary GIL/GC and the pinned PGO/LTO release build.

## Current Lython against CPython

| Phase / complete workload | Lython ms | CPython ms | Lython / CPython | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| Fresh CSV helpers | 8.083 | 3.707 | 2.18× diagnostic | 4.0% / 0.5% |
| Fresh text helpers | 6.201 | 1.466 | — inconclusive | 11.7% / 0.6% |
| Fresh JSON-lines helpers | 8.413 | 4.451 | 1.89× diagnostic, borderline | 10.0% / 0.5% |
| Warm CSV helpers | 7.320 | 3.511 | 2.08× diagnostic | 6.2% / 0.8% |
| Warm text helpers | 6.539 | 1.327 | 4.93× diagnostic | 2.6% / 1.1% |
| Warm JSON-lines helpers | 7.918 | 4.307 | 1.84× diagnostic | 6.3% / 0.6% |
| CSV compilation only | 0.09464 | 0.05471 | 1.73× diagnostic | 3.6% / 0.3% |

CSV: 4,096 records / 57,361 bytes; text: 3,072 lines / 35,840 bytes;
JSON: 4,096 records / 110,592 bytes. Complete maintained scripts, not isolated
operations. All phases verify full stdout/file goldens before and after timing.
One second warmup per engine and seven rotating-order batches targeting 25 ms.
IQR is interpolated interquartile range / median, not a confidence interval.
Predeclared >10% IQR withholds the ratio; JSON is 9.9789% before rounding.
The text fresh point ratio is 4.230, retained here as an inconclusive observation.

Fresh jobs include compilation, one public synchronous Run, file fixture
reset/staging, efficient mediated I/O, complete checks, prior-result cleanup and
per-invocation compilation disposal. Warm jobs reuse compiled code. Compilation
jobs execute no guest code. Final results stay retained through the response;
worker exit disposes remaining results/cached compilations. Startup, transport
and response hashing are excluded. These independent phase medians cannot be
subtracted or added: warm text's point estimate exceeds its noisy fresh estimate.

## Earlier Lython control, separately

The earlier source runs in the same collection on the same runtime, with exact
source/input/output equality checked before collection. These aggregate changes
span B09–B27; do not attribute them to a single optimization or compare them to
separately collected historical medians.

| Phase / workload | 4433986e ms | 830c6251 ms | Change in point median | Old / new IQR |
| --- | ---: | ---: | ---: | ---: |
| Fresh CSV | 8.945 | 8.083 | -9.63% | 4.5% / 4.0% |
| Fresh text | 8.556 | 6.201 | -27.52%, inconclusive | 6.5% / 11.7% |
| Fresh JSON | 8.568 | 8.413 | -1.81%, inconclusive | 11.6% / 10.0% |
| Warm CSV | 7.646 | 7.320 | -4.27%, inconclusive | 10.6% / 6.2% |
| Warm text | 6.051 | 6.539 | **+8.06%** | 5.4% / 2.6% |
| Warm JSON | 7.840 | 7.918 | +0.99% | 6.7% / 6.3% |
| CSV compilation | 0.33030 | 0.09464 | -71.35% | 8.6% / 3.6% |

Compilation's diagnostic reduction is substantial. Warm text regresses in this
observation, and warm JSON does not improve; both remain visible. These are short
diagnostics, not qualified release speedups or demonstrated causal attribution.
No CPU samples were collected here. Existing profiles constrain hypotheses but
do not establish a current removable fraction; no new redesign follows merely
from a favorable median or the presence of boxing/async helpers.

## Ownership, controls and evidence

The existing collector holds the exclusive VM lease through quiet checks, all
jobs and cleanup. Each micro job has a 30-second systemd control-group bound;
its worker supervisor awaits shutdown. The outer collector has a 120-second
failure bound, with a prospective 90-second collection budget and cleanup
headroom. All seven jobs complete, the collector service is inactive afterward
and the VM lease is available. No abandoned background work or producer polling.
Runtime admission/cancellation code is unchanged by this checkpoint; B27's
independent semantic/refusal/ownership tests qualify those contracts.

[summary.json](summary.json) records exact source/library hashes, full-precision
medians, uncertainty flags and the raw archive digest. [raw-results.tar.gz](raw-results.tar.gz)
contains every response, loaded worker identity, source/input/output hashes,
attempt, sample, machine/toolchain inventory, pressure/steal/page/throttling
counters and collector logs. The child service throttling counter remains
explicitly unavailable; parent counters are retained. [collect.py](collect.py)
adapts the existing fixed collector only. The source archive identifies Python
and catalog inputs; no new maintained measurement subsystem is introduced.

Stop B28. The current diagnostic gaps are warm text, then CSV/JSON; fresh text
remains inconclusive, while compilation has its own independently measured gap.
Do not retime this checkpoint, derive phase costs by subtraction, claim current
startup/suspended-I/O performance or reopen closed prototypes without new evidence.
