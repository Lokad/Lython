# D62 one bounded publication-fence refinement, before code

Original B293193.43s/sevenjobs/all gates preserved under parent/results. Frozen
14780388/current/control against D61/6c669799. Warm text-5.03/6.07%,JSON-7.67/7.57%,
CSV+2.12/+0.49% (1.60% same-current spread), basic+1.94/+1.40%; freshCSV-.60/+1.06%,
report variable/referenceIQR5.13%, compile+1.45/-.91% inconclusive. Do not erase
these adverse points or infer causalCPU/allocation/embedding latency.

Demonstrated source addition: PublishInstructionResult is forced NoInlining even
when HasLocalResult is false, where the old ordinary path published directly.
Refinement restores direct ordinary publication via a small inlineable wrapper;
only annotated local completion retains the existing no-inline lifetime fence.
No builtin/type selector, second strategy, new metadata/state/owner or skipped
cancellation/refusal. Same original cleanup-before-binding path and source spans.
This is completing the general transport design, not claiming that a JIT-layout
change guarantees CPU gains. Preserve all useful old store/worker barriers.

One final <=240s sevenjob complete CSV/text/JSON/report/basicloop warm plus CSV
compile-run/compile-only against SAME frozenD61/6c669799, independent same-current
control/CPython3.13.16/workerb94, normalGC/tiering and all existing gates/boundaries.
Focused actual11/10 checks before timing. Then decide retain/refine/withdraw and
explicit remaining regression debt; no automatic percent veto, additional broad
profile campaign, selective favorable-only timings or repeating unchanged D61
qualification. Full local/platform/stress/installed only at retained milestone.
