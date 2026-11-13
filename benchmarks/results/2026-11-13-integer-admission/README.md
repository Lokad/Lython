# Pre-admitted integer arithmetic

R305 retains frozen3a2c1cf6 against qualified63389c9d/2e74ef4f. All572production
inputs and both cores are frozen; source checks/failed conditions survive in
[source evidence](source-checks.tar.xz). This is a demonstrated admission correction
and shared-kernel simplification, with supporting complete-invoice performance.

Before correction, direct arithmetic on funded524289/257-bit inputs allocates
~64KiB for subtract/multiply and~128KiB for floor division/modulo before refusal.
Addition refuses after32diagnostic bytes. All eight sync/async regression cases
fail before correction; all affected operators now refuse after32bytes in the same
probe. This diagnostic is allocation at first denial, not a CPU measurement.

One integer kernel replaces generic integer branches for subtraction/multiplication/
floor division/remainder, from exact dispatch and bool/ingress fallback. math.prod,
sumprod and Counter subtraction share it. Removed four ungoverned generic numeric
methods, duplicate prod admission, post-allocation result funding and modulo's full
quotient/product/difference. Existing addition/power/shift protection remains.
The surviving result is committed from pre-admitted workspace; scratch refunds do
not retire input/result aliases. Large division conservatively admits working
capacity even for a tiny remainder and may reject earlier. Individual managed BCL
arithmetic cannot be cancelled internally; no hard-deadline claim.

115internal and1049public focused actual.NET11 Release cases pass. New24internal
cases cover denial before allocation, retained aliases and cancellation during host
admission. Both public APIs preserve signed arithmetic, bool/float, reflected
operators, augmented evaluation order, zero errors and accumulator behavior; isolated
local CPython confirms the independent semantic script. All eight existing complete
scripts pass both APIs with full output/ownership/cleanup. Allocation is effectively
flat; desktop clocks have moving controls and establish no CPU result.

| Complete script | Elapsed change | Wider process CPU | Reference/candidate control; max IQR |
| --- | ---: | ---: | --- |
| Invoice warm |−8.22..−7.16%|−8.25..−7.75%|1.07/0.06%;1.17%|
| Invoice compile + run |−9.29..−7.19%|−8.33..−6.50%|1.09/1.20%;1.55%|
| Captured text warm |−5.36..+0.70%|−5.10..+1.17%|2.13/3.97%;1.14%|
| JSON helpers warm |−0.90..+0.99%|−1.48..+1.26%|0.82/1.08%;1.84%|

Quiet Azure F4as_v7, Ubuntu24.04.4, four EPYC9V45vCPUs/16GiB/no swap,
actual.NET11RC3 ordinary GC/tiering,2026-11-13UTC. Common workerfec79afd, unchanged
eight-case catalog and adapter0.4. Four jobs complete179.626s under300s owner.
All identity, golden, shared-budget cleanup, quiet/join and persistent SHA-archive
gates pass; no remaining workers or RAM fixtures. Five rotated500ms rounds,
5s warmups (12s captured,8s JSON), two processes per image. Ranges are all four
process-median pairings, not confidence intervals. Wider CPU includes protocol/reset.
Warm excludes compilation/startup, includes actual mediated RAM-file I/O, complete
output checks and prior-result cleanup. Fresh additionally includes compilation and
compiled-owner disposal. Fixture reset/staging and final cache teardown are outside.

Text/JSON cross zero: no gain or regression resolved. Keep every adverse original;
no further retiming is needed for this retain decision. No external ratio is inferred
by multiplying source-pair gains into older CPython results. Full Windows/Ubuntu,
actual.NET11/10, original stress and installed Release package qualification is
GREEN43384587266 for exactc5d05cff/3a2c1cf6. [Cached delivery](delivery/README.md).
Publication remains postponed; this milestone does not complete the broader roadmap.

[Computed rows](summary.json), [raw observations](observations.tgz),
[archive receipt](archive-receipt.json), [focused validation/source identity](validation.json).
