# B1708b Prepare the short workspace at iterator creation

Commit `7d24cfe9a` proves the physical footprint once for the fixed map
or starmap callable. Unrelated compilation uses the original path. The
short iteration buffer now holds both the arguments and native temporaries.
The callee runs the existing native instructions. A deferred operation
admits the full normal frame and transfers its live slots and instruction
position before resumption.

The reference is `008546a1f`. All 32 files in both images match their
recorded identities, with the core as the only difference. Both images
verify five CPython goldens. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 25, 2026 UTC. Each image has two workers
with eight seconds of warmup and seven rotated batches per phase.
Ranges span the four pairings of worker medians.

Warm elapsed includes applicable mediated RAM-backed I/O, verification
and result disposal. Through result also includes compilation and disposal
of the compiled owner. Wider CPU includes fixture reset and request
framing. Startup and final process teardown sit outside both clocks.

| Workload | Phase | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| File-fed map | warm | -13.01% to -7.10% | -13.95% to -6.26% | 6.19% / 0.56% |
| Shipment report | warm-async | -2.41% to +0.60% | -3.41% to -0.19% | 0.01% / 3.08% |
| File-fed map | warm-async | -10.10% to -0.19% | -10.09% to -0.74% | 9.14% / 1.72% |
| Ordinary definitions | compile-run | -0.48% to +2.65% | +0.00% to +1.11% | 0.71% / 2.42% |
| Defaulted rules | compile-run | -1.48% to -0.19% | -2.47% to -0.90% | 0.81% / 0.49% |

The file-fed sync lane uses 6.26–13.95% less wider CPU. Its reference-worker
elapsed spread of 6.19% limits the magnitude estimate. The async lane
moves favorably on both clocks with 9.14% reference-worker spread.
Shipment and defaulted-rule CPU move in the opposite direction from the
first experiment. The effects on those controls remain unresolved.
Generated ordinary definitions span zero on elapsed with up to 1.11%
more CPU. Retain the removed workspaces and preserve the first block and
the control uncertainty.

Instrumented runs show 2,048 workspace starts removed per file-fed
invocation in both hosting modes. Separate CPython-checked map and
starmap diagnostics show one loan and one transfer of three live slots
on late binding. Admitted peaks and retained compilation match. The
execution allocation diagnostic differs by 8 B on file-fed map and 88 B
on shipment.

Windows .NET 11 and Ubuntu .NET 10 each pass 4,610 focused Release
checks. The regressions include a genuinely suspended replacement
callable, retained alias identity, one call, completion after cleanup,
cancellation and terminal refusal of the owned frame. Both quiet gates
and all 140 timed batches passed. Workers joined, the fixtures retired,
and the persistent observations were verified under the lease.

[Image and source](identity.json) · [Timings](quiet-summary.json) ·
[Routes](route-summary.json) · [Execution allocation](allocation-summary.json) ·
[Compilation allocation](compile-allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Initial experiment](../README.md).
