# B1711 Compiler-proved default stores

The private prototype links already proved default stores in the existing
tape. Single and paired factory construction use one producer. It executes
the existing arithmetic over the proved range and no longer inspects each
opcode to find the range end. Partial proofs leave the ordinary execution
route intact. Default storage and callable admission remain separate.

The reference is `7d24cfe9a`, with the same production and test sources
as `75af75769`. Both frozen images differ only in the core DLL and
verify seven complete CPython goldens. The Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 25, 2026 UTC. Two processes per source warm for
eight seconds and contribute seven rotated timed batches each. Ranges span
the four pairings of worker medians.

Warm elapsed includes applicable mediated RAM-backed I/O, output verification
and result disposal. Compilation includes compiled-owner cleanup.
Compile-to-result combines compilation, execution and cleanup in an
already-running host. Wider CPU includes reset and request framing.

| Workload and boundary | Elapsed change | Wider CPU change | Elapsed spread reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted pairs, warm | −3.43 to +2.47% | −3.33 to +5.07% | 2.34 / 3.68% |
| Eager annotations, warm | +4.70 to +6.34% | +4.70 to +6.76% | 0.33 / 1.22% |
| Postponed annotations, warm | −2.72 to +3.44% | −2.46 to +5.79% | 4.92 / 1.34% |
| Defaulted pairs, compile-to-result | −0.53 to +6.14% | −0.91 to +8.37% | 1.66 / 4.96% |
| Ordinary definitions, compile-to-result | −0.63 to +2.74% | −0.44 to +1.85% | 1.52 / 1.84% |
| Live-alias definitions, warm | −0.72 to +0.81% | −2.66 to −0.22% | 0.44 / 1.10% |
| Shipment control, warm async | −1.88 to +0.27% | −3.46 to −0.90% | 2.08 / 0.11% |
| Defaulted pairs, compile-only | −0.72 to +1.33% | −2.01 to +2.42% | 0.94 / 1.11% |

Keep the prototype private. The defaulted improvement remains uncertain in
all three phases. Eager annotations use 4.70–6.76% more CPU at low worker
spread. The unchanged shipment control uses less CPU. Compilation
allocation, retained compiler allowance and sampled runtime peaks match.
The three affected rule cases also match in local runtime allocation.
No component-specific cause is established. Further work on function
construction needs a distinct storage or invocation stage to remove.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,200 public and 563 white-box
Release checks. They cover both admitted owners, refusal and cancellation
during admission, rollback and live aliases. A separate comparison against
CPython 3.13.2 exercises negative and wide defaults and captured keyword
aliasing in both hosting modes. All 224 timed batches and both quiet gates
passed. Workers joined, guest fixtures were removed and the archive
verified under the lease.

[Source and image identity](identity.json) · [Timing](quiet-summary.json) ·
[Runtime allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patch](procedures.tar.xz).
