# B1839 Enter converted dictionary updates before staging call operands

The trial on `7bc13cd06` marks the existing member load when the receiver
and key are the same private bindings used by the assignment and the default
is a compiled small integer. An exact dictionary and string key can enter
the same converted update before publishing the receiver, method, key or
default into the operand stack. The ordinary call route continues to cover
computed operands. The shared kernel keeps the original prior value, live
converter, funded record-field lookup, decimal parse, managed call guards
and in-place assignment. A file-fed map and five other control tapes match.
The marked tapes keep their prior slot capacities and fallback instructions.

In a separate instrumented Windows Release build, both shipment scripts and
JSON totals complete 2,046 of 2,048 rows through the direct entry in each API.
The file-fed map uses that entry zero times. The instrumented build verifies
output, files and budget recovery and supplies route counts only.

Windows .NET 11 and Ubuntu WSL .NET 10 each pass 527 public and 197 white-box
Release checks. The regression suspends a custom `get`, rebinds the converter
and tests a computed default that changes the destination before the read.
Existing checks cover first lookup-cache admission and refusal, retained aliases,
missing and erroneous fields, and integer promotion. Four complete scripts
match independent CPython 3.13.2 output and file goldens in both frozen images.
The Windows allocation diagnostic restores the budget in both APIs. Peak
admission is identical, and median CLR allocation differs by at most 230 B
among the four scripts and two APIs.

## Initial local timing

The workers run on one logical CPU in local Ubuntu WSL .NET 11. Two workers
per image warm for five seconds and contribute seven rotated batches. Ranges
span every pairing of worker medians. Warm elapsed includes mediated I/O,
verification and prior-result disposal. Process CPU also covers fixture
reset and framing. Startup and compilation sit outside these lanes.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map control | Warm async | −16.04% to +0.53% | −17.61% to +15.73% |
| Wide shipment | Warm async | −36.06% to −11.88% | −8.25% to +44.36% |
| JSON service totals | Warm sync | −22.44% to +15.07% | −16.78% to −4.46% |
| Bound zip shipment | Warm async | −24.93% to −7.11% | −29.98% to −10.69% |

The wide candidate CPU medians differ by 41.18% and the unchanged file-fed
candidate medians by 23.33%. All 112 batches verify. These timing ranges are
diagnostic. The quiet VM had 12.3 MB free. Its staging guard stopped before
upload when apparent duplicates resolved to shared files. The archived VM
images remained intact. Confirmation uses the same frozen images locally.

## Same-image confirmation

The local repeat measures JSON totals, file-fed map and wide shipment in
that order. It uses the same worker binaries, timing boundary and sample
schedule. All 84 observations verify. Both captures join their workers and
remove guest files before the persistent archives verify.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| JSON service totals | Warm sync | −14.52% to −5.66% | −11.43% to −6.77% |
| File-fed map control | Warm async | −4.27% to +0.38% | +2.19% to +2.94% |
| Wide shipment | Warm async | −12.02% to +1.92% | −5.78% to +5.42% |

Retain the direct entry. Both JSON blocks measure lower process CPU, and
the code eliminates the call operand staging on 2,046 rows in each affected
script. Wide shipment throughput remains unresolved. The bound improvement
has one local observation. The file-fed map repeat shows a 2.19–2.94% CPU
increase despite unchanged execution at the marked site. Its first CPU
range spans zero. Carry that adverse result into the next quiet comparison.

[Image identities](identity.json) · [Tape proof](tape-proof.json) ·
[Route counts](routes.json) · [Allocation and admission](allocation-summary.json) ·
[Initial timing](summary.json) · [Confirmation timing](confirmation/summary.json) ·
[Initial observations](observations.tar.xz) ·
[Confirmation observations](confirmation/observations.tar.xz) ·
[Quiet staging](quiet-staging.json) · [Exact patches and checks](procedures.tar.xz).
