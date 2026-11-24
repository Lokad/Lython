# B1607 Caller-cursor-free default construction

The private refinement `6816cfa1e` stores each creation index in the
existing instruction. Single definitions and pairs still share one
constructor. Its caller no longer passes a mutable instruction cursor.
The reference runtime matches `f6d06f638`, as in B1606.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. The two 32-file images differ only in the core
DLL. Each source uses two warmed workers with seven rotated batches
each. Changes span all pairings of worker medians. Positive numbers
mean more time. Warm execution includes applicable mediated RAM-backed
I/O, result verification and prior-result disposal. Process CPU also
covers reset and request framing. Compile to result includes one
compilation, execution and cleanup in the warmed host.

| Workload and boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted rules, warm | −0.45 to +3.72% | −0.81 to +4.18% | 2.81 / 1.33% |
| Postponed annotated rules, warm | +3.95 to +12.30% | +2.18 to +14.94% | 3.00 / 4.88% |
| Eager annotated rules, warm | −0.10 to +1.76% | −0.57 to +0.42% | 0.61 / 1.24% |
| Captured rules, warm | −1.56 to +6.12% | −2.70 to +4.63% | 0.73 / 7.01% |
| Quoted invoice, warm | −0.01 to +5.11% | 0.00 to +5.45% | 1.26 / 3.82% |
| Defaulted rules, compile to result | +0.38 to +3.41% | +2.23 to +2.78% | 0.004 / 3.01% |

The Windows FullOpts shared entry returns to a 72-byte frame and
876 code bytes against the reference's 879. The combined default
constructors go from 2,966 to 1,888 code bytes. Allocation, admitted
execution peaks and retained compiler admission match on all six
scripts. The defaulted warm improvement in B1606 does not recur.
Postponed warm time and CPU increase despite the smaller entry.
This result leaves the slowdown's cause open. Both variants stay
private. Production retains the existing direct readers.

Windows and Ubuntu each pass 246 focused Release checks. Six independent
programs agree with CPython 3.13.2 in both execution modes and images.
Six goldens, 168 timed batches, both quiet gates, worker joins, fixture
cleanup and the persistent archive pass. The first launch stopped at
the disk preflight. Two inactive cores were then compressed and verified
against their original digests. Their complete images remain available
locally and the VM keeps the exact compressed bytes.

[Timings](summary.json) · [Image identity](identity.json) ·
[Runtime allocation](allocation-summary-six.json) ·
[Compiler ownership](compiler-owner-summary.json) · [Generated code](jit-summary.json) ·
[Raw observations](observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Compaction record](compaction.jsonl) · [Terminal status](terminal-status.json) ·
[First candidate](../README.md).
