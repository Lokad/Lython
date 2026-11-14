# Defensive helper source-form cost

R367/B755, 2026-11-14 UTC. **Same** committed `1c5af539` / frozen `ff2b5d76`
in every role, fixed worker/adapter0.4/catalog/input/output. Quiet Ubuntu24.04.4,
F4as_v7/four EPYC9V45, .NET11RC3, normal GC/tiering. Candidate labels mean protected
Python source, not another runtime. One two-job 73.478s collection; four fresh
processes/job, five-second full-request warmups, five rotating half-second batches.

Wrap invoice price_line in try/except ValueError and search wanted in try/except
TypeError. Inputs are valid and every stdout/file byte is unchanged. Exceptional
behavior intentionally differs: this is a whole source-form diagnostic, not Python
equivalence for all inputs or isolated frame attribution. Sixteen independent
Lython/CPython3.13.2 source/phase goldens pass locally, with budget cleanup.

| Complete warm workload | Protected elapsed change | Wider CPU change | Original/protected process spread | Allocation change |
| --- | ---: | ---: | ---: | ---: |
| 4,096-record invoice | +21.13..21.39% | +19.64..21.82% | 0.014/0.197% | +41.23% |
| 2,049-line early-exit search | +63.47..77.93% | +51.25..68.67% | 0.284/7.868% | +294.91% |

Positive means more/slower. Ranges use all independent process-median pairings,
not confidence intervals. Search protected processes differ materially: retain
the uncertainty rather than claim a precise multiplier. Invoice's stable controls
support a material cost; no confirmation is needed merely to narrow search's range.
Allocation is a separate local process-wide diagnostic with the existing tool:
5,245,534→7,408,429B/invoice;147,394.875→582,077.75B/search. It includes reset,
mediated I/O/output/file checks and result cleanup, excludes compile/startup/final
cache teardown, and performs no forced GC. Its counters do not prove VM CPU.

Quiet timing includes mediated RAM I/O, validation and prior-result cleanup;
reset/protocol/startup/compile/final cache teardown excluded. Wider process CPU
also includes reset/protocol. Compilation/fresh-source CPU, actual exceptional
input cost and embedding tail latency are not measured. These are not CPython
ratios, current runtime improvements or a prediction for a new design.

Read-only existing compiled-fact inspection confirms native eligibility changes
true→false for price_line (12 value slots) and wanted (6), while module capacities
and invoice rounded_basis_points stay unchanged. The compiler excludes every
exception region. This supports inspecting a complete activation boundary; it
does not attribute the whole difference to lexical-context allocation alone.

Select R368's bounded source feasibility/prototype milestone: permit the existing
synchronous native prefix through try/except's normal body, transfer an actual
fault at its exact PC into the existing ordinary pending-exception/handler path,
and leave finally/with/generators on their existing route. Skip handler-only code
during native eligibility; never execute a handler on a frame lacking its scope.
Remove the blanket exception-region exclusion and the eager context/owned backing
it imposes on successful eligible calls. No second handler dispatcher/controller,
guest-operation replay, swallowed compiler defect or new public option.

Before retention, prove exceptions/defaults/keywords/closures/aliases/evaluation
order, handler context/chaining, exact PC, protected nested calls, genuine I/O,
finally cleanup on existing routes, managed stack, cancellation, first refusal
and funding through joined cleanup. Then one bounded affected/control comparison
with compile/fresh and allocation separate decides retention/debt or withdrawal.
Earlier single-engine/merged-entry failures remain controls, not permanent bans.

All image/catalog/output/file/budget/quiet/join/archive gates pass. Raw SHA256
`4220e9ef54c311ed97fc5d78bf1c6ec0064bd6632223196f2a4fff17f8f383bb`.
No users or fixtures remain; free space276,013,056B;254/252MiB floors/current
images/catalog/toolchain preserved. No production change in this assessment.
The performance objective remains active; publication held.

[Timing and every control](summary.json), [allocation](allocation-summary.json),
[raw](observations.tgz), [source, goldens and local diagnostics](source-and-local-checks.tar.xz).
