# Fresh compile-to-result JSON job — 2026-10-16

One existing complete JSON-lines aggregation job inside already-running hosts:

| Engine | Code | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython control 1 | 1ad18b79f57421e15df10188ff6ec83f86c98d88 | 9.880 ms | 21.3% |
| Lython control 2 | The identical binary and source | 10.024 ms | 8.7% |
| CPython | 3.13.16 | 4.359 ms | 0.7% |

**These are equivalent Lython controls, not before/after versions.** The producer's
internal baseline/candidate labels identify separate worker processes executing
the same binary. The range 9.880–10.024 ms contains their two medians, not a
confidence interval. No qualified multiplier or runtime improvement is established.
There was one capture, no repeat, other lane, tuning or new producer/framework.

Each timed job freshly compiles the unchanged script and executes it with fresh
job state. Timers include Lython frontend/lowering/executable construction or
CPython compile(), imports, disk fixture reset/staging, the complete JSON parse/
filter/aggregation/report job, output byte/stdout checks, projection and existing
previous-result cleanup. The host processes and toolchains are warm. Startup,
transport and full output hashing are excluded. The existing compile-run lane
was reused without source changes; see the [boundary gate](boundary-source-gate.json).

This fills a missing AP00 boundary for scripts compiled and run once. It does not
isolate compilation time. Historical warm captures are separate measurements;
subtracting them would not produce a valid compilation-cost estimate. No code
caching assumption was introduced and no runtime code changed for this capture.
Other workloads' fresh/compile-only/startup gaps remain open.

The unchanged scripts.json-summary.large reads 4,096 records / 110,592 UTF-8
bytes, filters negative amounts, aggregates by kind, writes exact JSON and prints
the accepted count. Both equivalent control verification runs match independent
stdout/file goldens at all three existing sizes:
[control 1](baseline-correctness.json), [control 2](candidate-correctness.json).
The compile-run warmup/calibration/measured jobs retain the producer's existing
per-job exact output checks.

Measured on dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
ordinary GIL/GC and -I -S. Normal Lython GC/tiering, cooperative allowance,
preflights, cancellation and managed stack admission stay enabled; fuel unset.
CPython has no equivalent in-process containment. This diagnostic has no noise
or host-latency qualification and supplies no qualified overall ratio.

[Raw timing](compile-run.json) retains every attempt, version and runtime policy;
[declaration](comparison-declaration.json) freezes source/archive manifests.
[Cleanup/source proof](cleanup-source-proof.json) verifies all 524 production
sources and comparison/Python sources, identical loaded Lython library identity,
seven exited workers, seven removed file-fixture directories, inactive services
and a free exclusive lease. The owned capture finished in 9.364 s including
cleanup. The measured runtime is the source qualified by
[Windows/Ubuntu Release suites and package consumers](https://github.com/Lokad/Lython/actions/runs/38863015180).
No new runtime tests or Stress run were needed for this measurement-only change.
