# Complete CSV-helper attribution

R86/B449,2026-11-08. Committed source02c76e52/coreaa4acebe, frozen before the
exposed-callable correction. The later full CI failure concerns environment views
and Decimal diagnostics; its correction is tracked separately. This is diagnostic
CPU attribution, not qualification of that source or a performance comparison.

Existing complete CSV aggregation with an ordinary helper, full stdout/files
goldens before/after. CPU-clock/FP199Hz, six guest-second warmup, two-second settle,
six-second wall capture. Actual5.65guestCPU-s/1,124samples;79unknown(7.03%).
PerfMap/JIT diagnostics preclude normal-settings elapsed claims. Self shares:
instruction prefix11.57%, input handler6.49%, local-result completion3.11%,
stack transfer3.02%. Combined parser6.67%(15.12%inclusive), record adoption2.58%
(4.72%inclusive), binding/work construction3.56%(4.27%inclusive), numeric
normalization/comparison5.43%(6.23%inclusive). Inclusive shares overlap; inlined
work belongs to its caller. Do not add these as independent causal costs.

No dominant binder phase justifies an arena or value/frame redesign. Current CSV
already transfers private funded row arrays, shares record keys, batches field
admission and compacts native file input in place. The older bulk-row proposal
cannot be revived unchanged. Continue bounded source/generated-code attribution
of the demonstrated execution/operand work before selecting its replacement;
avoid another unmeasured per-name shortcut. This does not close the performance
objective or permanently reject a refined coherent design.

All source/core/auxiliary/Ready/runtime/pre-post goldens/quiet/Closed/exit/join/
fixture/archive gates pass. Persistent raw data/maps/unknowns retained. Temporary
CLR maps copied/verified after worker join and before owned RAM cleanup. Archive
verified under lease and independently locally; no workers or fixtures remain.
120s owner+30stop+30margin. Existing immutable B448 core/archive reused; historical
trace/archive size+25% fits8MiB headroom above240MiB OS floor. No evidence deletion.

[Assessment](assessment.json) and [complete evidence](observations.tar.xz) retain
raw samples, generated code, contracts, identities, sizing and original failed
conditions. Exact core bytes remain in the linked provenance of the immutable
[receiver checkpoint](../2026-11-08-receiver-contract/README.md).
