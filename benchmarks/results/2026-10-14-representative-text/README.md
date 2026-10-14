# Complete Unicode text summary: rejected ASCII sigma shortcut

The complete job reads 35,840 UTF-8 bytes (1,024 fixture repeats), lowercases and
splits lines, counts 6,144 words, prints the total and writes the exact sorted
report including accented text and emoji. Baseline is committed ca432c22; the
candidate changes only the internal final-sigma property lookup to classify ASCII
directly. Generated Unicode ranges, allocation/work checkpoints and all other
runtime/worker sources remain identical. The candidate is rejected and removed:
the single comparison is too noisy to establish a reliable elapsed improvement.

| Engine | Median warm complete-job time | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython ca432c22 | 8.018 ms | 3.6% | Short diagnostic |
| Rejected ASCII property candidate | 7.013 ms | 10.7% | Nominal 12.5% lower; inconclusive, includes a slow outlier |
| CPython 3.13.16 | 1.258 ms | 2.0% | External baseline for this diagnostic |

The one comparison took 8.930 seconds including owned cleanup. All seven rounds,
calibration, warmup, verification and loaded identities remain in
[micro.json](micro.json). No samples were replaced or repeated; no qualified
multiplier, overall speedup or host-latency claim follows. The
[rejected seven-line shortcut](rejected-candidate.patch) is evidence, not a runtime
change. Its [source identity](candidate-sources.json) and
[compiled input hashes](candidate-inputs.sha256) identify the measured candidate.

Measured 2026-10-14 on the dedicated Azure Standard_F4as_v7, four AMD EPYC 9V45
vCPUs, 16 GiB RAM, Ubuntu 24.04.4 LTS, .NET SDK 10.0.401/runtime 10.0.12,
CPython 3.13.16 with ordinary GIL/GC and `-I -S -X utf8`. Both engines reuse
compiled code, create fresh guest state and use identical file staging/reset and
independent stdout/report goldens. The warm boundary includes file reset/staging, complete execution,
capture/projection and exact stdout/output-file byte checks. Compilation, startup,
transport and full response hashing are outside it. Lython retains ordinary
public policy limits and a shared unbounded host allowance. No artificial CPython
containment limit is imposed.

One ten-second CPU capture took 14.521 seconds including owned cleanup:
6,213 samples, no lost events or missing stacks, 270 unresolved leaves (4.3%).
Final-sigma property lookup contributes 6.9% sampled CPU and lower-case mapping
15.6% inclusive; inclusive shares overlap. This supports investigating the lookup,
but does not establish how much total job time it can remove. Exact checked
responses, source manifests, trace hash and attribution are in
[profile.json](profile.json). All worker/tracer users exited, file staging was
removed, the services are inactive and the comparison lease is free.

The independent regression checks every ASCII scalar in four preceding/following
sigma contexts against CPython 3.13.2 output, through sync and async public runs.
It also checks result disposal returns the shared allowance to the compilation
baseline. Existing Unicode range boundaries, long ignorable runs and casing
admission/rollback controls remain. The accepted source passes 46 focused white-box
checks and 63 public checks, each owned group under four seconds. Production
runtime source is restored exactly to ca432c22; useful compatibility coverage is
retained. No new benchmark framework, data table, unsafe API, cache or configuration
switch is introduced.
