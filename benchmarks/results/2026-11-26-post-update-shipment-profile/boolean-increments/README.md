# B1766 Exact boolean increments in the integer path

The [current shipment profile](../README.md) identifies ordinary
arithmetic as a repeated route. A separately instrumented build of
`7f7c4ed59` shows that each shipment variant performs 2,048
machine-integer additions with an evaluated boolean operand. Every
one declines machine arithmetic and enters the generic path.

The patch sends those operations through existing machine
addition and result publication. It accepts the exact boolean only
as the right operand of augmented addition, after the left operand
already qualifies as a machine integer. Native integer overflow and
other operand shapes keep ordinary Python execution.

| Workload, each hosting mode | Boolean updates using integer path, reference → candidate | Boolean updates using generic path, reference → candidate |
| --- | ---: | ---: |
| Shipment callback report | 0 → 2,048 | 2,048 → 0 |
| Bound shipment report | 0 → 2,048 | 2,048 → 0 |
| JSON totals, file-fed map and Unicode text | 0 → 0 | 0 → 0 |

Both full 32-file images verify independent CPython output and file
goldens on all five workloads. Local allocation
changes by at most 940 B and peak admission matches on every workload.
Windows .NET 11 passes 375 public focused Release checks. The targeted
case agrees with CPython on computed boolean increments, retained
identity, signed overflow and custom in-place dispatch. Ubuntu .NET 10
passes 337 white-box and 1,182 public focused Release checks. The repeated
quiet measurement confirms lower warm async shipment CPU. The sync CPU
result is mixed across blocks. [Timing decision](confirmation/README.md).

[Reference routes](reference-routes.json) · [Candidate routes](candidate-routes.json) ·
[Allocation](allocation-summary.json) · [Identity](identity.json) ·
[Patch, tests and procedures](procedures.tar.xz).
