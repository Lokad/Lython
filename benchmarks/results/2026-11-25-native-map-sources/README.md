# B1710 Direct native map inputs

The private candidate retains already acquired native Python iterators
directly. Other sources still use the existing cursor. This removes
per-source cursor allocation and forwarding for native iterators, with
admission estimates held fixed. The 2,048-record shipment script uses
8,192 native input pulls across its nested maps. Each pull still advances
the original iterator. Source acquisition order, suspended prefixes and
cleanup remain in the existing map operation.

The reference is `7d24cfe9a`. The frozen images differ only in the core
DLL and verify three complete CPython goldens. The Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC. Two workers per
source supply seven rotated timed batches per phase. Ranges span all four
pairs of process medians. Warm execution includes applicable mediated
RAM-backed I/O, verification and result disposal. Compile-to-result adds
compilation and compiled-owner cleanup. Wider CPU includes reset and
request framing.

| Workload and boundary | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Shipment warm sync | −2.83 to +2.90% | −1.97 to +2.49% |
| Shipment warm async | −3.08 to +1.66% | −3.14 to +0.92% |
| File-fed map warm async | −7.12 to +6.12% | −4.50 to +5.35% |
| Ordinary definitions compile-to-result | −0.55 to +1.96% | −1.53 to +1.57% |

Keep the patch private. Every CPU and elapsed range crosses zero. The
file-fed candidate's 8.81% elapsed spread is the widest. The local
shipment diagnostic allocates 5,626 B less per invocation (0.43%).
File-fed map allocates 32 B less. Admitted peaks and cleanup balances match.
That allocation saving does not justify the wider iterator interface and
separate pull loop checks by itself. A future cursor consolidation can
reuse the ownership and semantics evidence.

Windows .NET 11 and Ubuntu .NET 10 each pass 480 white-box and at least
1,458 public Release checks. The added alias regression also passes on
both, with an independent CPython 3.13.2 golden. It covers a failed later
acquisition and interleaved advancement of the same bounded iterator.
Existing tests check refusal before acquisition effects, suspended row
cancellation and allowance reuse. Both quiet gates and all 112 timed
batches pass. Workers join, guest fixtures are removed, and the archive
verifies under the lease.

[Source and images](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal status](quiet-terminal-status.json) · [Procedures and patches](procedures.tar.xz).
