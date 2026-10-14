# Complete file copy/checksum/manifest: bounded diagnostic

The accepted Lython runtime ca432c22 copies two files (4,107 input bytes), computes
byte checksums and writes an exact JSON manifest. The binary input includes
0/127/128/255; the second input and filename exercise Unicode. Empty/small/large
outputs match independent goldens and isolated CPython. Runtime, worker and script
trees still match documentation-only HEAD 7736b6c0 at preparation.

| Engine | Median warm complete-job time | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython control 1 | 6.103 ms | 15.1% | Inconclusive diagnostic |
| Lython control 2 | 6.254 ms | 8.0% | Identical binary; a separate control process |
| CPython 3.13.16 | 0.442 ms | 1.6% | External baseline for this diagnostic |

The single 8.321-second comparison uses the existing micro tool. Its two required
Lython roles (`baseline` and `candidate` in [micro.json](micro.json)) point to the
same frozen assemblies; no old/new revision or improvement is being compared.
Both medians remain visible. Keep all seven rounds, warmup, calibration,
verification and loaded identities; no timing samples were replaced or retried.
The measured gap deserves attention, but this diagnostic does not qualify an
exact multiplier, stable execution-only difference or host-latency distribution.

Measured 2026-10-14 on the dedicated Azure Standard_F4as_v7, four AMD EPYC 9V45
vCPUs, 16 GiB RAM, Ubuntu 24.04.4 LTS, .NET SDK 10.0.401/runtime 10.0.12,
CPython 3.13.16 with ordinary GIL/GC and `-I -S -X utf8`. Both workers reuse code
and create fresh invocation state. File reset/staging, script file I/O, capture/
projection and exact stdout/output-file byte checks are inside both warm timers.
Compilation, startup, transport and full response hashing are outside them.
Lython retains ordinary public policies and a shared unbounded allowance;
CPython receives no artificial equivalent containment limit.

One ten-second CPU capture took 14.221 seconds including owned cleanup, with
2,094 samples, zero lost events/missing stacks and 76 unresolved leaves(3.6%).
Background tiered compilation contributes 48.5% inclusive sampled CPU, fixture
reset 15.6%, and guest execution 23.7%. Inclusive shares overlap and are process
CPU attribution, not a decomposition of elapsed time. They do not justify a
specific dominant runtime removal or explain the measured elapsed gap by
themselves. The [profile](profile.json) preserves source manifests, trace hash,
829 checked responses and 1,658 exact complete invocations. All worker/tracer
users exited, staging was removed, services are inactive and the lease is free.

Stopping criterion reached: no speculative runtime branch, changed JIT/GC policy,
longer warmup, fresh capture or wider qualification campaign. The investigation
keeps the representative workload and records its uncertainty. There is no
runtime performance improvement claimed for this family. Existing focused
public file/representative, retained-buffer rejection and protocol cancellation
controls pass 128/128 in 12.614 seconds including cleanup. The original stress
scales remain separate; protection and refusal assertions are unchanged.
