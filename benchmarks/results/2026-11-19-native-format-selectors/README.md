# B1045 and B1046 native format selectors

The retained runtime `9ac02d15d` stores UTF-8 bracketed string keys and their
hashes in the admitted format layout. Exact native dictionaries use those keys
inside the ordered rendering loop. Generic subscripts and missing-key errors
receive fresh guest keys. The change removes per-lookup conversion on the
native path and the separate async selector continuation. Layout preparation
can refuse earlier because it admits the retained keys before rendering.

The 3,072-record selector report ran **13.17–13.75% faster warm** and
**13.87–14.21% faster compile-to-result**, with lower process CPU in both.
Windows allocation fell **10.80%** from 4,778,222 to 4,262,118 bytes per
invocation. The accounted peak fell from 4,728,793 to 3,503,577 bytes.
Control allocations changed at most 0.11% and all budgets returned to
baseline. The ordinary method-format control was noisy initially and
remained mixed on repeat.

| Collection and workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1045 Shipment selector report, warm | −13.75…−13.17% | −13.45…−12.91% | 0.51 / 0.16% |
| B1045 Shipment selector report, compile-to-result | −14.21…−13.87% | −14.38…−13.29% | 0.17 / 0.24% |
| B1045 Ordinary method-format report, warm | −2.93…+3.21% | −2.70…+4.86% | 0.48 / 5.82% |
| B1045 Named format, warm | −3.13…−0.31% | −3.68…−1.12% | 2.53 / 0.37% |
| B1045 F-string report, warm | −1.74…+1.31% | −0.57…+2.31% | 2.38 / 0.70% |
| B1045 Expanded forwarding report, warm | −2.22…+0.12% | −1.68…−0.83% | 1.61 / 0.77% |
| B1045 Positional calls, warm | −0.71…+1.31% | −1.56…+1.68% | 0.42 / 1.61% |
| B1046 Ordinary method-format report, warm | −0.44…+1.96% | −0.78…+0.80% | 0.13 / 2.28% |
| B1046 F-string report, warm | −2.18…−0.30% | −4.10…+1.00% | 1.25 / 0.66% |

Both collections compared `014e58531` and `f04a8daf3` on the quiet Ubuntu
24.04.4 Azure F4as_v7 with .NET 11 RC3 on 2026-11-19 UTC. The candidate
source and tests match retained `9ac02d15d`. Ranges are the four pairings of
process medians. Negative values favor the candidate. B1046 used the same
images and cases with a different worker order. The control data support
retaining the selector gain. Small effects on the other formats remain uncertain.

Windows .NET 11 and WSL .NET 10 each passed 696 public and 87 white-box
focused cases. They include actual host suspension through a subscript,
mutated dictionary values, distinct hook key identities, missing-key
payloads, refusal and cancellation. The selector semantics probe agreed
with CPython 3.13. Both complete 32-file frozen images passed the six-case
catalog verification with CPython outputs.

The seven initial jobs took 321.17 seconds and the two confirmation jobs
took 95.51 seconds. All 180 measured batches verified their output and file
hashes. Workers joined, guest fixtures were removed and both archives were
verified under the quiet-VM lease. Guest elapsed includes mediated I/O,
output checks and earlier result cleanup. Compile-to-result adds compilation
and its cleanup. Startup, fixture preparation and final cache teardown are
outside guest elapsed. Process CPU covers the wider request.

[B1045 summary](summary.json) · [B1045 raw](observations.tgz) ·
[B1046 summary](confirmation/summary.json) · [B1046 raw](confirmation/observations.tgz) ·
[Measured core](measured-core.dll.gz) · [Sources, checks and procedures](checks-and-procedures.tar.xz).
