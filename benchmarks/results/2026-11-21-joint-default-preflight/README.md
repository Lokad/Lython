# B1308 mixed-default admission

The private prototype `6e5261808` combines the positional-default and
single keyword-only default preflights in the compiled mixed-default
shape. Each owner keeps its own refund balance. Admission of the full
amount precedes either default expression. A denied request may report
the combined amount.

Keep the prototype private. It removes 2,048 allowance growths per
factory run, including the metadata observer. All five allocation
diagnostics restore the allowance and keep the accounted peaks. CLR
allocation matches for the factories, observer and ordinary helper.
The CSV invoice changes from 3,672,402 to 3,672,522 bytes per execution.
The ordinary helper still makes seven allowance growths.

## Quiet warm execution

On 2026-11-21 UTC, compare `4337b7765` and `6e5261808` on the dedicated
Ubuntu 24.04.4 VM with .NET 11 RC3. Both processes for each image warm
independently for eight seconds. Each worker then contributes seven
rotated batches targeting 650 ms. Warm clocks include mediated output
and prior-result disposal. Compilation, startup and final teardown are
outside these clocks. Negative changes mean less time for the prototype.
Ranges cover all four pairings of per-worker medians.

| Workload | Elapsed change | Process CPU change | Ref / prototype elapsed spread |
|---|---:|---:|---:|
| Eager annotated factories | −6.71 to +1.37% | −6.07 to +1.23% | 5.02 / 3.46% |
| Postponed annotated factories | −5.24 to −2.08% | −5.00 to −1.03% | 1.66 / 1.65% |
| Factories observing default metadata | −2.94 to +0.23% | −2.53 to +1.14% | 1.97 / 1.27% |
| Ordinary fully supplied helper | −2.99 to −2.52% | −3.45 to −2.71% | 0.30 / 0.19% |

The unchanged helper has a stable CPU shift within the postponed
factory range. Eager and observer ranges straddle zero. The collection
therefore establishes the admission-count reduction while leaving the
CPU effect of the changed path unresolved. The unchanged-control shift
is retained with the raw batches and both measured images.

Both images match the CPython goldens and all 112 timed batches pass
their output and fixture checks. The service exits successfully after
221 seconds. Both machine gates pass, all workers join and the guest
fixtures are removed before the verified archive is released. The
prototype passes 5 white-box and 82 public focused Release checks on
each of Windows .NET 11 and Ubuntu .NET 10. The preflight regression
verifies the denial occurs before the first default invokes a host call.

[Prior wider admission experiment](../2026-11-20-default-container-admission/README.md) ·
[Reference core](../2026-11-21-cpython-retained-checkpoint/measured-core.dll.gz).
