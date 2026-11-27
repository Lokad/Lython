# B1885 Compare current log searches with CPython

On November 27 the frozen `4ab65dcc6` image ran against CPython 3.13.16
on one logical CPU under Ubuntu WSL on an Intel i7-14700KF. Lython used
.NET 11 RC3 and the optional adapter with signed PythonRe 0.5.0. The
complete 32-file image matches the delivered B1884 source. Both scripts
read 3,072 lines from the same 86,954-byte UTF-8 file and consume one
capture on each match. One uses a compiled pattern and the other calls
`re.search` through the module. Independent output and file goldens verify.

| Workload | Phase | Lython | CPython | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Compiled-pattern log search | Warm | 2.37484–2.38422 ms | 0.90704–0.92178 ms | 2.576–2.629× | 2.545–2.569× |
| Module-level log search | Warm | 2.77914–2.79027 ms | 1.42270–1.46284 ms | 1.900–1.961× | 1.905–1.986× |
| Module-level log search | Compilation only | 0.12383–0.12813 ms | 0.02724–0.02748 ms | 4.507–4.704× | 4.365–4.826× |
| Module-level log search | Compile-to-result | 3.13204–3.15143 ms | 1.55325–1.55635 ms | 2.012–2.029× | 1.956–2.029× |

Ratios divide Lython by CPython and span all pairings of worker medians.
Two workers per engine warm for five seconds per lane before seven rotated
batches. Warm elapsed includes mediated RAM-backed I/O, golden verification
and prior-result disposal. Compilation only compiles and retires the script
without running the guest workload. Compile-to-result combines compilation
with execution and cleanup. Process CPU includes fixture reset and request
framing. Process startup sits outside the four clocks.

The compilation-only ratio covers a small absolute interval of about
0.12 ms on Lython. The warm and compile-to-result gaps both remain near
2× for the module script. The compiled-pattern search has a wider warm
gap. These local diagnostics are separate from the earlier quiet VM
observations. Use the matched B1884 images to assess the adapter change.

All 112 timed batches verify in 150.35 seconds. Workers join and RAM
fixtures are removed before the persistent archive verifies. The initial
launch stopped before sampling on an inherited auxiliary identity from
the older shipment comparison. The verifier passed on both launches.
The corrected run uses the same frozen image and all its own hashes.

[Timing](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Verification and procedures](procedures.tar.xz).
