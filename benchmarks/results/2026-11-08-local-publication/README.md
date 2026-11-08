# Deferred local-name publication prototype

2026-11-08. Baseline `05790317` / core `13107e6b`; prototype over `7bd69241`,
core `761c30f7`. Only `StoreLocalValue` changed: defer immutable compiler-name
lookup/classification to actual global/mirroring branches. Cell writes, lifetime
fences, refusal/cancellation checks and cleanup were unchanged. No API change.
446 public /132 internal focused Release tests pass per actual .NET11/.NET10.
Prototype was not fully qualified and is **not retained**.

Normal-settings same-process diagnostics are allocation-neutral across the
integer loop and complete CSV/text/JSON/report scripts, both host APIs. Their
elapsed observations are mixed and remain in the archive.

B472 uses the existing quiet F4as_v7 VM, frozen worker7cf/optional0.4, normal
GC/tiering, two isolated roles per core, five-second warmup and seven rotated
150ms rounds. Six jobs/167.915s; full stdout/file goldens, ownership baselines,
image isolation, quiet endpoints, joined workers, fixture cleanup and local
archive verification all pass. Warm excludes compilation/startup/protocol;
includes invocation, I/O for file cases, validation and prior-result cleanup.
The maintained16384-iteration loop has no guest file I/O; empty file maps adapt
it to the existing file-capable diagnostic driver. No new benchmark framework.

| Workload | Elapsed change across all role pairings | CPU change |
| --- | ---: | ---: |
| Integer loop, synchronous |−3.98% to+3.70%|−3.57% to+3.30%|
| Integer loop, async API |−3.22% to−0.20%|−3.40% to−1.48%|
| CSV |−4.79% to+1.53%|0% to+1.82%|
| Text |−3.01% to+0.05%|−2.38% to0%|
| JSON |−1.61% to+1.02%|−2.44% to0%|
| Report |−0.29% to+1.70%|+1.42% to+3.84%|

Negative is faster. The synchronous-loop candidate roles differ5.79%; CSV old
roles differ4.33%. These cases are uncertain. Async loop supports a small gain;
report has possible CPU debt. No general CPU gain or CPython refresh follows.

Decision: restore the simpler canonical implementation. This pure relocation
adds duplicated namespace branch structure, with no allocation reduction and no
convincing whole-script benefit. This is a complexity/evidence tradeoff, not a
fixed percentage veto or permanent rejection of publication redesign. Preserve
the profile lesson: publication includes necessary checks/retirement and cannot
be removed solely from its inclusive or leaf sample share.

[Decision and qualification](decision.json), [all controlled roles](assessment.json),
[identity](identity.json), [archive receipt](archive-receipt.json),
[sources, binaries, diagnostics and raw VM archive](observations.tar.xz).
