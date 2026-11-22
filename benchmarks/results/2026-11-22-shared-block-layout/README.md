# B1344 Store operand depths and instruction offsets together

Private `46c7e347f` replaces two temporary arrays with one eight-byte
record per block. Default entries remain unreachable. The same table
serves final instruction, diagnostic span and exception region
construction. The reachability and physical layout passes remain.

Compared with `fd41c741e` on November 22, 2026 UTC. The quiet Azure
F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3. Both 32-file images use the
same worker and optional PythonRe 0.4.0 adapter. Each revision has two
processes with twelve seconds of warmup and seven rotated batches
apiece. Changes span the four process-median pairings.

Compilation includes code-owner disposal. Time to result adds
execution, mediated RAM-backed I/O and prior-result cleanup.
Process CPU also includes request handling. Startup and final teardown
are separate. Both images match the same CPython goldens.

| Workload | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Sparse generated validator | Compile | −1.85 to −0.23% | −2.82 to +0.88% | 0.99 / 0.66% |
| Postponed nested factories | Compile | +0.43 to +0.94% | +0.38 to +1.00% | 0.21 / 0.30% |
| Inherited dataclass | Compile | −3.19 to +0.62% | −2.33 to +1.20% | 1.77 / 2.12% |
| Dead-heavy validator | Compile | +0.18 to +2.87% | −0.99 to +1.53% | 1.11 / 1.56% |
| Sparse generated validator | Time to result | −0.55 to +1.77% | −0.25 to +1.53% | 2.08 / 0.24% |

Keep this form private. Combining two arrays reduces local compiler
allocation by 1,640 bytes for the dataclass, 136 for the postponed
factory and 72 for the dead-heavy fixture. The wide validator's local
allocation varies. The postponed factory's elapsed and CPU costs rise
with narrow same-source spreads. Dead-heavy elapsed also rises while
its CPU and validator time to result span zero. The smaller table alone
does not remove a full traversal. Revisit the layout together with a
design that resolves only demanded branch targets and eliminates the
proactive block-target pass.

Windows .NET 11 and Ubuntu .NET 10 each pass 362 white-box and 1,992
public focused Release checks. The independent inspection matches
79 executable code objects across 11 scripts, including native layout,
operand spans and exception routing. Both compilation owners refund.
The original Windows public run lacked a built Release probe. The
same source passed after building the probe, and the original log
is preserved. The rebased source passes both platform checks.

All 140 timed batches and both quiet gates passed in 353 seconds.
Workers joined and the archive verified under the lease after fixture
cleanup. Only two aliases of an exact archived working core retired.

[Timing](summary.json) · [Compiler allocation](compile-allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
