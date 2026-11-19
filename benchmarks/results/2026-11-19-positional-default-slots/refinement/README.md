# B1101 direct reads from the positional-default tuple

R670 (`bc65698dd`) builds the final tuple from the evaluated slots under its
existing reservation and binds from the live tuple by its tail position. This
removes repeated tuple-like dispatch, counts and checks. Both native tuple
indexing and binding borrow from the same integer storage. The measured source
and tests match `accd3c2d5`. The reference image `3156df3b9` matches the
delivered source at `f8a7920e7`.

Both local targets passed 438 focused Release checks and both 32-file images
matched seven CPython goldens. Ordinary rule allocation falls 3.62% and
annotated rule allocation falls 2.89%. Their accounted peaks rise 5.95% and
3.47% because the slot-backed tuples fund wider capacity and eventual boxes.
The computed-default fixture saves 24 B with an accounted peak 40 B higher.
All allowance baselines recover. The original allocation and test context
are in [B1099](../README.md).

Using the same quiet VM, catalog, phases and 12-second warmup as the
[B1100 confirmation](../confirmation/README.md), the measured source pair
had these ranges across the four combinations of duplicate medians.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Computed defaults | Warm | −0.65…+1.25% | +0.69…+1.97% |
| Computed defaults | `RunAsync` warm | −6.79…+1.17% | −6.38…−0.54% |
| Ordinary rules | Warm | −3.94…+1.84% | −2.03…+1.58% |
| Captured rules | Warm | −5.20…+2.04% | −4.86…+3.83% |
| Constant defaults | Warm | −9.68…−1.84% | −8.72…−1.93% |

Retain the simpler construction and binding together with lower rule
allocation. Warm timing for ordinary and captured rules remains mixed on
this source pair. Constant defaults and asynchronous computed defaults use
less CPU, with reference process spreads of 8.64% and 5.47% in elapsed
respectively. Synchronous computed defaults use more CPU with close controls.
Recheck these workloads with the next combined delivery before attributing
a broader CPU improvement. The architecture accepts the measured increase
in conservative admitted capacity while keeping live-memory refusal in force.

All 100 measured batches passed in 309.84 seconds. Both quiet gates, worker
joins, fixture removal and archive verification passed. See B1099 for the
command boundary and the earlier adverse conditions.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) · [Lease receipt](archive-receipt.json).
