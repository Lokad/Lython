# B1469 Retained factory CPU attribution

The quiet Ubuntu Azure F4as_v7 profiled retained source `6e01ec965` on
November 23, 2026 UTC, using the same 32-file .NET 11 RC3 image as B1468.
Each of four independent workers verified its CPython golden before and
after capture and warmed for eight seconds. Linux perf sampled CPU-clock
at 199 Hz over about five seconds of complete repeated batches. Its
process boundary includes validation, protocol and cleanup. Only the
postponed fixture includes compilation. This is diagnostic attribution.

| Nearest resolved runtime method or admission caller | Postponed time to result | Eager warm | Defaults observer warm | Fully supplied warm |
| --- | ---: | ---: | ---: | ---: |
| All process samples | 1,006 | 990 | 1,001 | 983 |
| Runtime ancestry | 995 | 976 | 998 | 975 |
| Unmapped leaf | 80 | 82 | 52 | 57 |
| Instruction prefix | 154 | 144 | 202 | 261 |
| Complete result consumer | 77 | 70 | 47 | 87 |
| Native definition dispatcher | 117 | 106 | 56 | 0 |
| MemoryGovernor.Reserve | 96 | 95 | 46 | 1 |
| ↳ Callable creation | 57 | 40 | 31 | 0 |
| ↳ Default construction | 34 | 31 | 13 | 0 |
| ↳ Eager annotations | 0 | 21 | 0 | 0 |

An earlier postponed sample found 72 callable and 32 default
reservations out of 979 resolved runtime stacks. Its next worker
timed out waiting for the profiler control acknowledgment and left
three diagnostic IPC endpoints on the temporary mount. Every process
joined. Recovery preserved and hashed the original evidence and
recorded the endpoints before unmounting. The second run waits for
profiler readiness and retains the same image, fixtures and capture
boundary. Its full archive verifies and all original cleanup gates pass.

The factory retains definitions and their defaults as guest values.
Admission coalescing is an open design question and must preserve
the early preflight, side-effect order and pending ownership. Earlier
default-container combinations had mixed or adverse whole-script
results. The two factory helpers each immediately return the function
they just stored in a local. Eliding a terminal private store could
remove that binding handoff without altering allocation or admission.
Test that hypothesis through existing lowering, retain captured or
observable stores, and measure complete scripts. Inclusive invocation
totals include most work performed inside the helper, so they do not
estimate the removable CPU cost.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Raw confirmation](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Original partial attribution](original-partial-attribution.json) ·
[Failed run](original-failed-observations.tar.xz) ·
[Recovery receipt](failure-recovery.json) · [Procedures](procedures.tar.xz).
