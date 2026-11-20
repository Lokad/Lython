# B1201 Inline lexical definition prototype

Compared private `75e11995f` with `f65be4da5` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. The prototype
uses the current inline operand slots for default construction and eligible
function and lambda creation. Scope-sensitive declarations transfer through
the existing continuation path. The prototype still has separate
completion code for array-backed and inline declarations.

| Warm workload | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Eager annotated rules | −31.87% to −28.02% | −31.55% to −28.35% | 1.93% / 3.65% |
| Nested factories | −13.91% to −9.11% | −15.50% to −10.81% | 3.52% / 1.99% |
| Postponed annotated rules | −30.68% to −29.60% | −30.80% to −30.18% | 1.21% / 0.34% |
| CSV invoice control | −4.78% to −0.85% | −5.75% to −1.67% | 3.43% / 0.67% |

Ranges cover the four combinations of duplicate process medians. Each
process warmed for at least eight seconds. Five rotated rounds used
requests targeted at 0.4 seconds. Guest elapsed includes execution,
mediated RAM-backed I/O where applicable, checks and prior-result disposal.
Process CPU covers the request loop. Compilation, process startup, fixture
reset and final teardown are outside guest elapsed.

Both complete 32-file images passed four workload goldens. Windows .NET 11
and Ubuntu .NET 10 each passed 2,843 focused public Release checks.
Ubuntu also passed 1,438 focused white-box tests. Local warm CLR allocation
fell 136 bytes per eager and nested run. All four runs returned their
shared allowance to baseline.

The three factory gains were much larger than the change on the CSV
control. The control's movement limits attribution of small gains. The
prototype prompted a confirmation using shared lexical construction.
Both quiet gates and 80 timed batches passed. Workers joined and the
persistent archive verified under the VM lease.

[Timing](summary.json) · [Observations](observations.tgz) ·
[Source, complete image identities and checks](procedures.tar.xz).
