# B1171 combined default container admission

Private `e4632503c` groups the positional and keyword default containers
under one initial admission. The function signature supplies a shared
keyword layout for executable, lowered and generated defaults. This
removes a reservation and the rollback of a partly constructed pair.
The initial implementation assumed generated fields were grouped by
parameter kind. Three dataclass cases on each platform caught interleaved
fields. The corrected implementation groups the already-evaluated generated
values by role and preserves order within each role.

| Workload | Boundary | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: |
| Eager annotated rules | Warm | +3.38% to +5.77% | +2.60% to +5.60% |
| Nested factories | Warm | −1.55% to +0.40% | −0.28% to +0.86% |
| Ordinary default closures | Warm | −4.14% to +0.44% | −5.46% to +1.04% |
| Generated keyword dataclasses | Warm | +0.31% to +1.54% | +0.12% to +2.34% |
| CSV invoice control | Warm | −2.09% to +0.06% | −2.63% to −0.03% |
| Eager annotated rules | Compile to result | −6.41% to +4.15% | −7.14% to +3.53% |

Four duplicate-process median pairings compare source `9b14997e4` and
the prototype on Ubuntu 24.04.4 and .NET 11 RC3, 2026-11-20 UTC on the
quiet Azure F4as_v7. Warm elapsed includes mediated I/O, golden checks
and prior-result disposal. Compile-to-result adds compilation and disposal
of its owner. Process CPU covers the wider batch. Eager compile-to-result
has an 8.72% reference spread. The eager warm regression holds across
all pairings.

Factory and compile allocations were effectively flat, as were most
accounted peaks. The keyword dataclass variant allocated 0.27% more and
its accounted peak rose 3,072 bytes. All shared allowance balances returned
to baseline. Both images matched seven CPython goldens and each platform
passed 1,195 focused Release checks after the ordering fix.

Keep this design private. The source removes an admission and simplifies
rollback. Those savings do not establish lower CPU, and eager warm work
increased. Revisit only with evidence that identifies the warm cost or a
design that removes another construction or transport step.

The initial staging check found insufficient disk margin before touching
the VM image. The revised staging reused the frozen reference and retired
only older replicas verified against committed archives, including one
duplicate observation JSON. The remote archive and committed observations
remain available. All 120 timed batches and both quiet gates passed.
Workers joined, fixtures were removed, and the archive was verified under
the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, original failures and checks](checks-and-procedures.tar.xz) ·
[Exact reference core](../2026-11-20-funded-definition-construction/measured-core.dll.gz).
