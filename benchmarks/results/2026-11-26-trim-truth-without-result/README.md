# B1802 Test default trimming without a string result

The candidate on `29e078ddd` uses the shared private string-result
path when an exact builtin strip call feeds the subsequent condition.
It tests whether any text survives using the existing bounded Python
whitespace scan. The ordinary branch keeps its own check and receives
the boolean result. Explicit character sets, escaping string values and
dynamic method results retain their original paths.

The JSON Lines helper report processes 5,120 input lines, including 4,096
nonblank records. It allocates 14.34% less in both APIs in the Windows
.NET 11 allocation diagnostic. Peak admission falls 13.72%,
from 3,822,014 to 3,297,534 B. All three controls match allocation and
peak admission across both APIs. Compiled admission matches and every
owner returns to baseline.

Both 32-file images match all four CPython goldens. Windows .NET 11 and
Ubuntu .NET 10 each pass 1,180 public and 285 white-box focused Release
checks. The new case covers default trim truth for empty, whitespace and
Unicode inputs, dynamic truth callbacks, explicit characters and
expressions that keep the real trimmed value. CPython 3.13.2 matches the
expected output.

## Local diagnostic

The five local Ubuntu WSL lanes complete 140 batches. Ranges span all
pairings of two worker medians per source. Negative values mean less time.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| JSON Lines helpers | Warm async | −5.35% to +2.45% | −5.66% to +2.85% |
| JSON service control | Warm sync | −2.33% to +1.42% | −3.10% to +1.68% |
| Unicode text control | Warm sync | +2.45% to +7.39% | +4.52% to +6.43% |
| JSON Lines helpers | Warm sync | −6.82% to +1.47% | −7.14% to +0.00% |
| Protected file-search control | Warm sync | +6.60% to +21.28% | +7.37% to +23.22% |

## Quiet confirmation

The same images run four reordered lanes on the Azure F4as_v7, Ubuntu
24.04.4 and .NET 11 RC3, November 26, 2026 UTC. Workers warm for five
seconds and then run seven rotated batches targeting 440 ms. Warm
elapsed includes mediated RAM-backed I/O, verified outputs and files,
and previous-result disposal. Process CPU also includes fixture reset
and request framing. Startup and compilation sit outside both clocks.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Protected file-search control | Warm sync | −0.62% to +0.08% | −1.69% to +2.54% |
| JSON Lines helpers | Warm async | −5.98% to −2.37% | −6.98% to −1.75% |
| Unicode text control | Warm sync | −0.82% to +3.17% | +1.12% to +1.84% |
| JSON Lines helpers | Warm sync | −9.08% to −2.50% | −6.72% to −1.46% |

Retain the candidate for its measured allocation and admission savings
and the quiet improvement in both JSON Lines modes. The local JSON Lines
timing was mixed. Protected file-search CPU spans zero on the VM after
a sizable local slowdown. Unicode text CPU is adverse locally and on
the VM, where it rises 1.12–1.84% with narrow same-source CPU spreads.
The measured tradeoff is specific to these scripts. The cause of the
Unicode control movement remains open.

All 112 quiet batches and both machine checks pass. Workers join, guest
fixtures retire, and the archives verify. Initial space previews made
no changes. The verified duplicates from the two new profiles added
2,957,312 B to 4,952,064 B of free space, below the 9,053,026 B requested
at that preview. An expanded preview also found an earlier remote
archive missing. Staging instead released one duplicate B1622 core DLL
after verifying its full frozen local image, separate compressed backup
and remote observations against their pushed copy.

The first launch reached 4,370,432 B free and stopped before timing
at the collector's 5 MB evidence floor. The staged images and both
profiles remained available. Reclaiming 2,760,704 B of byte-identical
expanded profile files left 7,024,640 B at retry. Both profile archives
verified against their pushed copies, and the retry used both original
32-file images and the same fixture and timing content. The collector's
sampling and storage checks stayed in force.
[Initial status](quiet/first-attempt/terminal-status.json) ·
[Initial observations](quiet/first-attempt/observations.tar.xz).

[Sync allocation and admission](allocation-summary.json) ·
[Async allocation and admission](async-allocation-summary.json).

[Local timing](local/summary.json) · [Local observations](local/observations.tar.xz) ·
[Local image](local/identity.json) · [Local receipt](local/archive-receipt.json).

[Quiet timing](quiet/summary.json) · [Quiet observations](quiet/observations.tar.xz) ·
[Quiet image](quiet/identity.json) · [Quiet terminal status](quiet/terminal-status.json).

[Verification, procedures and exact patches](procedures.tar.xz).
