R194 controlled confirmation, 2026-11-10. Compare the same frozen refined
8f24bf38 and fully-qualified ac6edb6f images. Reuse unchanged complete-jobs.dll,
its five complete scripts,32warmups/role,5rotated32-invocation rounds and0.2ssettle.
Four fresh driver processes, order syncA/asyncA/asyncB/syncB; each has before,
before-control,candidate isolated images sharing CoreLib/processGC. Ordinary
GC/tiering, same worker/adapter0.4/auxiliaries and hashing. No product/tool changes.

Report run-only executionMilliseconds separately from whole elapsedMilliseconds:
run-only includes mediated I/O/projection and excludes fixture reset, output/file
validation and result disposal. Whole envelope includes those. Compilation/startup
are outside warm samples. Existing earlier desktop summaries used run-only times;
do not mislabel them as cleanup-inclusive or compare them to CPython worker clocks.
CPU is wider process time with shared GC, not isolated per-engine CPU. Async uses
completed host I/O; genuinely pending I/O latency remains unmeasured.

Purpose: resolve sync targets and adverse async report/search using two independent
process observations per API and unchanged controls. Keep original/refined source,
JIT code/frame growth, adverse timings and all controls. No fixed percentage veto,
no prediction, no automatic promotion on source simplification. One owned120srun,
30sstop/30smargin,240MiBOSfloor/12MiBevidenceheadroom. Reuse existing machine and
all golden/balance/isolation/fixture-cleanup checks; archive persistently under
fresh idle lease and verify localSHA. Do not restart a terminal or timed-out owner.
Decide source/debt or record uncertainty after this bounded confirmation. No broad
qualification/ownership audit campaign. Publication remains postponed.
