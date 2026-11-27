# B1850 Isolate the ready collection insert change

The comparison uses the exact [B1847](../2026-11-27-direct-prepared-inserts/README.md)
reference and candidate images. Their cores correspond to `4919f6748` and
`9cee6f423`. The nested JSON and Unicode text scripts come from
[B1849](../2026-11-27-post-ready-insertion-cpython/README.md). The bound
shipment fixture also matches B1847. Its initial worker order is reversed
from the earlier run. Both images pass all three independent CPython
output and file goldens on Windows and on the VM.

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 27,
2026 UTC. Two workers per image warm for at least five seconds each and
provide seven rotated batches. Ranges span all pairings of worker medians.
Warm elapsed includes applicable mediated RAM-backed I/O, verification
and previous-result disposal. Process CPU also includes fixture reset and
request framing. Compilation and startup sit outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested JSON document | Sync | −1.75% to −0.88% | −1.37% to 0.00% |
| Unicode word-count control | Sync | −1.23% to +4.38% | −3.41% to +4.30% |
| Bound shipment | Async | −5.41% to +0.53% | −3.90% to −0.23% |

The direct comparison leaves the higher JSON timing in B1849 unexplained.
Its elapsed range decreases in this source pair and its CPU range reaches
zero. Bound async CPU reverses the original 0.40–1.22% increase. The text
control varies, with 5.78% candidate CPU spread. Preserve the original
and repeated measurements and close this bounded confirmation.

Both quiet gates and all 84 batches pass. Staging reuses all image files
for both roles and streams 271,450 B of new inputs. All workers join,
RAM-backed fixtures are removed and the persistent archive verifies.

## Deferred definitions and indirect scope observation

A one-argument `next(observer)` can expose the caller's bindings. The
observer's `__next__ = staticmethod(locals)` retrieves a dictionary with
the earlier callable and its original positional and keyword defaults.
The delivered compiled tape marks the callable as a deferred definition.
CPython 3.13.2 produces the same result. The new regression passes alongside
the focused defaults and deferred-binding suites in both Release targets,
38 cases per target, and checks the execution allowance returns to zero.
An argument-count check alone cannot prove that pending definitions are
unobservable. [Regression](../../../tests/Lokad.Lython.PublicApi.Tests/PreparedFunctionDefaultsTests.cs).

[Timing summary](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Staging](stage-custody.json) · [Goldens and procedures](procedures.tar.xz).
