# Native tuple returns: retained final operand transport

Frozen8f69261d, six production inputs changed from fully qualified14dd45b7/93a6d134.
Exact immediate unpack consumers borrow already-admitted final caller operands.
Eliminate transient tuple shell/backing, fixed caller scratch, shared scratch type,
duplicate span publisher and the caller-output span from hot native execution
state. Only scalar result count/start remain; the synchronous return wrapper copies
before cleanup. Track output extent before any write, preserving wider/narrower
overlap and failure cleanup. Keep conservative eager tuple admission; this improves
CLR allocation, not the allowance estimate. Escaping/starred/mismatched/suspended
results retain ordinary Python tuples. No public API/behavior change or new engine,
pool, budget policy, unsafe code or execution quota.

**Correctness:**110internal/183public actual.NET11 Release focused checks pass:
aliases/defaults, target order, both APIs, genuine delayed I/O transfer, first
refused admission/cancellation before caller writes, funded retained results and
cleanup, plus existing isolated managed-stack continuation scenarios. Transitional
NET10 checks also pass110/183, but span the883c-to8f source transition and are not a
single-source qualification receipt. Full Windows/Ubuntu/original stress/installed
Release package qualification is pending on the eventual committed source.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Invoice warm |−5.07..−1.80%|−6.59..+0.67%|3.27/0.17%;1.28%|
| Invoice compile + run |−5.23..+0.83%|−4.84..+1.67%|3.01/3.20%;2.35%|
| Captured text warm |−0.67..+1.89%|+0.38..+1.32%|1.61/0.96%;3.88%|
| Report warm |−3.71..−0.19%|−3.41..−0.38%|3.37/0.16%;4.56%|

B669 four jobs180.051s,2026-11-13UTC, quiet AzureF4as_v7/Ubuntu24.04.4/fourEPYC9V45
vCPUs/16GiB/no swap, ordinary.NET11RC3 GC/tiering, fixedfec79worker/adapter0.4.
Two processes/image/five rotating500ms rounds,5swarmup(12captured). Ranges span
four paired medians, not confidence intervals. Warm includes mediated RAM-file
I/O, complete stdout/file checks and prior-result cleanup; fresh adds compilation/
compiled-owner disposal. Startup/reset/staging/final cache cleanup are outside;
widerCPU includes reset/protocol. Moving controls, CPU quantization and IQR remain
visible. No universal CPU gain, compounded CPython ratio or host-latency claim.

**Final allocation gap closed separately:** Windows' final8-case diagnostic and its
managed-stack observation time out; partial/empty outputs and joined-owner record
remain. This establishes uncertainty, not a failed runtime. B670 uses the same ALC
allocation tool on the quiet VM for only the original/rich invoice, relaxing solely
its fixed8-case guard/description. No runtime/host/clock change or new framework.
It completes15.062s with full goldens/balances/cleanup, quiet and identity gates.
Final8f allocation falls6.408–6.436% on4096records/127031B,5.049–5.052% on the
same script/goldens with823388B rich text. Controls use the same qualified93 core.
Allocation includes host reset/full validation/result cleanup; ALCs share CoreLib
and process GC, so its clocks remain diagnostic and separate from B669. The earlier
eight-script883c observations keep their own label; final8f has four controlled
scripts plus both targeted invoice fixtures, not an invented complete8-case result.

All collection users join; SHA-archive verification precedes RAM unmount. B669
retires only exact, canonically archived obsolete883 VM aliases under a fresh idle
lease/all-alias/inode/UID/hash check. B670 reuses final/reference images. Current93,
external-comparison2e, canonical bits and every adverse phase remain. No floor is
lowered; final VM free268435456B. Inherited B668 print text in B669 retrieval is
cosmetic; unit/root/core identities correctly name B669 and8f throughout.

**Retain**, based on removed transient storage and verified allocation reduction,
with measured invoice warm improvement and no repeat of the earlier stable report
regression. Fresh execution remains inconclusive. Small text-control debt (elapsed
−0.7..+1.9%, widerCPU+0.4..1.3%) is explicitly accepted for this milestone; carry
it into the next complete-script checkpoint. Do not claim a general CPU speedup or
replay unchanged timings to average away that debt. A material future regression
can justify bounded investigation; fixed percentages are not vetoes. Broader
performance work continues through representation/frontend gates and a qualified
CPython refresh. Publication remains postponed.

[Original guard](../initial/README.md), [scratch debt](../scratch/README.md),
[direct operands](../operands/README.md), [raw timing](observations.tgz),
[targeted allocation](allocation/summary.json), [sources/bits/checks/failed conditions](source-checks.tar.xz).
