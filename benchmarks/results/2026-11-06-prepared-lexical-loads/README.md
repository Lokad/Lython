# Prepared lexical loads — 2026-11-06

Retained compiler fact/routing simplification and generic-shadowing correction;
**elapsed improvement is inconclusive**. [Declared design](contract.md),
[raw four-job comparison](observations.tgz), [summary](summary.json),
[validation](validation.json), [original failure/check logs](correctness-history.tgz).
This milestone does not complete the whole-script performance objective.

Enclosing executable compilation already knows ordinary locals and captures.
One compiler-only completeness fact lets the remaining function free names use
the existing live LoadGlobal operation. Ordinary global reads stop walking
function-local lexical frames. No new opcode, runtime guard/cache, token, owner,
API or alternate interpreter. Dynamic separately compiled class/generator bodies
and type-parameter scopes retain the general resolver because they still need it.
The global/builtin values remain live, including mutation and deletion.
[Static proof](prepared-loads.json) shows CSV's `int` becomes LoadGlobal; primary
text/JSON/report native-return helpers contain no such loads and remain controls.

Six [independent CPython3.13.2 controls](independent-goldens.json) cover live
global/builtin updates, transitive captures, type parameters, comprehension
captures and class/global barriers, through both public APIs with budget reuse.
The original type-parameter test exposed an existing false LA3113: outer T=str
was incorrectly carried into the generic function body and simple summary.
Both fact masks now discard the shadowed outer value. The golden was unchanged;
the original failed2768/1 run is preserved. Focused2769public/1098internal pass
per actual.NET11RC3/.NET10.0.8, zero-warning/error Release build. Existing gates
include cancellation, terminal admission refusal, suspended users and cleanup.
Full platform/stress/installed qualification subsequently passed on ae3a5a5b,
cached42155751891/all8; see the linked delivery evidence below. K01's earlier
qualification did not cover these changes.

B311113.56s/four jobs, quiet Azure F4as_v7 Ubuntu24.04.4/.NET11RC3, unchanged
workerb94/protocol2, normalGC/tiering, CPython3.13.16, two identical-current roles,
5-second warmup/role and7rotated150ms rounds. Candidate50b6b872 versus original
dd76df36/coreb16fc0d7. Frozen patch/source identities, full output/files, quiet
endpoints, Closed/final funding, joined workers and removed fixtures all pass.

| Complete workload / boundary | Original ms | Current ms | Identical current ms | Current/original changes |
| --- | ---: | ---: | ---: | --- |
| CSV helper / warm | 4.840341 | 4.802161 | 4.872774 | -0.79% / +0.67% |
| CSV helper / compile-to-result | 4.887735 | 4.876410 | 4.973230 | -0.23% / +1.75% |
| Text helper / warm control | 2.271214 | 2.246184 | 2.306581 | -1.10% / +1.56% |
| Report / warm control | 3.740319 | 3.703592 | 3.731444 | -0.98% / -0.24% |

Both CSV roles straddle the baseline, as does text; report IQR reaches7.55%.
Do not claim a speedup or infer allocation/causalCPU gains from this block. No
automatic retiming follows. The generic correction is included in frozen source
but timed scripts have no generic parameters. Keep these observations separate
from B309's K01/P01 debt and the primary representative guide.

Warm includes invocation, mediated RAM-file I/O, full validation and prior-result
cleanup; compile-to-result adds fresh compilation/program disposal. Startup,
fixture staging/reset, worker transport and final teardown are excluded. Persistent
CPython imports versus per-execution Lython imports remain an asymmetry. Startup,
pending-I/O latency, RSS/heap and embedding-process latency remain unmeasured.

Combined F02/F03 delivery on ae3a5a5b is GREEN, cached terminal42155751891/all8.
[Exact scope and steps](../2026-11-06-unified-invocations/delivery-ci.json) exclude later F04/F05. No unchanged qualification was repeated.
