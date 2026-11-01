# Rejected dictionary-write consolidation (B37)

**Rejected and removed.** Consolidating repeated native dictionary probes misses
the predeclared complete-script CPU target. The identical text controls also
exceed the allowed drift. No retiming, replacement mechanism or speedup claim.

Measured 2026-11-01 on the dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC
9V45 vCPUs / 16 GiB, .NET 11 RC3. Baseline is **8d099a1f**, whose runtime is
unchanged from **ae22392d**; candidate is that source plus [the exact compressed
patch](source.patch.gz). These are historical Lython comparisons, without a
CPython timing baseline.

The hypothesis was that sharing native and already-resolved writes would remove
repeated ContainsKey/IndexOf probes. The prototype deletes 27 net lines without
changing table representation, ownership or public options. Its focused Release
checks pass 308 native / 736 public cases on each actual .NET 11 RC3 and 10.0.8.

## Fixed comparison

[Pre-code criteria](pre-code-plan.md): at least 5% lower CPU against both ordered
baseline controls, at most 2% extra allocation, and no material control-workload
regression. Identical controls allow at most 5% CPU / 0.2% allocation drift;
every measured CPU batch must exceed 100 ms. Existing complete-job workers use
four warmups, seven samples of 128 jobs, ordinary runtime tiering, full golden
verification, shared funding and disposal through completed cleanup.

| Complete script | Before CPU ms/job | Candidate | Identical before | Candidate difference vs both controls |
| --- | ---: | ---: | ---: | --- |
| Text summary, large | 6.4767 | 6.3491 | 6.1120 | -1.97% / +3.88% |
| CSV summary, large | 7.8615 | 7.8803 | 7.8509 | +0.24% / +0.37% |

Text control CPU drift is -5.631%; allocation is effectively unchanged in both
scripts. All adverse wall/CPU/allocation observations are retained in
[acceptance.json](acceptance.json). Six worker processes take 47.494 seconds in
aggregate, 48.843 seconds for collection. Quiet-machine, golden and ownership
gates pass; every worker is joined, the service is inactive and its lease is free.

Fresh nonincremental source builds and all loaded-worker identities distinguish
the intended binaries. Baseline/restored SHA256 is
`b3fb73723e93dd6bdef82cbd8b6dd4885177562f1932dcafe12a395f18e26273`;
candidate is `1c9b4ae6e723f7aa3e34877182b63fc7f9c23ee44e39c35f471b11ae515ee16b`.
Restoration is byte-for-byte and the fresh rebuilt core equals the frozen baseline.

Two independent public key-identity/view/reentrancy cases remain and pass the
restored runtime on both targets. A subsequent mixed native/custom-key probe
exposes a separate correctness defect; its fix is B38, rather than retention of
this rejected optimization.

[Raw results](raw-results.tar.gz), exact identities, build/focused/restoration
logs and the collection drivers preserve the evidence. The archive SHA256 is
`4ef65b0411e94011ee69b8f82465a6a749049b4e6d1f424ae9dc8bd9019cb15d`.
