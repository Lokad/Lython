# Parsed numbers shared by compiler consumers

R532/B962 compared the qualified 2c6b304b core with the private numeric facts
change on 2026-11-18 UTC. Parsing produces typed integer, floating and imaginary
values for known numeric tokens. Analysis, annotation rendering, executable
emission and fallback evaluation reuse those values. Abstract analysis keeps its
decimal eligibility and spelling distinctions where those affect diagnostics.
Inferred flags and byte values stay numeric. Prefixed integer parsing packs the
validated digits into unsigned magnitude bytes and checks cancellation while
walking long inputs.

Both copied 32-file images passed the four-case timing catalog and 13-case
output, allocation and allowance reuse checks. The images differ in the core
assembly alone. The 26-case reference matrix matched both execution APIs on
actual .NET 11 and .NET 10. Each target passed 52 white-box and 803 public
focused Release tests, including a regression for large binary, octal and
hexadecimal magnitudes that cross byte boundaries.

The quiet VM completed six jobs in 278.71 seconds. Each job used two processes
per revision, eight seconds of rotated warmup per process and five measured
rounds. The table reports the range across process-median pairings, in percent.
Negative numbers favor the prototype.

| Workload and phase | Elapsed change | Process CPU change | Same-image elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Config edit, compile | −0.43 to −0.01 | −0.94 to +1.96 | 0.32 / 0.10 |
| Config edit, compile and run | −1.57 to +1.58 | −2.91 to +0.08 | 0.13 / 3.07 |
| CSV invoice, compile | −1.99 to −0.01 | −1.01 to +0.69 | 1.42 / 0.59 |
| CSV invoice, compile and run | −1.83 to +2.29 | −5.24 to +1.96 | 3.34 / 0.83 |
| Report rendering, compile | −0.77 to +1.03 | −1.10 to +1.58 | 0.52 / 1.29 |
| Helper text, warm control | −1.24 to +0.88 | −0.44 to +1.75 | 0.70 / 1.44 |

Warm execution includes mediated RAM I/O, output checks and prior-result
cleanup. Compile and run adds compilation and compiled-owner disposal. Process
CPU also includes fixture reset and protocol work. Compilation allocation on the
local current-thread driver changed by −152 bytes for CSV, −16 for config, −8
for report and +8 for helper text. Execution peaks and ownership balances
matched across all 13 verification scripts.

Retain the change for simplifying compiler value transport. Config and CSV
compile elapsed measurements are slightly favorable, with mixed CPU results.
The source pair leaves whole-script CPU improvement unresolved. Full delivery
checks apply to the integrated source. Publication remains postponed.

The first staging calculation fell short of the disk reserve and stopped before
changing the VM. The second verified an additional archived duplicate under the
same lease. All workers joined, RAM fixtures were removed and the observation
archive was verified.

[Timing summary](summary.json) · [Compile allocation](compile-allocation-summary.json) ·
[Execution allocation](allocation-summary.json) · [Raw observations](observations.tgz) ·
[Verification procedures and logs](procedures-and-checks.tar.xz)
