# Shared trim entry and scratch ownership

R538/B970 compared f7b1c056d (frozen 9eed03c9) with isolated R538 (27cb473e)
on 2026-11-18 UTC. The quiet Azure F4as_v7 VM used .NET 11 RC3, normal GC
and tiering, and the same 32-file worker with the optional 0.4.0 adapter.
Both copied images passed the existing catalog against CPython before use.

The guest wrapper already admitted custom trim sets. This change binds each
trim direction once, uses one entry for default and custom trimming, and moves
the existing reservation to the primitive that builds the set. It ends after
the slice is built. Unchanged inputs and the empty singleton retain their
identity. New slices use their existing owner-aware UTF-8 construction, so
the wrapper no longer checks ownership a second time.

| Complete script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Helper text, warm | −1.16 to −0.73% | −1.19 to −0.48% | 0.12 / 0.32% |
| Helper text, compile-to-result | −1.74 to −1.16% | −2.98 to −1.82% | 0.52 / 0.08% |
| Text normalization, warm | −1.41 to +0.39% | −2.01 to +0.68% | 0.52 / 1.30% |
| Positional calls, warm control | −0.47 to +0.63% | −2.00 to −1.22% | 0.70 / 0.40% |

Negative percentages favor the candidate. Ranges include all pairings of
two candidate and two reference process medians. Each used eight seconds
of warmup and five measured rounds. Warm includes mediated RAM I/O,
output validation and prior-result cleanup. Compile-to-result adds
compilation and compiled-owner disposal. Process CPU also includes
worker execution outside guest clocks.

Both actual .NET targets passed 159 white-box and 1,658 public checks each.
A separate probe matched CPython for Unicode, custom sets, empty inputs,
identity and argument errors through both APIs. All four measured programs
restored their allowance balances. Peaks matched and allocation differences
were below 0.02%. Retain the consolidation and modest helper-script gain.
The other elapsed controls are mixed. This comparison has no CPython ratio.

The four jobs completed in 187.17 seconds. Both quiet endpoints, joined
workers, fixture release and archive hash checks passed. The stage retired
one archived superseded B495 core and two exact committed raw replicas
after verifying the lease, paths, owners, hashes and aliases. The measured
images remain available. Full delivery is a separate checkpoint.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Verification and procedures](procedures-and-checks.tar.xz)
