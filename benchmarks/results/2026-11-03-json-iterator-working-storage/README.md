# Iterator-owned JSON working storage — B154

2026-11-03. Retained and fully qualified41609489628 on exact0f05d7c0.
One governed builder replaces per-next builder/capacity construction. Each yielded
string is an independent funded copy. Exhaustion/failure releases scratch; abandoned
iterators remain charged to their owner. Largest-chunk capacity can remain longer
and cause earlier denial between yields. No serializer, pool, cache, public API or
new ownership infrastructure. Measured config peaks+8B do not bound every possible
largest-buffer retention case. Existing preflights/replacement overlap remain.

Reentrant default callbacks previously consume the next item: ["v"] replaces
["v",2]. Independent CPython3.13.2 rejects nested next with ValueError("generator
already executing"). New sync and genuinely suspended async regressions pass.
A rejected nested next preserves the outer operation; cancellation/refusal keep
priority and native cleanup remains available.

## Measurement

Exact qualified bd3f9dee CPU capture:2048 complete config jobs, actual guest0.89s/
174 samples. Iterator advancement55(31.6%) inclusive; monitor leaves27(15.5%);
unknown19(10.9%). These overlap/include useful work. JIT CPU and a malformed
terminal map line remain; no steady-state or wall-minus-guest-CPU attribution.
Existing perf capture finishes14.803s/exit0, goldens/quiet gates/joins PASS;
[complete maps/chains/raw observations](profile/raw-capture.tar.gz) are preserved.

| Complete job | Elapsed Δ / unchanged control | External process CPU Δ / control |
| --- | ---: | ---: |
| Large config warm |−2.61 /+5.88%|−4.26 /+0.57%|
| Large config fresh |+1.65 /+0.77%|−8.59 /+33.93%|
| Small config warm |+1.59 /−0.41%|+7.30 /+4.15%|
| Copy warm |−0.25 /−1.39%|+1.11 /−0.45%|
| Unchanged report |−2.35 /−1.67%|+2.62 /+7.11%|
| Unchanged JSON |−0.90 /+1.33%|−2.04 /+0.23%|
| Unchanged text |−0.13 /+4.48%|−1.01 /+2.79%|
| Unchanged CSV |+1.89 /+2.57%|+0.52 /+2.63%|

Elapsed improvement is unproven; adverse small/fresh results remain. Process CPU
includes reset/runtime/JIT/response, not guest-only work. Initial64-warmup/eight-job
allocation gives large config−4.36%/control+6.86%, copy control+1.63%: inconclusive.
One bounded confirmation preserves exact images, uses512 warmups/32-job batches
and reversed setup order. No timing replay. Nine rotated batches establish:

| Complete job | Allocation Δ | Control Δ |
| --- | ---: | ---: |
| Large config |−10.500%|+0.0025%|
| Small config |−4.374%|0%|
| Copy/manifest |−1.573%|0%|
| Unchanged report |+0.0021%|−0.00009%|

Only existing private helper warmup/batch constants and purpose label change;
source is retained. Natural GC/tiering, no forced GC or overrides. Initial B150
purpose text remains, while actual catalogue/IDs/hashes define it. Counts include
full public Run/reset/mediated I/O/checks/result disposal, excluding compile/startup.
All images/hash witnesses/goldens/balances/owned cleanup PASS. Confirmation49.141s/
exit0; quiet gates PASS. Its elapsed/CPU is not a performance verdict.

## Source and validation

Baseline bd3f9dee3d0f372f6738c1973cb042bd12903e20, core
578496bb2cd47ba826729dca9fe2cbadf277597caafe518d745e36a2d2e2f92c.
Candidate c53e91beef4596402f61063a5119932025d6f88d15e1fd5fdb8a3e764bf503bd.
[Source hashes/unchanged auxiliary normalization](comparison/identity.json) and
patch identify measured images; committed rebuild metadata may differ. B153 is absent.

Quiet Standard_F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Protocol2,
four-second warmups/image, nine rotated~200ms batches, eight complete jobs.
Existing sources/fixtures/goldens; isolated CPython verifies small config. Timing
excludes staging/startup/transport/final owners, includes actual guest I/O/full
checks/prior-result cleanup; fresh adds compile/dispose. All gates/goldens/joins
PASS,216.824s service/exit0, lease free. No external CPython ratio follows this pair.

Focused Release178 internal/807 public per actual.NET11/.NET10 PASS, covering
chunks/aliases, partial errors/cycles, hooks/suspension, cooperative cancellation,
terminal admission and shared-budget reuse. Initial tests wrongly demanded CPython
fragment segmentation (not a stable json contract); corrected full-text/alias tests
pass baseline. Two remaining baseline failures prove actual reentrant item loss.
Original/corrected failures, independent golden and final logs remain. Existing
public chunk shapes are preserved. Full platform/stress/package checks batch at
delivery. Publication postponed; whole-script performance remains active.

Subsequent [B156 RAM-backed source pair](../2026-11-03-ram-file-checkpoint/README.md)
demonstrates config warm/fresh elapsed−13.00/−10.23%, unchanged controls−0.78/+0.25%.
It uses the same exact source images/API/checks and changes both fixture locations.
This closes elapsed uncertainty for that condition; the original filesystem result
remains unproven. No earlier adverse data are replaced.

Full delivery41609489628 is GREEN on exact0f05d7c0: Windows/Ubuntu ordinary
Release.NET11/10, original Stress and installed consumers all PASS. Every required
step is verified in the cached delivery-ci-status.json under the B154 evidence.
No unchanged replay. Publication remains postponed.
