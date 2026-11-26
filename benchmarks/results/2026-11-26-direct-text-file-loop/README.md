# B1781 Borrow the text file directly in executable loops

An exact text file already carries its own Python iterator position.
The executable loop now keeps the borrowed handle in its operand and
publishes completed lines without a private cursor or an intermediate
iterator result. It checks the managed stack before invoking the shared
reader. A suspended line uses the existing refill and cleanup path
before publication. Loop exit releases the operand. The file's existing
owner still closes it. [Source patch](candidate.patch)

| Complete script | Input | Lines delivered directly per run | CLR bytes before → after |
| --- | ---: | ---: | ---: |
| Protected file search | 42,010 B | 1,025 | 113,368 → 113,280 |
| JSON Lines helper report | 110,592 B | 5,120 | 1,600,336 → 1,600,304 |
| CSV quantity map control | 27,350 B | 0 | 808,528 → 808,528 |

The separately instrumented route counts match in both APIs. Each
affected script bypasses one private cursor. Search breaks early, and
JSON Lines reaches EOF. These runs use the benchmark host's completed
read operations. The measured frozen images exclude the counters.
Peak admission matches for all scripts and every run restores its
budget after cleanup. Both images match three independent CPython
output/file goldens. [Route counts](routes.json) ·
[Allocation and admission](allocation-summary.json)

## Quiet comparison

The reference runtime matches `e222c5221`. The November 26, 2026 UTC
comparison uses Ubuntu 24.04.4 and .NET 11 RC3 on the Azure F4as_v7.
The two 32-file images differ only in their core DLL. Two workers
per source warm for five seconds and supply seven rotated batches
per lane. Ranges span the four pairings of worker medians. Warm
elapsed includes mediated RAM-backed I/O, output and file verification,
and previous-result disposal. Process CPU also includes fixture reset
and request framing. Compilation, startup and final teardown are
outside these clocks. Negative changes mean lower time.

| Workload and lane | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| CSV quantity map control, async | −1.49% to +0.27% | −2.29% to −0.09% |
| Protected file search, sync | −2.17% to +0.43% | −0.76% to +0.17% |
| JSON Lines helper report, sync | −2.68% to +0.02% | −2.31% to +0.70% |
| Protected file search, async | +0.07% to +1.44% | −2.17% to +1.09% |
| JSON Lines helper report, async | −4.30% to −1.77% | −4.69% to −1.57% |

Retain the direct handoff. Async JSON Lines improves on both clocks.
The unused CSV control also uses less CPU. Both sync timing ranges
span zero, and async search elapsed rises slightly with mixed CPU.
The pinned local WSL diagnostic had adverse sync CPU and as much
as 66.14% CPU spread between the CSV reference workers. Retain that
block separately. [Quiet timing](quiet/summary.json) ·
[Local diagnostic](summary.json)

## Validation

Windows .NET 11 and Ubuntu .NET 10 each pass 368 white-box and
1,513 public focused Release checks across the main selection and
targeted replay. The new regressions cover shared `next()` position,
break, comprehension exhaustion, retained aliases and a suspended
read that must finish with its uncancelled close before refund.
The first Windows alias test used unsupported text `seek()`. The
corrected test verifies that `readline()` returns EOF on the open
handle. Both timing blocks completed 140 validated batches each.
On the VM both quiet checks pass, all workers join, and the archive
verifies after fixture cleanup. Preparing the VM reclaimed 11
redundant archived copies after matching their bytes to committed
archives under the existing lease.

[Quiet observations](quiet/observations.tar.xz) ·
[Quiet terminal status](quiet/terminal-status.json) ·
[Quiet procedures and test logs](quiet/procedures.tar.xz) ·
[Local observations](observations.tar.xz) ·
[Frozen images](quiet/identity.json)
