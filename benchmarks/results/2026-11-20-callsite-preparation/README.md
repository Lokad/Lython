# B1241 and B1242 call-site preparation during operand forwarding

This change removes the two separate block scans for call-site result
and split-chain information. The existing forwarding traversal reads
original instruction adjacency before rewriting it. Ordinary call sites
keep their original record when no result or split metadata changes.

Both quiet runs compare `785640f8e` and private `0addc9a9c`. Each complete
32-file build verifies four CPython goldens. An independent reflection
diagnostic also matches instruction fields and the examined call-site
metadata in five complete scripts, including immediate case conversion,
split, unpacking and iteration. The first private source pair
`eae706262` and `e260d9cd0` verifies the same four benchmark scripts.

| Workload and phase | B1241 elapsed | B1241 process CPU | B1242 elapsed | B1242 process CPU |
| --- | ---: | ---: | ---: | ---: |
| Generated validator, compile | −6.27% to −2.70% | −5.36% to −2.75% | — | — |
| Generated validator, compile-to-result | −2.56% to +0.53% | −1.67% to +0.31% | — | — |
| Guarded manifest, compile-to-result | −0.69% to +1.82% | −0.51% to +2.58% | −1.68% to +2.68% | −1.55% to +1.81% |
| Expanded report forwarding, warm | +0.42% to +2.72% | +2.30% to +2.39% | −5.16% to +4.07% | −4.07% to +3.69% |

Retain the combined compiler walk for the removed scans, lower compile
allocation and the validator's measured compilation improvement. Both
compile-to-result results span zero. The repeat does not reproduce
uniformly higher forwarding CPU. Its same-source elapsed spreads are
6.45% in the reference and 3.08% in the candidate. The forwarding
measurements leave warm impact unresolved, and the initial adverse
result stays in the record.

The private rebased source reduces median local compile allocation
by 0.36–1.01% across the four scripts. The validator's 2.97% allocation
reduction on the first pair does not recur in full on the rebase.
Individual sample batches show validator allocation falling during
warmup, which limits the comparison of those two source pairs.
The local diagnostic disposes each compiled owner and checks allowance
reuse after every invocation.

The quiet VM was an Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3
on November 20, 2026 UTC. Ranges cover all four pairings of the duplicate
process medians. B1241 uses seven rotated batches per lane and 12 seconds
of warmup per worker. The four lanes took 305 seconds. B1242 reverses
the worker startup order for each repeated lane, warms each worker for
22 seconds and completes in 246 seconds. All 168 batches pass, both
runs pass quiet machine checks, and the persistent archives verify
under the lease after guest fixture cleanup. Warm includes
mediated RAM-backed file I/O, goldens and cleanup of the previous result.
Compile-to-result also includes compilation and code-owner disposal.
Compilation excludes guest work. Process CPU covers the wider worker
request. Startup and final teardown are outside guest clocks.

The first prototype passes 176 white-box and 777 public focused Release
checks on both platforms. On the rebased candidate Ubuntu .NET 10 passes
all 953 and Windows .NET 11 passes 777 public checks. Coverage includes
unpacking, original operand failure order, cancellation, and owned
iteration and tuple returns.

[B1241 samples](summary.json) · [B1242 samples](confirmation/summary.json) ·
[Local compile allocation](rebased-compile-allocation.json) ·
[Source, checks and procedure](checks-and-procedures.tar.xz).
