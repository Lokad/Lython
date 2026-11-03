# Unchanged separate-process controls (B179)

Measured 2026-11-03, runtime source 7c183083, frozen core
`bf2ddeb1d6adb8b4dccda00e0980225502b3a5a92e8aec019ab753ac3a6e69ed`.
Three independent Lython workers per case use the SAME physical frozen worker
image; names are roles, not source differences. No CPython or speedup claim.
Quiet Standard_F4as_v7 Ubuntu VM, .NET11 RC3, normal workstation GC/tiering.

| Warm synchronous complete job | Before ms | Candidate role ms | After ms | Within-worker IQR/median |
| --- | ---: | ---: | ---: | --- |
| CSV, 4,096 records | 5.256898 | 5.221601 | 5.282909 | 0.78/0.99/1.03% |
| Text, 3,072 lines | 3.269552 | 3.318389 | 3.203128 | 0.39/0.41/0.75% |

The CSV range is1.17%; text3.60% relative to the fastest worker, despite small
within-process IQR. Separate processes avoid shared-ALC type-profile interaction,
but per-process hash/layout/tiering differences remain possible, unproven causes.
The two methods are not pooled. Earlier ALC controls moved5–8% in other jobs;
this bounded check neither establishes universal superiority nor explains them.

Decision: use existing separate workers with an unchanged control for the next
source pair. Record both source/control effects; small elapsed changes remain
uncertain. Allocation reduction, source simplification and CPU improvement are
separate findings. No unchanged retime is selected.

Existing protocol2, three seconds adaptive warmup per worker, nine rotating-order
batches targeting200ms, one-second settle. Warm compilation/startup/fixture reset
are outside timing; invocation setup, actual tmpfs file I/O, prior-result disposal
and complete output/file verification are included. Final result/program/fixture
teardown outside the clock is joined. Catalog/output/identity hashes, every warmup
and sample, before/after quiet gates and filesystem are retained. All goldens,
worker joins and gates passed; collection34.461s, owned service35.064s exit0.
Evidence lives in persistent home storage; only guest fixtures use RAM.

Original launch failed before any worker because a Windows-written shell wrapper
contained CRLF (`set: Illegal option`). Its journal is retained. Corrected launch
invoked Python directly under the same owned systemd/lease boundary; no timing
sample was discarded. No runtime or VM policy changed.
