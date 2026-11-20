# B1169 combined function and annotation admission

Executable function definitions with evaluated annotations now admit the
callable and its final annotation storage together. The function retains the
private value array until Python reads the dictionary. This removes the
separate attachment reservation. A failure releases the combined charge
while it is still private. Eager expression order and decorator visibility
stay the same.

The reference is `ab2aa9554`. Measured `02639bec8` was retained as
`f3b9957bd`. Windows .NET 11 and Ubuntu .NET 10 each passed 489 focused
Release checks. Both complete images matched five CPython goldens. Local
allocation and accounted peaks stayed effectively flat, and every workload
restored the borrowed allowance.

| Workload | Boundary | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: |
| Eager annotated rules | Warm | −0.79% to +3.83% | −1.17% to +3.85% |
| Nested annotated factories | Warm | −1.05% to +0.40% | 0.00% to +0.78% |
| Eager rules, annotations read each definition | Warm | −5.88% to −2.31% | −4.07% to −2.92% |
| Ordinary default closures control | Warm | −0.41% to +0.63% | −0.92% to +0.26% |
| Positional invoice control | Warm | −1.93% to +1.58% | −2.60% to +1.96% |
| Eager annotated rules | Compile to result | −5.39% to −3.54% | −4.53% to −3.62% |

Ranges cover the four duplicate-process median pairings on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, 2026-11-20 UTC. Warm elapsed
includes mediated file I/O where applicable, golden checks and prior-result
cleanup. Compile-to-result adds compilation and code-owner disposal.
Process CPU covers the wider batch.

Retain the simpler admission path and the gains in the read-every-definition
and compile-to-result workloads. The eager warm candidate medians differ
by 3.94%, and both CPU and elapsed have adverse pairings. Nested warm
elapsed spans zero. The sizes of those effects remain uncertain.
All 120 timed batches and both quiet gates passed. Workers joined and
the archive was verified after fixture cleanup under the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz) ·
[Exact reference core](../2026-11-20-cpython-dataclasses-and-factories/measured-core.dll.gz).
