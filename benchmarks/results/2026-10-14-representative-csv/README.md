# Complete CSV summary: profile and rejected candidate

This is one bounded diagnostic on 2026-10-14, not a qualified speedup or CPython
multiplier. The complete script filters and aggregates 4,096 data records from
57,361 UTF-8 input bytes, then writes the exact sorted report and prints `3072`.
The accepted runtime is producer `f95fb734`, production tree
`8d517bc1549b6ab4e904bcbf1df223099afea212`. A subsequent parser experiment was
rejected and removed. [Raw paired responses and identities](micro.json) preserve
all samples; [CPU attribution and capture identities](profile.json) preserve the
single trace's evidence.

| Engine | Median warm time per complete job | IQR / median | Status |
| --- | ---: | ---: | --- |
| Accepted Lython baseline | 7.530 ms | 15.4% | Inconclusive diagnostic |
| Unquoted-run candidate | 7.362 ms | 6.5% | Rejected: nominal 2.2% difference is smaller than baseline spread |
| CPython 3.13.16 | 3.483 ms | 0.6% | External baseline for this diagnostic only |

The machine is the dedicated Linux x64 benchmark VM: Ubuntu 24.04.4 LTS,
four AMD EPYC 9V45 vCPUs, .NET SDK 10.0.401/runtime 10.0.12. Both engines reuse
compiled code with fresh guest state. Both timers include file reset/staging,
complete execution/capture and exact output-file checks. Compilation, startup,
transport and response hashing stay outside the warm timer. CPython uses
`-I -S -X utf8`, ordinary GIL/GC and its recorded PGO/LTO build. Lython uses
ordinary per-run policies and one unbounded shared allowance, with prior-result
and temporary-compilation cleanup timed and final response owners released after
writing. No fictitious equivalent CPython resource limits were introduced.

The one ten-second kernel CPU capture completed in 14.635 seconds including
startup/cleanup. It has 6,445 samples, no lost events or missing stacks, and 297
unresolved leaves (4.6%). All 654 responses and 1,306 complete invocations pass
independent stdout/file goldens. Local verification covers all 12 representative
cases against CPython 3.13.2; VM preflight covers all four small jobs plus the large
CSV job against CPython 3.13.16. Both file workers use UTF-8.

CSV dictionary iteration appears in 31.0% of inclusive stacks; parser `Feed`
appears in 15.0%. These overlap. The main interpreter has 7.5% of leaf samples,
parser `Feed` 4.3%, and CLR monitor exit/enter together 6.4%. Inlining and native
leaves prevent treating those percentages as predicted savings. Recorded GC pairs
are incomplete and establish neither an embedding pause distribution nor latency.

The [rejected candidate](rejected-candidate.patch) appended unquoted ordinary text
runs within already-funded field capacity. It preserved original growth/admission
boundaries and token/error handling, with fallback for escapes/non-BMP dialects.
260 public and 57 white CSV/representative controls passed, including alias,
async/cancellation, retained-data, Unicode and denial checks. An additional
funded-capacity failure/resumption regression remains useful on the accepted
parser. The candidate's nominal gain is unproved, so its runtime branch is gone.
Do not recollect this producer to replace noise or restore the optimization from
these results.

One initial timing launch failed before workers started because its uploaded shell
script passed a carriage return in the case ID; no timing receipt or samples existed.
After fixing that preparation error, the sole measurement completed in 8.725 seconds.
A wrong-PID offline parse was corrected against the same trace, without recapture.
The private failures, raw trace and full receipts remain preserved. All owned
workers/tracers exited, temporary file staging was removed, the services are inactive
and the VM lease is free. No ten-minute lane or broader qualification ran.
