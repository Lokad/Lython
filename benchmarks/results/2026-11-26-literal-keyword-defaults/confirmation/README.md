# B1788 Reordered literal keyword-default comparison

The [first block](../README.md) provides the image, timing boundaries and
measurement procedure. This repeat uses the same complete worker images and
script and file goldens. It adds a compile-only lane for the ordinary factory
to investigate the earlier CPU increase through result. Compile-only includes
compiled-program disposal.

| Complete script and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Ordinary definitions, through result | −0.65% to +0.59% | +0.87% to +1.92% |
| Escaping rules, warm async | −9.53% to −1.16% | −10.87% to −1.22% |
| Protected file search, warm sync | −1.92% to −0.53% | −2.32% to +0.73% |
| Ordinary definitions, compile only | −0.66% to +0.36% | +0.26% to +1.61% |

The escaping factory uses less warm async CPU in both blocks. Its repeat CPU
spread is 6.00% in the reference and 4.55% in the candidate. The file-search
control also takes less elapsed time, with mixed CPU. The ordinary factory
never initializes prepared keyword literals. Its modest compile-only CPU
increase is consistent with a compile-side cost. The measurements do not
identify which compiler work accounts for it, and both repeat elapsed ranges
span zero.

Keep direct initialization and track the ordinary-factory CPU increase at
the next callable or frontend checkpoint. The new path removes work from
2,048 closed pair members per run in both APIs, keeps execution allocation
and admission unchanged on four fixtures, and retains 80 more admitted
compiler bytes on escaping rules. Through-result speedup remains unproven.

All 112 batches and both quiet checks pass. All workers join and fixture
cleanup completes. The observations archive verifies before the VM lease
ends. This repeat reuses the existing frozen images.

[Timing and worker spreads](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz)
