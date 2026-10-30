# Native JSON writer and representative scripts

Measured once on **2026-10-30**. Candidate source
**4433986e6d18c7aaa26bbee5d286e8b0ab9f7cf4** replaces eager/incremental JSON's
UTF-16 builder and growth accounting with the existing governed UTF-8 builder.
Control **e81b7a54a3c1988e3b3a3db9bd9e1cbb3b52a260** uses the same worker,
scripts, inputs and .NET runtime. CPython is the primary external baseline.
All observations are **diagnostic and unqualified**; no repeat collection or
favorable sample selection. The fixed seven-job collection took **57.57 seconds**.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 x86-64, four AMD EPYC
9V45 vCPUs, one thread/core, 16 GiB RAM, no swap. SDK/runtime:
11.0.100-rc.3.26522.148 / 11.0.0-rc.3.26522.148. Reference: CPython 3.13.16,
GCC 13.3, PGO/LTO, ordinary GIL/GC, no debug/JIT, launched with -I -S. File jobs
also select UTF-8 mode. Both quiet-machine gates pass; observable pressure,
paging, steal and parent-cgroup throttling counters are preserved in raw evidence.
The child service's unavailable throttling counter remains explicitly unavailable.

## CPython comparison

These six rows measure fresh compilation plus one complete synchronous execution
inside already-running workers. Includes invocation setup, fixture reset/staging,
real filesystem work, full output checks and the worker's declared ownership
cleanup. Excludes startup, transport and response digest encoding. Efficient
opened-file reads/writes are supplied by the comparison host. Ordinary worker GC
and tiering remain enabled. Warmup is one second per engine; seven rotating-order
batches target 25 ms each. IQR means interquartile range divided by median.
Ratio = candidate Lython median / CPython median; above one means slower.

| Workload | Input bytes | Lython ms | CPython ms | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: | --- |
| CSV helpers, 4,096 records | 57,361 | 8.559 | 3.756 | 2.28x | 2.3% / 1.4% |
| Text helpers, 3,072 lines | 35,840 | 6.607 | 1.481 | 4.46x | 3.2% / 1.2% |
| JSON-lines helpers, 4,096 records | 110,592 | 9.260 | 4.689 | 1.97x | 7.6% / 0.6% |
| Config read/edit/write, 16 paths | 181 | 3.057 | 1.568 | 1.95x | 4.4% / 6.4% |
| Copy/checksum/manifest, two files | 4,107 | 2.337 | 0.598 | 3.91x | 6.5% / 0.8% |
| File search with early exit | 42,010 | 2.307 | 0.394 | — | 146.9% / 2.1% |

The search row remains inconclusive. Quiet machine gates do not remove internal
scheduling, JIT or GC variation; these are hypotheses, not measured attribution.
No comparison against separately collected da0ff811 medians establishes a speedup.

## Earlier Lython control, separately

These are same-runtime old/new observations. The JSON-lines point median rises;
the table preserves that result alongside reductions and unchanged rows. The
collection does not establish a repeatable overall elapsed improvement.

| Workload | e81b7a54 ms | 4433986e ms | Old / new IQR |
| --- | ---: | ---: | --- |
| CSV helpers | 8.574 | 8.559 | 11.2% / 2.3% |
| Text helpers | 7.187 | 6.607 | 11.1% / 3.2% |
| JSON-lines helpers | 8.623 | 9.260 | 8.1% / 7.6% |
| Config edit, small | 3.269 | 3.057 | 8.3% / 4.4% |
| File manifest helpers | 2.325 | 2.337 | 12.3% / 6.5% |
| Early-exit file search | 2.211 | 2.307 | 120.2% / 146.9% |

One separately selected affected workload measures **warm** config read/edit/write
with 1,024 paths / 7,177 input bytes, reusing compiled code. It includes invocation,
file work and declared cleanup, excluding compilation and startup:

| Boundary | Old Lython ms | New Lython ms | CPython ms | Old / new / Python IQR |
| --- | ---: | ---: | ---: | --- |
| Warm large config | 3.639 | 3.549 | 1.787 | 30.9% / 36.3% / 2.2% |

This affected-workload timing is inconclusive. Source inspection proves removal
of whole-result UTF-16 staging, text/key/indent decoding and JsonGrowthCharge;
it does not prove an allocation amount or elapsed speedup. Large integer and
Python float rendering still use managed text renderers. Small-result allocation
and admission thresholds may change. Capacity/replacement/final-copy funding,
hooks, escaping, chunks, cancellation and deterministic cleanup remain active.

## Correctness and reproduction

All seven timed jobs verify complete stdout and file goldens before and after
timing in both Lython sources and CPython. A separate supervised check of all 33
representative cases completes with zero failed cases, including warm execution,
fresh compilation/run and compilation-only timer smoke. Its two-iteration batches
are correctness evidence, not qualified timing. Focused local Release checks pass
175 native and 1,032 public cases per target (.NET 11 and .NET 10), including actual
suspension, refusal, retained aliases/results and budget reuse. Platform/package
qualification is reported separately in PLAN.md.

[collect.py](collect.py) reuses the existing fixed-collection driver and comparison
commands. It holds the shared VM lease; each diagnostic runs in an owned systemd
service with a 30-second external hard deadline and process-group cleanup. A
prospective 90-second collection budget leaves cleanup headroom. No runtime
quotas or benchmark framework are introduced. Tool paths identify the actual
toolchains; the script's worker directories describe this preserved experiment.

[raw-results.tar.gz](raw-results.tar.gz) preserves the full catalog, source/input/
output hashes, loaded worker/library identities, every response and sample,
quiet-machine counters, machine/toolchain inventory, correctness evidence and
collection logs. Baseline core SHA-256:
58fb53d73458a27da3cfafe8d7f72ebda9f78bea5a69152fe9e11de1225d6e92.
Candidate core SHA-256:
eaacb359d506bcc4495eb7e98f65836cc811e542e27a11fc4c7e39d484082c66.
