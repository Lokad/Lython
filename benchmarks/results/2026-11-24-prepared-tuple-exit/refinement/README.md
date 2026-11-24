# B1584 Isolate prepared tuple admission from shared dispatch

`55128bf1c` retains the production and test source measured at
`6779796fd`. The refinement keeps scalar admission in the existing
preparation helper. The helper releases its private slots on interruption
and keeps the tuple's source position for refusal. Both drivers retire
the prepared operand sequence without a second tuple dispatch or scalar
estimate scan. Materialized local values keep their identities. Dynamic
reads, Python protocols and escaping tuples use the original tape.

The earlier source expanded both interpreter loops. Windows x64 FullOpts
code inspection now shows the native loop at 5,642 bytes versus 5,670
in production and 5,955 in the first prototype. The framed loop returns
to 12,620 bytes. The prepared helper grows from the former 862 bytes to
1,168 bytes. This inspection uses tiering disabled. The timed workers
use normal runtime settings. [First comparison](../README.md).

## Complete-script timing

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Compared the same frozen `b8c2aa8c8` reference
with `6779796fd`. Every file except the core DLL is identical in the
verified 32-file images. All five scripts verify their CPython 3.13.16
goldens.

| Warm script | Elapsed change | Wider process CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Captured rules | −1.72 to +0.65% | −1.96 to +0.93% | 0.52 / 1.88% |
| Defaulted rules | −10.59 to −2.20% | −10.03 to −2.52% | 3.66 / 5.52% |
| Eager annotated rules | −1.71 to +8.06% | −1.84 to +8.03% | 7.99 / 1.81% |
| Short text report | −3.31 to +5.36% | −2.58 to +4.29% | 5.44 / 3.33% |
| Quoted CSV invoice | −1.39 to +1.10% | −1.96 to +1.28% | 0.99 / 1.52% |

Two independent workers per source warm for eight seconds and supply
seven rotated batches. Ranges span the pairings of worker medians.
Elapsed includes applicable mediated RAM-backed I/O, golden verification
and prior-result disposal. Wider CPU also includes resets and protocol.
Compilation, startup and final teardown sit outside the clocks.

Retain the refined prepared return. Each defaulted pairing improves both
elapsed and CPU, with enough same-source variation to leave the gain's
size uncertain. The other four ranges span zero. Preserve the adverse
eager and text pairings and their reference spread, and carry the existing
text regression debt forward. Admitted peaks match on all five scripts.
Local CLR allocation is equal on four and within 176 bytes on the
3.28 MB invoice. Each execution restores the allowance.

Separate instrumented Windows runs confirm that the defaulted script's
`make_rules` now reaches the path for its callable pair and the invoice's
`price_line` keeps its existing prepared arithmetic route. The other three
scripts never enter it. Both platforms pass 117 white-box and 495 public
focused Release checks on .NET 11, including a pressure case with a prepared
tuple that retains its refusal span and refunds on both entry points.
All 140 timed batches, quiet gates, worker joins, fixture retirement and
persistent archive verification pass.

[Timing and spread](summary.json) · [Allocation](allocation-summary.json) ·
[Prepared-route counts](prepared-routes.json) · [Exact images](identity.json) ·
[Reference core](../reference-core.dll.gz) · [Raw observations](observations.tar.xz) ·
[Procedures and JIT](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
