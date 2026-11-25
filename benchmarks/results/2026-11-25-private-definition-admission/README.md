# B1721 Admit fixed storage for private function definitions together

An unannotated, undecorated definition without captures can admit the
callable shell with its keyword-default storage. Both executable drivers
and the closed definition paths use the existing keyword-default slot to
carry the charge until the callable takes the storage. Failure retires both
charges before the next guest operation, including when a closed pair
fails after building its first member. Variable storage still needs its
own preflight.

The fixed combined charge can refuse earlier, before a default expression
that the separate default-storage preflight previously admitted. The
first refusal points to the definition. Successful operations keep the
original expression order and expose the usual defaults and their aliases.
A genuinely suspended default keeps its storage charged through cleanup.

The frozen reference uses the runtime of `7120ee55e`. The two 32-file
images differ only in the core DLL and verify six complete CPython-derived
output/file goldens. A separate local diagnostic counts positive allowance
resize requests on the same images.

| Complete script | Reference requests | Candidate requests | Peak execution admission on both |
| --- | ---: | ---: | ---: |
| Generated ordinary definitions | 3,093 | 1,557 | 971,730 B |
| Defaulted rule factories | 8,200 | 4,104 | 2,786,655 B |

The request counts and admitted peaks match between public sync and async
hosting. The four control workloads have the same resize counts in both
images. All runs return the borrowed allowance to their original balances.
The two affected scripts have identical warm and compilation CLR
allocations. The runtime report retains a small change in the unchanged
shipment allocation control.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 with .NET 11 RC3 on November 25,
2026 UTC. Two workers per image warm for five full-request seconds in the
first block and seven in confirmation, then contribute seven rotated batches
per phase. Ranges span the four pairings
of worker medians. Warm elapsed includes mediated RAM-backed I/O where
applicable, verification and prior-result cleanup. Wider CPU includes reset
and request framing. Compilation and compile-to-result are measured
separately from warm execution.

## First block

| Workload | Phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Generated ordinary definitions | Warm sync | −3.57 to −0.29% | −4.68 to +0.18% | 0.35% / 3.04% |
| Defaulted rule factories | Compile-to-result | −5.15 to +0.00% | −5.63 to +1.42% | 4.99% / 0.42% |
| Eager annotated rules | Warm sync | −2.13 to +0.63% | −3.85 to +1.42% | 1.25% / 1.55% |
| Shipment callback report | Warm async | −0.24 to +1.66% | −0.54 to +1.10% | 0.15% / 1.75% |
| Generated ordinary definitions | Compile-to-result | −2.09 to +0.61% | −2.55 to +0.77% | 1.42% / 1.32% |
| Postponed annotated rules | Compile-to-result | −4.43 to +7.83% | −5.04 to +7.08% | 3.59% / 8.92% |
| Defaulted rule factories | Warm sync | −2.99 to −1.63% | −1.14 to −0.94% | 1.14% / 0.26% |
| Nonlocal Unicode text | Warm sync | −3.74 to +8.15% | −3.70 to +8.73% | 11.22% / 1.01% |
| Generated ordinary definitions | Compilation | −0.73 to +1.52% | +0.54 to +2.66% | 1.45% / 0.80% |

## Reordered confirmation

| Workload | Phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Generated ordinary definitions | Compilation | −1.96 to −0.62% | −3.11 to −0.71% | 1.23% / 0.14% |
| Defaulted rule factories | Warm sync | −4.86 to −2.02% | −5.89 to +0.59% | 0.12% / 2.87% |
| Nonlocal Unicode text | Warm sync | −10.08 to −4.25% | −10.20 to −3.55% | 1.79% / 4.62% |
| Generated ordinary definitions | Warm sync | −4.94 to −2.18% | −4.76 to −1.34% | 1.64% / 1.24% |
| Postponed annotated rules | Compile-to-result | −10.69 to −3.11% | −12.48 to −3.33% | 4.81% / 3.52% |

Retain the combined preflight for the removed allowance requests and the
earlier refusal before guest effects. Ordinary warm elapsed is 0.29–3.57%
lower in the first block and 2.18–4.94% lower in confirmation. Defaulted
factories are 1.63–2.99% and 2.02–4.86% lower. Wider CPU crosses zero in one
block for each target. Compilation CPU rises 0.54–2.66% in the first block
and falls 0.71–3.11% in confirmation. The unchanged text and postponed
controls also improve in confirmation, with larger ranges. The measurements
leave the causal elapsed and CPU magnitudes uncertain.

Windows .NET 11 and Ubuntu .NET 10 each pass 610 focused Release checks.
The coverage includes an admission refusal before a default's side
effects, cancellation with a suspended default, default alias identity and
refund of unpublished pairs while their scope is still alive. All 392
timed batches verify. The persistent archive verifies under the quiet lease
after workers join and guest fixtures are removed.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allowance requests](admission-summary.json) · [Runtime allocation](allocation-summary.json) ·
[Compiler allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Source and procedures](procedures.tar.xz) ·
[Confirmation timing](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal receipt](confirmation/quiet-terminal-status.json).
