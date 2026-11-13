# Native integer intermediates: retained same-stream accumulator

Frozen91d0540d, two production inputs differ from fully qualified85c73469/8f69261d.
Invoice's rounding helper is three adjacent arithmetic instructions using the same
temporary slot. Follow that existing stream with one local signed-long accumulator,
boxing only its final observable value. No new opcode, expression graph, evaluator,
metadata array, shadow slots or compulsory per-helper payload. Calls/stores/branches
stop the region. Pure eligibility failure or overflow publishes nothing and keeps
the existing exact arithmetic/admission path; no admission refusal is retried.

**Focused Release11:**135internal/216public unique cases pass across targeted runs:
signed floor/remainder, long boundaries and promotion, bool/custom protocols,
identity at stores, original error order/spans, aliases and shared-budget reuse,
first refusal/cancellation with retained inputs and balanced frames, genuine delayed
I/O after arithmetic, plus existing isolated managed-stack continuations. An initial
test fixture was correctly rejected by static unbound-local validation; original
failure is retained and corrected valid-source/operand-error coverage passes.
Committed40d92aef is fully source-matched and qualified on Windows/Ubuntu actual
NET11/10, original stress and installed Release consumers by[cached GREEN43417375221](delivery/README.md).
All573frozen production inputs match that head; no unchanged measurement replay.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Invoice warm |−6.55..−2.91%|−7.58..−4.48%|2.68/1.17%;1.72%|
| Invoice compile + run |−6.03..−3.78%|−6.52..−2.82%|1.32/1.05%;1.32%|
| Captured text warm |−4.85..−2.31%|−6.61..−1.46%|1.68/0.93%;1.28%|
| Report warm |−0.68..+4.78%|−2.92..+6.94%|1.85/3.46%;1.70%|

B672 four jobs180.331s,2026-11-13UTC, quiet AzureF4as_v7/Ubuntu24.04.4/fourEPYC9V45
vCPUs/16GiB/no swap, actual.NET11RC3 normal GC/tiering, fixedfec79worker/adapter0.4.
Two processes/image,5rotating500ms rounds,5second warmups(12captured,8report).
Ranges span all four paired medians, not confidence intervals. Warm includes
efficient mediated RAM-file I/O, complete stdout/file goldens and prior-result
cleanup; fresh adds compilation/compiled-owner disposal. Startup/reset/staging/final
cache teardown are outside. Wider process CPU includes reset/protocol.

B673 reuses the unchanged two-fixture ALC allocation tool/catalog/witness on the
quiet VM, completing15.165s. All goldens/shared balances/cleanup/quiet/identity/join/
SHA-archive gates pass. Allocation falls8.456–8.480% on4096records/127031B and6.545%
on the same script/goldens with823388B rich text. The affected run removes524288B,
consistent with16384intermediate32Bboxes. This is not a claim that all time savings
come from boxing: repeated generic dispatch/publication is also avoided. Allocation
includes host reset/full validation/cleanup; shared CoreLib/processGC means tool
clocks remain diagnostic, separate from controlled B672 timing.

**Bounded report confirmation:** B674 uses unchanged bits, longer12s warmups and
seven500ms rounds, one job72.678s. It observes elapsed−9.17..+0.15%/widerCPU
−7.68..−1.45%, but reference spread7.38%/candidate2.68%, maxIQR2.21%. It does not
establish a stable report gain or regression. Preserve the original adverse result;
do not pool collections or describe unchanged code as a correction.

**Retain** for removed storage and supported complete-invoice CPU improvement.
Report remains unresolved debt, accepted at this milestone and carried to the next
changed-source checkpoint. No universal speedup or new CPython ratio is inferred.
The[qualified8f external checkpoint](../2026-11-13-native-return-cpython-checkpoint/README.md)
keeps its own source label. Whole-script/frontend and independent regex work continue.
Publication remains postponed.

All workers join, funding returns and SHA archives verify before owned RAM teardown.
B672 retires only exact prior93 VM working aliases after verifying the canonical
source/binary archive, every link/inode/UID/hash and a fresh idle lease. Current8f,
canonical93 qualification and earlier adverse evidence remain. No OS/evidence floor
is lowered. B673/B674 reuse existing images/tools and leave no VM users or mounts.

[Controlled timing](timing-summary.json),[raw timing](observations.tgz),
[allocation](allocation/summary.json),[report confirmation](report-confirmation/summary.json),
[all exact sources/bits/checks/failed fixture](source-checks.tar.xz).
