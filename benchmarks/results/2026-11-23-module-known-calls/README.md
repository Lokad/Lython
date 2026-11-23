# B1453 Module-owned known-call contracts and signatures

Private `69304759d` removes the all-names frontend index and the
shared runtime signature initializer. Forty module owners build
their own immutable lookup and signature metadata. The frontend and
runtime still use the same signature instances. Canonical targets
route aliases and nested modules by the first dotted component.

Compared `c6d2f2b3d` and `69304759d` on November 23, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The 32-file
images differ only in the core DLL. CPython 3.13.16 validates the
scripts and supplies an unchanged fresh-process control.

| Fresh process | Reference median | Candidate median | Median change | Paired-round elapsed changes |
| --- | ---: | ---: | ---: | ---: |
| Configuration edit | 264.8 ms | 262.2 ms | −0.99% | −5.98 to +3.34% |
| Typed ASCII CSV meter | 321.4 ms | 316.1 ms | −1.63% | −6.52 to −0.41% |
| Small XML selection | 241.4 ms | 237.9 ms | −1.43% | −3.56 to +1.20% |
| Empty control | 122.3 ms | 122.7 ms | +0.36% | −1.87 to +5.00% |
| Tiny arithmetic control | 148.9 ms | 149.8 ms | +0.61% | −9.19 to +4.00% |

Seven fresh starts per source and workload alternate the source order
and rotate the workloads. The existing runner times launch through
exit and pipe drain. This includes worker initialization, compilation,
one execution, applicable RAM-backed I/O and in-worker cleanup. OS
caches stay warm. These source-pair observations are diagnostic.

| Warm lane | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Configuration compile | −2.18 to +0.60% | −1.96 to +1.02% | 2.57 / 0.27% |
| Typed CSV compile | −0.69 to +0.71% | −3.07 to −0.17% | 1.12 / 0.29% |
| XML compile | −0.97 to +0.79% | −1.63 to +0.06% | 0.61 / 1.16% |
| Read-only validator compile | −0.27 to +3.50% | +0.73 to +3.37% | 1.71 / 2.03% |
| Expanded JSON Lines execution | −1.81 to −0.03% | −1.37 to −0.25% | 1.24 / 0.57% |

Two workers per revision warm for eight seconds and supply seven
rotated batches each. Ranges cover every pairing of worker medians.
Compile elapsed includes prior compiled-owner disposal. Warm JSON
includes mediated I/O, output verification and previous-result
disposal. Wider CPU includes resets and framing where applicable.

Local first-compile allocation drops from 1,120,192 to 839,208 bytes
for configuration, 1,186,800 to 924,880 for CSV, and 1,065,136 to
809,584 for XML. Warm allocations and admitted peaks match across
all seven cases. All compile owners refund on disposal.

Constructor events separate frontend lookup from argument validation.
Configuration loads only the JSON frontend call index. Other runtime
signature groups still initialize when the semantic checker reads
their names. The XML fixture loads its own frontend index. These
findings motivate the direct semantic routing refinement. Keep this
intermediate source private and preserve the higher warm validator CPU
as well as the favorable allocation and CSV observations.

Both .NET 11 Windows and .NET 10 Ubuntu pass 88 white-box and 1,187
public focused checks. All 493 declarations, 487 frontend contracts
and 23 aliases have identical metadata. Both images match seven
CPython goldens. All 70 fresh pairs and 140 warm batches verify.
Both quiet gates pass, workers join, fixtures are removed and the
archive verifies under the lease. Before those checks, the initial
partition used two field names that collided with their catalog
class. The audit also initially assumed a parameterless source span.
The corrected source and parity procedure are included.

[Source and images](source.json) · [Timings](summary.json) ·
[Signature parity](parity-summary.json) · [Allocations and allowance](allocation-summary.json) ·
[Constructors](constructor-summary.json) · [Raw observations](observations.tar.xz) ·
[Checks and procedures](procedures.tar.xz).
