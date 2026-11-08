# Unified JSON encoding — 2026-11-08

Retain one iterative frame traversal for eager final UTF-8 builder output and
incremental chunks. Delete the independent recursive value/dictionary/sequence/
default walker. Four per-step helpers complete native operations directly; small
continuations retain actual pending callbacks. Scalar/key/string writers remain.
No new lifetime fields, pools, caches, public options or parser changes.

Baseline fully qualified c74c5802/core3fb37cc3; candidate b03fd326 plus archived
two-file production patch/core1674a06c. Full production hashes in identity.json.
Committed15c06863 independently passes Windows/Ubuntu actual11/10, originalStress
and installed core consumers at [cached CI42484943603](delivery-ci.json). This
receipt does not qualify the later R105 prototype. The main
CPython guide remains the named94cb8963 checkpoint.

| Complete JSON normalization | Allocation sync / async | Controlled elapsed, all four median pairings |
| --- | ---: | ---: |
| Eager JSON output, sync | +4.95% / +4.90% | +3.27 to +5.01% |
| Streamed JSON output, sync | neutral / — | −2.02 to −4.70% |
| Streamed JSON output, async API | — / neutral | −3.28 to −6.42% |

Eager coarse process CPU+6.25% agrees with the adverse elapsed result; baseline
spread1.13%/candidate0.54%. This is an accepted cost of deleting the separate
recursive renderer, **not a performance improvement**. Extra eager metadata is
about352B/record. Eager async controlled CPU remains unmeasured. Streamed sync
coarseCPU0to−5.56%, async nearzero at this clock resolution: elapsed observations
do not establish a universal CPU gain. The broader performance objective remains.

Config secondary-affected control−0.36to+0.47%, nearbaseline/mixedCPU.
Unchanged CSV+1.17to+8.77% with6.14%candidate spread: adverse/inconclusive, retained.
Decode-heavy JSONsummary−2.45to+1.20%, mixed/inconclusive. Original desktop eager
adverse timings and baseline drift up to35.8% remain. No historical regression debt
is repaid by this different pair. Future encoder work must preserve one traversal,
identify remaining whole storage/work and explicitly decide this eager debt;
no duplicate renderer/frame pool/blind capacity tuning is justified by these data.

Both affected scripts read4096records/110592B, edit fields/construct nested values,
and write the full UTF-8 JSON-lines result. Eager dumps and streamed dump have
identical independent isolated CPython stdout/file goldens, including non-ASCII
text, floats, lists, bool/None and source blank lines. An initial missing blank-line
filter failed the Python oracle before any measurement; source/failure preserved.

765public/171internal focused JSON Release tests peractual11/10 pass, including
options/order/chunks/numbers/Unicode/cycles/mutation/managed nesting, actual
suspended hooks/reentry/cancellation, terminal first refusal, retained chunks and
awaited cleanup. Private eager traversal has temporary128B working admission held
across callbacks; exposed iterators keep their retained value charge. Earlier
storage stays funded through final-result admission. The initial50accounting
failures exposed private storage incorrectly charged as escaping; correction and
eight threshold-fixture failures are preserved. Result-admission tests now use the
existing host hook instead of an exact tiny-budget threshold, retaining first
origin, no publication, unchanged input and all cleanup balances.42rechecked pertarget.

Quiet Ubuntu24.04.4/AzureF4as_v7,4EPYC9V45vCPUs/16GiB/noswap, actual.NET11RC3.
Same worker7cf37ca5/optional0.4adapter, normalGC/tiering;5swarmup/process/job,
two processes/core,7rotated150msrounds. Six jobs172.779s. Command-v2 includes full
execution, mediated RAM I/O, output checks and prior-result cleanup; excludes
compilation/startup/staging/final cached teardown. I/O completes inline; real
suspension is independently tested. No startup/RSS/embedding-tail claim.

All source/core/aux/Ready/golden/quiet/Closed/exit/join/fixture/archive/local gates
pass. B461 terminal/cached; all users joined and guest RAM unmounted before the
owned persistent archive. Exact-byte hardlink maintenance recovered28049408B while
preserving every original path/alias/content and archive; fresh proof included.
240MiB OSfloor+7MiB evidence+actual payload preflight. No discarded evidence.

[Controlled results](controlled-comparison.json), [allocation/desktop evidence](allocation-and-local.json),
[identity](identity.json), [status](status.json), [sources/cores/tests/failures/raw](observations.tar.xz).
