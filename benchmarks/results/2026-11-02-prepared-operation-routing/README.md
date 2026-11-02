# B114: direct prepared-operation routing

2026-11-02. Preserve the executable engine and existing operation bodies; route
once from the nonasync entry to the existing suspension, definition or target
handler. Delete two default-await forwarding arms. Normal stores no longer enter
three state machines merely to reach the actual handler. No new dispatcher,
backend, ownership mechanism, type matrix or option.13 production lines added,
3 removed; genuine pending operations keep the original awaited cleanup.

The fixed current B113 CPU source gate completes in25.152s. Actual named guest
threads: CSV5.91CPU seconds/1174 samples, text4.24/843; unknown94/45 and partial
perf-map lines remain explicit. Prepared leaf samples28/38 include handler work;
they do not establish a removable forwarding fraction. CSV already appends text
in bounded UTF-8 runs. No per-character conversion removal or speculative
record-shape storage follows from these captures.

## Combined whole-script comparison

Compare pre-B113 `8f58934c`/runtime`bc0d9823` with B113 plus this routing patch.
Exact hashes/source patch are recorded. This measures the combined migration;
it does **not** separately attribute a CPU improvement to these13 lines.

| Complete script | Warm allocation | Warm CPU vs baseline, two blocks | Control / decision |
| --- | ---: | ---: | --- |
| Text aggregation | –24.63% | –15.40%, –12.91% | Unchanged controls+0.92%,+0.45%; combined benefit clear. |
| Helper report | –35.7% | –10.66%, –10.65% | Controls–0.58%,+0.09%; combined benefit clear. |
| File search | –47.5% | Inconclusive | First CPU control+216.7%, later elapsed control also moves; no new search CPU/elapsed claim. |
| CSV aggregation | Unchanged | +3.35%, +2.76% | Controls–2.48%,–4.20%; about6–7% debt remains. |
| JSON aggregation | Unchanged | +0.04%, –0.28% | Controls–1.37%,–0.29%; approximately flat, first small debt remains uncertain. |

Retain the routing simplification. Text/report gains survive a complete controlled
source pair; no universal speedup and no claim that CSV debt is paid. That debt
remains an explicit next-call/activation milestone obligation. The performance
objective remains open beyond this change.

Same F4asv7/.NET11 RC3, ordinary GC/tiering, existing B113 three-image driver,
identical auxiliary DLLs, two original/reversed blocks,192 warmups and9×32 jobs
per image/case. Complete Run includes fixture reset, actual mediated I/O, full
stdout/file goldens, result disposal and balance checks; compilation/startup
excluded. Compiler source/metadata is unchanged. Reuse B113's separate conditioned
compilation result; no additional compilation or CPython campaign.

Comparison service122.977s exits0, quiet before/after; full hash witnesses,
all goldens/balances and fixture cleanup pass. All owned services terminal and
VM lease released. CPU is process CPU including runtime/JIT, not isolated guest
CPU. Keep adverse controls and all original B113 data linked separately.

## Correctness and delivery

Release internal1175 cases per actual.NET11/.NET10: executable/argument binding,
assignment/function/generator/closure/annotation/exception/async/admission.
Public.NET11 selected1719 pass (3m58s); narrower.NET10 selection170 passes (6s),
including target semantics, real host/protocol suspension and cancellation,
retained inputs, terminal refusal, budget reuse and compound native returns.
No new architecture-only tests or relaxed assertions/timeouts.

B113's coherent platform/Stress/installed-package run41472979014 is independent
and was still active when this source snapshot was prepared. This routing delta
is focused-qualified only until the next batched delivery; do not claim that
run qualifies an uncommitted patch. No publication or new adapter pin.
