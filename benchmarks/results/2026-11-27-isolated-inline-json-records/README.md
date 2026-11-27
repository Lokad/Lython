# B1909 Isolate inline values to shared JSON records

The four starting slots live in the shared JSON record's shell.
Ordinary dictionaries keep their original size and allocation path.
The managed record uses the ordinary Python dictionary type and methods.
It admits the scalar credits before creation and admits the entire new
array before growth or shape promotion. The key layout and retained
values keep their existing ownership.

Against `8c05ed067`, dense nested JSON allocates 98,256 fewer CLR bytes
per invocation in both hosting modes. Sparse Lines saves about 197 KB.
Admitted peaks fall 9.18% and 8.63%, respectively. The quoted CSV witness
returns to its original allocation range and has the same admitted peak.
Every invocation returns the borrowed allowance to baseline.
[Initial shell trial](../2026-11-27-inline-json-records/README.md).

## Matched local timing

Only the core DLL differs between the verified 32-file images. Both
match three independent CPython 3.13.16 output and file goldens. The
November 27, 2026 UTC WSL run uses two workers per image and seven
rotated batches per worker, with at least five seconds of warmup.
Intervals span pairings of worker medians. Warm elapsed includes
mediated I/O, validation and previous-result disposal. Process CPU
also includes fixture reset and request framing.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dense nested JSON | Run | −3.46% to −1.90% | −2.68% to −0.60% |
| Quoted CSV invoice control | Run | −1.59% to +5.50% | −0.19% to +5.92% |
| Sparse nested JSON Lines | RunAsync | −4.20% to +1.50% | −4.09% to +2.54% |
| Dense nested JSON | RunAsync | +0.25% to +1.52% | −6.02% to +0.80% |

All 112 timed batches pass. The two candidate CSV workers differ by 6.12%
in process CPU. Dense async elapsed moves adversely and CPU spans zero.
Retain the change for the eliminated array lifetime, lower admission and
local dense sync result. Keep the async and CSV timing uncertainty.

## Semantics

CPython verifies the shared-record program covering exact Python type,
instance and unbound methods, copy, keyword forwarding, deletion,
reinsertion, key order, promotion and clear. Both Lython APIs run the
same regression and verify allowance reuse with retained results.
Four initial white-box failures required an exact CLR class. The corrected
assertions still verify shared key identity, independent mutable records,
refusal and cleanup. Both initial and corrected runs are in the archive.
Final focused Release checks pass on Windows .NET 11 (2,038 checks),
Windows .NET 10 (771) and Ubuntu WSL .NET 11 (771).

[Image identity](identity.json) · [Allocation and admission](allocation-summary.json) ·
[Timing summary](summary.json) · [Raw observations](observations.tar.xz) ·
[Source patches and focused checks](procedures.tar.xz).
