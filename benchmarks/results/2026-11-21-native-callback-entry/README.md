# B1256 and B1257 native entry for exact positional callbacks

Both runs measure the complete Lython reference `a5a1d4492` and frozen
candidate `8ad55d9d5` on November 21, 2026 UTC. The Azure F4as_v7 runs
Ubuntu 24.04.4 and .NET 11 RC3. The candidate is retained at
`d6a360e23` with the separately delivered dictionary iterator change.

Fully supplied positional callbacks can bind directly into the existing
managed native slots. This removes owned workspace rental and retirement
when the native body completes. A transferred body rents final owned
slots at its current instruction and rejoins the existing invocation
and cleanup path. General binding still handles keywords, omitted
parameters and larger or different call shapes.

| Campaign and complete warm workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| B1256 map-based JSON normalization | −3.15 to +0.35% | −2.46 to −0.33% |
| B1256 map-based text report | −22.30 to −7.24% | −21.23 to −7.88% |
| B1256 ordinary-call JSON normalization control | −1.85 to +1.19% | −2.14 to +0.62% |
| B1257 map-based text report | −10.07 to −6.00% | −8.89 to −5.61% |

The text report reads 1,024 lines and 35,840 input bytes, aggregates
normalized words and writes a report. Each range crosses all pairings
of two independent worker medians per revision. Each worker contributes
seven batches. The text reference medians differ by 16.19% initially
and 4.17% on the exact-image confirmation. B1257 reverses worker order
and uses longer warm-up and batches. Both clocks confirm a text gain.
The JSON callback's elapsed improvement remains inconclusive, and the
ordinary-call control spans zero.

Warm time includes mediated RAM-backed I/O, output verification and
previous-result disposal. Compilation, startup and final teardown are
outside the clock. CLR allocation stays within 0.07% in the local
diagnostic. The accounted peak falls by 232 bytes for map-based JSON
and 184 bytes for text, and stays flat for the control. All executions
return the borrowed allowance to baseline. Both frozen 32-file images
pass all three CPython goldens. The candidate passes 2,879 focused
public, 789 white-box and 19 separately selected cancellation and
sort checks per platform.

Both VM runs pass quiet checks, join all workers and verify their raw
archives under the lease. Two initial stage attempts stop before
mutation, first for free space and then for a missing archived replica.
The successful stage checks all links, exact binary bytes and retained
archives before reclaiming 91,613,184 bytes of duplicate staged cores.
The original failed conditions and selected reclaim evidence are in
the procedure archive.

[Initial summary](summary.json) · [Initial observations](observations.tgz) ·
[Confirmation summary](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tgz) ·
[Allocation diagnostic](allocation-summary.json) ·
[Source, verification and procedures](checks-and-procedures.tar.xz) ·
[Confirmation procedures](confirmation/procedures.tar.xz).
