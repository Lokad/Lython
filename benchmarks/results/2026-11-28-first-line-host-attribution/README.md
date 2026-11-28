# B1982 Attribute the first-line file-search cost

Two warmed .NET 11 RC3 EventPipe captures use `f231ce76a` on one logical
CPU in Ubuntu WSL. Both APIs run the same first-line search and write
the same output file. The independent CPython 3.13.16 golden and all
1,849 sampled batches verify. Both workers and tracers join.

| Managed region, inclusive | `Run` | `RunAsync` |
| --- | ---: | ---: |
| Mediated text file | 66.6% | 64.6% |
| Open | 26.2% | 16.8% |
| First refill and line assembly | 11.5% | 9.1% |
| Write and file cleanup | 27.8% | 38.3% |
| Module and command setup | 3.3% | 1.2% |

The percentages describe resolved managed samples. Inclusive regions
can overlap. The two captures have separate denominators. The nearest
Lython frame often belongs to the host adapter even when the sample
resolves further down the stack. In the open region, path inspection
and file opening (`LStat` and `Open`) account for 61.15 of 85.78
resolved milliseconds in `Run` and 22.31 of 42.16 in `RunAsync`.
File locking and handle release also account for much of the work
under the generic host wrapper. These samples cover the comparison
host and CLR file implementation together with the runtime boundary.

The separate B1978 quiet comparison measures a first-line gap of about
16–17 µs per command. Inspect the comparison host's cost with the same
script before selecting a runtime change. Keep path validation, active
memory admission and awaited close in the measured boundary.
[B1978 comparison](../2026-11-28-current-mixed-cpython/README.md).

[Attribution](attribution.json) · [Host frames](host-leaves.json) ·
[Worker identity](identity.json) · [Summary](summary.json) ·
[Captures and procedures](profiles.tar.xz).
