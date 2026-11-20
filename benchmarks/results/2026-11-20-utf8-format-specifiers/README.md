# B1172 native UTF-8 format specifiers

Retained `8643ebfd8` against `e631ab53c`. A single byte grammar serves
`str.format`, f-strings and builtin `format`. Expanded methods reuse one
funded buffer, and a Python `__format__` hook receives its backing after
binding. This removes the expanded-spec UTF-16 conversion, its scratch lease,
and the second encoding for callbacks. Builtin `format(value, spec)` passes
the original spec to the hook. Datetime formatters borrow bytes directly.
Published native exceptions fund their diagnostic text during adoption.
An initial revision charged retained datetime parsing errors twice.
Deleting the date-specific message commitment restored the existing behavior.

| Workload | Boundary | Windows warm allocation | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Two nested widths | Warm | −15.28% | −10.93 to −8.33% | −10.79 to −7.49% |
| Two Unicode fills and nested widths | Warm | −15.21% | −9.17 to −5.50% | −8.74 to −4.49% |
| Shipment selectors | Warm | −1.18% | −3.72 to +0.69% | −4.17 to +2.13% |
| Shipment selectors | Compile-to-result | — | −1.48 to +1.31% | −5.10 to −0.41% |
| Named formatting | Warm | −1.43% | −2.04 to +1.08% | −3.73 to +0.16% |
| Positional invoice control | Warm | Flat | −2.42 to −1.17% | −2.60 to −0.65% |

Negative changes favor the candidate. Ranges cover all four pairings of
duplicate process medians on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 on 2026-11-20 UTC. Warm includes mediated I/O, goldens and prior-result
disposal. Compile-to-result adds compilation and code-owner disposal.
Process CPU covers the wider invocation loop. The invoice moved without an
allocation change. The nested workloads still improve beyond that movement
in every pairing. Shipment and named-format elapsed are inconclusive.
Forwarding was checked for correctness and allocation only, with flat allocation.

Both 32-file images matched six CPython goldens. The borrowed allowance returned
to baseline in the allocation check. Accounted nested peaks fell by 918 and
974 bytes, and other peaks matched. Windows .NET 11 passed 2,275 focused checks.
Ubuntu .NET 10 passed 1,556. Coverage included cancellation with a pending guest
format hook, terminal refusal, escaping aliases and recovery of unused allowance.
All 120 quiet batches verified their output and files. Both machine gates passed.
Workers joined, the fixture mount was removed and the archive verified under
the lease.

[Pairing summary](summary.json) · [Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
