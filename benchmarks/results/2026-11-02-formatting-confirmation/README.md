# Direct UTF-8 formatting: controlled confirmation

2026-11-02, runtime f9ae2d65 plus the identical c23d1a10 Unicode correction in both variants. **Retain the direct UTF-8 formatting/storage implementation.** Two independent counterbalanced blocks support this decision on the complete report workload; the cause of the earlier adverse observation remains unknown.

| Report render,3,072 records | Without direct storage | Direct storage | Identical original | Change against both originals |
| --- | ---: | ---: | ---: | --- |
| Block1 complete elapsed |11.049ms|10.437ms|11.015ms|-5.54%/-5.25%|
| Block1 process CPU/job |9.583ms|9.024ms|9.604ms|-5.83%/-6.04%|
| Block2 complete elapsed |11.234ms|10.442ms|11.303ms|-7.05%/-7.61%|
| Block2 process CPU/job |9.726ms|9.042ms|9.787ms|-7.03%/-7.62%|

Complete-job allocations are about13,073KB vs11,951KB, a reduction of8.59% in this current-source setup. This is separate from the established8.67% on the earlier revision. Report elapsed IQR/median0.96-2.94%; CPU0.57-2.56%. Unchanged JSON complete elapsed differs by-0.50% to+1.50% from the originals in the second block and-0.44%/+0.11% in the first. No material control regression is established.

The aligned public Run-only phase is also faster:8.254 vs8.874/8.845ms, then8.271 vs8.992/9.082ms. Thus the large adverse15.125ms phase result does not recur in these controls. This does not establish its historical cause. The [original failed conditions and observations](../2026-11-02-direct-string-storage/README.md) are preserved unchanged; no unsupported GC/JIT explanation is offered.

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4, EPYC9V45, actual.NET11RC3, ordinary GC/tiering. Reuse the existing three-image complete-job driver, shared CoreLib/key hashes and identical auxiliary binaries. Two independent processes,256 interleaved warmups/image/case,9 rotating64-job batches, second block reverses initial image order. Timing includes fixture reset, efficient mediated file I/O, complete stdout/file checks and result disposal. Compilation/startup excluded. Run-only clock excludes reset/checks/result disposal, matching the important older phase distinction. Process CPU includes background runtime activity; shared GC means it is not an isolated causal fraction.

Both variants pass409 focused public formatting cases, including protocol/error/Unicode/span/lifetime controls. Every measured job passes full goldens, stable execution balance, compiled cleanup to zero and owned fixture removal. Both quiet gates pass. A setup-only first quiet check failed while compiler servers were active; that original receipt is retained, servers were shut down, and no guest measurement ran during it. Successful owned service105.612s/94.971CPU seconds, exited0, inactive/MainPID0/empty group/lease free.

[All samples and summary](assessment.json), [image/source identities](identity.json), [prospective comparison plan](pre-code-plan.md), [exact source variant](formatting-variant.patch), [driver, raw receipts and failed setup](evidence.tar.gz). This is a bounded before/after comparison, not a refreshed CPython ratio or universal release speedup.
