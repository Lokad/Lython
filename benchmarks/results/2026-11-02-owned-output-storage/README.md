# Consuming UTF-8 output storage

Measured2026-11-02 on the dedicated Ubuntu/.NET11RC3 VM, source c66253ed (baseline runtime equivalent8f07764a) plus the recorded change. **Retained for allocation/copy simplification; full platform/Stress/installed-package qualification is pending the coherent delivery boundary.**

The common consuming builder conversion transfers its full exact backing instead of allocating/copying another array. Partial buffers still copy, and nonconsuming snapshots stay independent. Casing uses consuming completion. Existing output admission, conservative overlap, result ownership, refusal rollback, current cancellation and cleanup remain; no string view/offset, owner layer, default allowance or backend is added.

| Complete script | Allocation versus both originals | CPU/elapsed interpretation |
| --- | ---: | --- |
| Text helpers |about–2.36%|First block faster; second straddles controls. No established CPU gain.|
| Text uppercase |–10.06%|CPU/elapsed approximately unchanged, small variable points retained.|
| Report rendering / JSON control |approximately unchanged|No consistent material regression.|

Two counterbalanced blocks, three isolated images with an identical original control,256 warmups/image,9 rotating rounds x64 complete jobs/image. Complete public Run/fixture reset/efficient mediated I/O/full stdout+file checks/result disposal included; compilation/startup excluded. Shared CoreLib/hash witnesses/auxiliary bits and ordinary GC/tiering verified. All goldens/balances/owned cleanup and both quiet gates pass. Service153.444s/129.164CPU seconds, naturally terminal/inactive/MainPID0/empty group/lease free. Allocation, CPU and elapsed remain separate observations, not an external comparison or a causal fraction.

1207 internal/3228 public focused Release cases pass on each actual.NET11/10 runtime, including eleven new full/partial/UTF-8/snapshot/reuse/retained-output/refusal controls. The original StringBuilderAdmissionTests caught premature working-storage release on refused consuming string completion. Its assertions remain unchanged; the code preserves the original ownership until native cleanup. Original two failures and the correction are preserved. No timeout or containment assertion was relaxed.

[All observations and uncertainty](assessment.json), [raw evidence](evidence.tar.gz), [exact source/driver](source-and-driver.tar.gz), [validation including initial failure](validation-logs.tar.gz), [ownership correction](ownership-correction.md). Historical CPython tables describe their named revision; this result does not refresh those ratios. The broader whole-script CPU objective remains open.
