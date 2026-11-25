# B1724 Receiver-first executable subscription dispatch

The prototype reads the subscription receiver once and tries a machine-integer
operand only on exact lists, tuples, bytes and strings. This removes the
separate sequence probe in the ordinary executable driver and skips integer
decoding for other receivers in the native prefix. Prepared type subscriptions
keep their original operands through callback suspension. Sequences still
read unpublished integer slots and return canonical tuple elements.

The source-equivalent reference is `93eead293`. Only the core DLL differs
between the two frozen 32-file images. Both verify four complete CPython
goldens, including the 4,096-row plain-reader invoice with positional indexes.
Two workers per image warmed for seven full-request seconds and supplied seven
rotated batches per phase on November 25, 2026 UTC. The Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3. The table spans all four pairings of worker
medians. Warm elapsed includes mediated RAM-backed I/O, verification and
result cleanup. Wider process CPU also includes fixture reset and framing.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Quoted invoice | Warm async | +0.56% to +3.88% | +1.33% to +4.05% | 1.24% / 2.03% |
| Defaulted rules | Warm sync | −1.73% to +5.74% | −1.60% to +7.84% | 2.01% / 5.47% |
| Plain-reader invoice | Warm sync | −2.75% to +1.59% | −3.30% to +1.27% | 4.28% / 0.17% |
| File-fed shipment map | Warm async | −8.18% to +5.93% | −7.96% to +3.64% | 9.72% / 5.15% |
| Quoted invoice | Warm sync | −0.44% to +0.69% | −0.75% to +1.30% | 0.09% / 1.04% |

Peak admission matches in the local sync diagnostic and shared balances
return to baseline. CLR allocations differ by at most 0.024% across the
four workloads. The unchanged rules control has wide spread. File-fed map
and positional reads span zero. The quoted invoice async comparison uses
more CPU across both workers. Keep the patch private because a complete
workload has an adverse result and the remaining cases establish no benefit.
A follow-up needs a broader dispatch design or a concrete cause for the
adverse result.

Windows .NET 11 and Ubuntu .NET 10 pass 551 focused nonstress Release
checks each, covering evaluation order, retained aliases, awaited subscription
callbacks and CSV ownership. All 140 timed batches and both quiet gates pass.
All workers joined, guest fixtures were removed and the archive verified.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Source and checks](procedures.tar.xz).
