# Shared CSV backing: refined comparison and withdrawal

R308 uncommitted frozen277cb5a5 against fully qualified14dd45b7/93a6d134.
The changed-source refinement derives row admission/shared-payload lengths from
completed descriptors, deleting persistent counters and three reset invariants.
Long strings use existing immutable windows; short strings stay inline. No new
ownership infrastructure.117internal/372public Release11 CSV checks pass.

| Whole script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Text-rich invoice warm |−0.64..+1.41%|0..+1.79%|1.15/0.89%;0.91%|
| Text-rich invoice compile + run |+0.65..+2.88%|−0.04..+3.92%|1.61/0.57%;1.09%|
| Original invoice warm |−0.86..+2.16%|0..+1.96%|1.79/1.21%;1.45%|
| Captured text warm |+0.17..+1.76%|+1.22..+3.43%|1.34/0.22%;1.87%|

B664: four jobs169.149s,2026-11-13UTC, same quiet AzureF4as_v7/Ubuntu24.04.4,
fourEPYC9V45vCPUs/16GiB/no swap, ordinary.NET11RC3 GC/tiering, fixedfec79worker
and adapter0.4. Two processes/image, five rotating500ms rounds,5swarmup(12captured).
Ranges span all four paired medians, not confidence intervals. Warm includes
mediated RAM-file I/O, full stdout/file checks and prior-result cleanup; fresh adds
compilation/compiled-owner disposal. Startup/reset/staging/final cache cleanup are
outside; widerCPU includes reset/protocol. No CPython or embedding-latency claim.

All identity/golden/balance/quiet/join/persistent-SHA archive gates pass. No workers
or RAM fixtures remain. Qualified93a6reference/catalog/worker are reused by hard
link. Only superseded initial21d0core aliases are retired after verifying their
bytes in the tracked initial archive, fresh idle lease/inode/hash/UID/all-alias
checks; qualified images and original adverse observations remain. No storage
floor lowered. Final VM free266612736B. An initial source-path preflight error
occurs before staging/deletion; corrected launch uses the qualified reference.

The separate desktop diagnostic produced no first-case observation after roughly
eight minutes; its owned tree was stopped and confirmed gone. Empty/incomplete
outputs survive. It establishes uncertainty, not a runtime failure or an allocation
result. The established−3.50% allocation result belongs to initial21d0, whose
desktop adversity and controlledB663 observations remain separately labelled.

**Decision: withdraw both forms.** Initial backing reduces allocation, but neither
phase establishes useful CPU improvement; the state-removal form adds an extra
descriptor pass and shows adverse fresh execution. Sharing also pins all long
fields when one survives. The small allocation benefit does not currently outweigh
those costs and added materialization branches. This is an evidence-based choice,
not a fixed-percentage veto or permanent ban on bulk CSV construction.

Production CSV is restored exactly to qualified14dd45b7, with no dormant alternate
path. A forced nonincremental Release11 rebuild and117internal/372public CSV checks
pass on restoration. Six useful regressions survive: long UTF8 offsets/aliases,
later rows and scratch cleanup, genuine delayed reads, and admission/cancellation
before final payload allocation/publication. No supported semantics/API change or
new full qualification of unchanged production. The broader redesign objective
continues; a future CSV redesign needs a changed storage/copy premise, not unchanged
retiming. [Initial phase](../initial/README.md), [raw](observations.tgz),
[summary](summary.json), [exact sources/bits/checks/incomplete conditions](source-checks.tar.xz).
