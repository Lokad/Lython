# Ordinary named calls: closed assessment (B73)

2026-11-01, source081a2280; frozen core source9003d045 and ordinary adapter
d703767c/PythonRe0.4.0. **No runtime prototype selected.** The verified named-call
routing difference is not a demonstrated material complete-workload bottleneck.
This is CPU attribution, not qualified elapsed timing or an updated CPython ratio.

Current positional eligible calls enter the existing child-frame driver. Named
calls use the general binder and a nested executable runner. That binder already
consumes operand spans into final local storage: there is no argument-array copy
to eliminate. Interpreter/function admission and owned arguments remain necessary.

## One fixed diagnostic

Four complete CSV/text helper jobs: original positional spelling and explicit
named spelling, otherwise identical source/fixtures/goldens. All four use the
same frozen worker/core/auxiliary bits, .NET11RC3 on the quiet dedicated Azure VM,
ordinary GC/tiering and the existing checked public worker.128 warmups and0.5s
settling per case,1.5s CPU-clock/fp capture each at199Hz. Total sampled windows
**6.019s**, owned service **20.098s**, within8s/24s. No recapture or tuning.

| Complete script | Positional guest CPU us/job | Named guest CPU us/job | Observed additional CPU |
| --- | ---: | ---: | ---: |
| CSV helpers | 6,712.33 | 6,830.99 | +1.77% |
| Text helpers | 5,082.87 | 5,111.11 | +0.56% |

The predeclared selection gate requires at least15% additional guest CPU/job on
both named variants plus actual named reentry/binder chains. Neither rate reaches
it. These short different-process profiled rates have ongoing JIT, unknown/inlined
frames and independent hash state; they do not prove a stable small surcharge or
predict the gain from rerouting calls. Named reentry is visible in32 CSV/81 text
recorded chains, but a chain's whole body is not removable wrapper cost.

Warm clocks exclude compilation/startup and include mediated file work, fixture
staging, verification and result cleanup. I/O is not separately attributed here;
existing B68 phase records remain explicitly diagnostic. Full isolated pinned
CPython3.13.16 stdout/file goldens, both quiet gates, final zero budgets, joined
users/profilers and removed owned fixtures pass. MainPID0, empty group, lease free.
All per-thread CPU and samples/unknown leaves remain in the summary/raw evidence.

Offline recorded-chain inspection finds CsvDictReader under83/194 positional CSV
samples (69/192 named); parser descendants overlap32/29. This locates a material
record-processing region, not a removable fraction. It motivates examining the
whole native-record construction path rather than another general call tweak.
Sigma-property self leaves remain visible in text, but the already closed B30/
B36/B56 table/context trials are not reopened.

## Initial setup failure

The first827ms service omitted manifest version headers. CPython goldens pass,
but the Lython worker aborts during manifest load, before identity, execution or
any profiler. The failure/empty-response record is preserved under setup-failure
in the raw capture. Only catalog metadata is corrected; scripts, hashes, collector
parameters, bounds and gates are unchanged. The corrected service performs the
sole actual capture. No workload or qualification is restarted after observation.

[All observations and bounds](summary.json), exact source variants, frozen plan,
collector, reference check, worker identities and per-case chain groups are kept.
[Raw capture](raw-capture.tar.gz), SHA256
`0d4a857d90f45eb0c56f4c8413864b3312f0410f6f4f662c44556a0f2155c75d`.
No production dependency/API/test change follows this closed hypothesis.
