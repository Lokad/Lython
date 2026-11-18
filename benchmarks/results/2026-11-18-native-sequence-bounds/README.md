# Native sequence bounds

R536/B967 and B968 compared the combined 55fe3da03 source (8971ee04) with
the isolated indexing change (9eed03c9) on the quiet Azure F4as_v7 VM on
2026-11-18 UTC. Only the core binary differed between the two copied
32-file images. Both used the same .NET 11 RC3 runtime, worker and optional
0.4.0 adapter. Four complete programs passed the existing verifier against
CPython before collection. The source pair measures Lython against Lython.

The runtime keeps machine integer indices native through the width check.
Native and generic access share negative-index normalization. Successful
native reads avoid diagnostic target selection. Error forms, string
admission, tuple element identity and the original context stay covered.
Each actual local .NET target passed 176 white-box and 653 public tests.
Accounting peaks and allocated bytes matched and all allowance balances
returned to baseline in the four programs.

| Complete script and phase | Initial elapsed change | Confirmation elapsed change | Confirmation process CPU change |
| --- | ---: | ---: | ---: |
| Floating JSON report, warm | +0.85 to +1.25% | −1.90 to +2.70% | −1.95 to +0.79% |
| Floating JSON report, compile-to-result | −2.24 to −1.38% | −2.05 to −1.52% | −3.88 to −2.66% |
| Helper text, warm | +0.23 to +3.84% | −1.64 to +0.29% | −3.83 to −0.63% |
| Text normalization, warm | −5.24 to +1.93% | — | — |
| Positional calls, warm control | −3.27 to +0.97% | — | — |

Percentages cover all pairings of two candidate and two reference process
medians. Negative favors the candidate. The first run used eight seconds
of warmup per process. Confirmation used 12 seconds and reversed the role
order for all three jobs. Warm timing includes mediated RAM I/O and output
checks and prior-result cleanup. Compile-to-result adds compilation and
compiled-owner disposal. Process CPU includes wider worker costs.

Retain the conversion deletion and repeated floating compile-to-result gain.
Warm floating remains inconclusive with confirmation control spreads
of 1.80% and 2.83%. The first warm floating and helper-text slowdowns stay
visible. The two untimed-in-confirmation controls had first-run process
spread up to 4.83%. Full delivery applies separately to the integrated
source with the corrected regex test fixture.

The five initial jobs took 232.91 seconds. The three confirmation jobs
took 190.80 seconds. Quiet endpoints, worker shutdown, fixture cleanup
and archive hash checks all passed. Two superseded cores, B472 and B473,
were retired after a fresh lease, exact committed-byte and all-alias check.
The two tested images remain available.

[Initial summary](summary.json) · [Confirmation summary](confirmation/summary.json) ·
[Allocation](allocation-summary.json) · [Initial observations](observations.tgz) ·
[Confirmation observations](confirmation/observations.tgz) ·
[Verification and procedures](procedures-and-checks.tar.xz)
