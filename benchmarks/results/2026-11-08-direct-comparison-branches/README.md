# Direct comparison conditions — 2026-11-08

R95 replaces adjacent comparison/truth-branch instructions with one operation
when their spans match. Exact numbers reuse PyNumberOps.TryCompare and consume
truth without a boolean result box or publication. Ordered comparison helpers pass
one operator enum; predicate delegates, method-name transport, the async forwarding
wrapper/invoker parameter, unused truth parameter and primitive fallback disappear.
Escaping comparisons keep result objects; custom/reflected/NotImplemented and
suspended comparison/truth semantics stay on the existing protocol helpers.

Baseline: fully qualified05d81b83/core68f63d29 (metadata HEAD80143659).
Retained candidate:80143659 plus archived R95 sources/core9d30623c. Full source/core
hashes are in identity.json. Committedcb2b015e is fully qualified by
[CI42472284713](delivery-ci.json): Windows/Ubuntu actual11/10, original Stress
and installed core consumers.
The primary CPython table still describes its named ac5aea97 checkpoint. These
are Lython source-pair improvements, not new CPython ratios or universal gains.

| Complete script | Warm allocation diagnostic | B456 elapsed, all four median pairings |
| --- | ---: | ---: |
| CSV helper, sync |−3.72%|−9.44 to −5.00%|
| CSV helper, async API |−15.25%|−15.73 to −10.20%, variable candidate control|
| Returned CSV records, sync |−3.15%|−7.53 to −3.25%|
| JSON aggregation, sync |−3.87%|−7.92 to −6.18%|
| Ordinary rsplit text, sync |approximately neutral|+1.28 to +3.50%, retained debt|
| Report, async API |approximately neutral|−1.17 to +1.79%, inconclusive|

Affected coarse process CPU agrees in direction: CSVsync−0.95to−6.98%,
async−10.26to−14.63%, returnedCSV−0.95to−6.90%, JSON−7.50%.
Async CSV identical-candidate elapsed spread5.50% limits magnitude precision.
Text CPU0to+2.90%; its elapsed regression is explicit accepted debt. Architectural
removal and affected gains justify retention; unrelated earlier gains do not repay
that debt. No complete causal explanation of the remaining text penalty.

B455's original corecde66aec/source/raw data remain intact: CSVsync−8.29to−8.81%,
async−13.45to−16.53%, but text elapsed+0.72to+6.58% and CPU+4.41to+7.46%.
One justified refinement restores the unchanged ordinary value handler and gives
comparison control its own opcode handler. B456 repeats exactly the same six jobs,
settings and baseline. Separate collections and JIT layout prevent assigning the
difference to a single removed branch. Desktop adverse text and variable controls
remain in the raw evidence. No further retiming to erase the residual regression.

B455/B456168.446/168.583seconds. Ubuntu24.04.4/F4as_v7,4EPYC9V45 cores,
.NET11RC3, unchanged worker7cf37ca5/optional adapter0.4. Normal GC/tiering;
5second warmup per process/job,7rotated150ms rounds, two old/two current processes.
Complete execution includes mediated RAM-file I/O, full stdout/file checks and
prior cleanup; compilation/startup/staging/final cache teardown excluded. Hosts
complete benchmark I/O inline; real suspension is tested separately.

The separate original-source frontend diagnostic reports affected compilation
allocation−0.06to−0.10%, affected compile-to-result allocation−3.0to−3.7%.
Its elapsed controls vary and establish no frontend CPU improvement. Moving the
handler changes no frontend algorithm; those timings are still labeled as the
original cde prototype, not reattributed to the retained9d core.

Focused final-source checks:76public/61internal per actual11/10, plus6public
checks per target after strengthening exact file-close assertions. Three new
complete programs independently match isolated CPython3.13: all numeric forms,
large/mixed/NaN/infinity, escaped raw results, reflection, chains, loops and cleanup.
Real comparison/truth suspension, cancellation, terminal refusal/first origin,
input aliases and reusable shared funding pass. No public API change.

All source/core/auxiliary/Ready/golden/quiet/Closed/exit/join/fixture/archive/local
verification gates pass; no VM users or fixtures remain. observations.tar.xz keeps
both prototypes/cores/sources and original failures/adverse data. A bounded fresh
inventory rejected alias paths before changes; resolved in-scope targets then used
verified exact-byte atomic hardlinks, preserving every alias/content/archive and
freeing28,057,600B. Aliases were verified again before B456. No evidence deletion.
No RSS, embedding-tail, startup or whole-performance-objective completion claim.
