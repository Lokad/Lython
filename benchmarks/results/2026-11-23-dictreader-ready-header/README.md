# B1485 Return an initialized DictReader header directly

Private `2bc4e2443` bypasses the header-acquisition state machine when
the header already exists. The existing cold path still adopts the raw
header and updates the reported physical line number. Both paths use
the same line-number update. Data row ownership and publication use
the retained state machine.

Compared with `edb02618a` on November 23, 2026 UTC. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. The reference image has
identical production source to that revision. Only the core DLL differs
between verified 32-file images. Two workers per revision warm for eight
seconds each and supply seven rotated batches. Ranges span all four
pairings of their medians. Warm elapsed includes mediated RAM-backed
I/O, stdout and file verification and prior-result disposal. Compile to
result adds compilation and compiled-owner disposal. Wider CPU includes
reset and transport.

| Complete script | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Quote-heavy CSV invoice | Warm | −0.23% to +0.59% | −1.03% to +0.73% | 0.23 / 0.59% |
| Ordinary CSV invoice | Warm | −1.14% to +6.18% | −1.56% to +7.68% | 0.82 / 6.53% |
| Short nonlocal word report | Warm | −7.27% to +5.08% | −3.95% to +7.66% | 1.56 / 11.58% |
| Typed ASCII CSV meter | Warm | −4.12% to +0.51% | −5.30% to +2.50% | 1.37 / 3.42% |
| Quote-heavy CSV invoice | Compile to result | +1.36% to +4.64% | +0.00% to +5.66% | 0.12 / 3.11% |

Keep the direct header return private. The quoted warm lane is effectively
flat, and its compile-to-result elapsed rises 1.36–4.64%. The ordinary
invoice and short text candidate workers vary by 6.53% and 11.58%.
Allocated bytes are within 56 bytes per script at unchanged admitted
peaks. The quoted warm gain in the broader B1484 split therefore has
little support from this isolated header change.

Windows .NET 11 and Ubuntu .NET 10 each pass all 354 white-box and 1,586
public focused Release checks. Both actual worker images pass all four
goldens and every repeat restores the allowance. All 140 timed batches
verify in 236 seconds. Both quiet gates pass, workers
join, guest fixtures are removed, and the raw archive verifies under
the lease.

[Timing](summary.json) · [Allocation](local-allocation.json) ·
[Identity](identity.json) · [Terminal](terminal-status.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz).
