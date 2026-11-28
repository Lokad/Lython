# B1966 Share bytes and list member facts with their type forms

The frontend uses the same per-member lookup for `str`, `bytes` and
`list`. Each family initializes on use. Instance and type forms share
83 callable and 52 return rows for bytes and lists. Argument shapes,
diagnostic messages, mutation facts and receiver kinds stay identical.
The sets retain their distinct instance-only call and return metadata.

The two 32-file worker images use `bf5123e6f` and the candidate patch.
Only the core DLL differs. Both match 57,408 queries per surface, all
962 callable and 596 return matches, and 33 compiler diagnostic cases.
Both images pass three independent CPython 3.13.16 goldens. Windows
.NET 10 and 11 and Ubuntu WSL .NET 11 each pass 1,508 public and 550
white-box focused Release checks.

## First compilation

The allocation witness measures the caller thread through its first
compile call in a fresh Windows process. Each workload uses seven
samples per image in alternating order. All 28 probes agree on admitted
storage and return allowance to zero during compiled-owner cleanup.

| Workload | CLR bytes, reference → candidate | First compile elapsed medians |
| --- | ---: | ---: |
| Configuration edit | 484,888 → 400,144 B | 105.67 → 102.78 ms |
| UTF-16 encoding, hex and decoding | 434,712 → 191,280 B | 84.20 → 74.43 ms |

First-compilation allocation falls 17.48% for configuration and 56.00%
for the codec. The elapsed medians describe these process-isolated probes.

## Warm compilation

November 28, 2026 UTC on Ubuntu WSL and .NET 11 RC3. Each collection
uses two workers per image, at least five seconds of warming and seven
rotated batches. The repeat reverses worker and lane order and moves
from logical CPU 27 to 25. Elapsed includes compilation from resident
source and previous compiled-owner disposal. Process CPU also includes
request framing. Ranges span pairings of worker medians.

| Workload | Initial elapsed | Initial CPU | Repeat elapsed | Repeat CPU |
| --- | ---: | ---: | ---: | ---: |
| UTF-16 encoding, hex and decoding | −2.77% to −0.22% | +1.15% to +1.28% | −2.22% to −0.15% | −2.69% to +0.50% |
| Binary struct packing | −0.85% to +1.19% | −0.36% to +1.70% | −13.50% to −1.34% | −8.67% to +2.20% |
| Configuration control | −1.56% to +0.77% | −1.44% to +1.98% | −1.70% to −0.96% | −2.77% to −0.06% |

Keep the first codec CPU increase and both observations. The repeat
spans zero with 3.14% candidate CPU spread. The repeat struct workers
differ by about 5.8% within each image. The narrower metadata and cold
allocation reduction support adoption. Both comparisons verify all
168 timed batches in total, join their workers, remove the RAM fixtures
and verify their persistent archives.

[Cold allocation](cold-allocation.json) · [Initial timings](initial-summary.json) ·
[Repeat timings](repeat-summary.json) · [Initial image](initial-identity.json) ·
[Repeat image](repeat-identity.json) · [Initial observations](initial-observations.tar.xz) ·
[Repeat observations](repeat-observations.tar.xz) · [Procedures and parity](procedures.tar.xz).
