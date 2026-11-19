# B1090 inline keyword dictionary transfer

A fresh dictionary display passed directly as the sole trailing `**`
argument already owns its backing storage. For a compatible function with
all positional slots filled, its validated mapping becomes the callee's
`**kwargs`. This removes one complete dictionary allocation and copy per
eligible call. Named mappings still produce independent snapshots, and
protocol keys or other argument shapes use ordinary binding.

| Workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Inline dynamic keys, warm | −5.48 to −3.96% | −5.77 to −3.64% | 1.29 / 0.32% |
| Inline dynamic keys, compile-to-result | −3.45 to −2.15% | −4.31 to −1.69% | 0.60 / 0.74% |
| Inline literal keys, warm | −12.95 to −10.09% | −11.85 to −10.20% | 1.75 / 1.51% |
| Named forwarding, warm | +0.80 to +2.78% | +1.16 to +2.94% | 1.50 / 0.46% |
| Mutable bound-method forwarding, compile-to-result | −2.83 to −0.59% | −2.29 to −0.54% | 0.68 / 1.61% |
| CSV invoice, warm | −1.93 to −1.66% | −3.47 to −3.29% | 0.24 / 0.04% |

The two inline scripts each process 1,024 records and produce the same
validated output files as the original named report. Their local warm
allocations fall 21.09% and 24.58%. Accounted peaks fall from 4,940,898 to
3,204,194 bytes and from 4,023,260 to 2,286,556 bytes. Both variants and
all controls return the borrowed allowance to baseline. The mutable
bound-method and invoice controls move favorably, and the named report is
adverse. These comparisons do not isolate why those controls moved.

Retain the transfer as `c3cb1906a` with regression coverage at `6dd12252a`.
The measured delta matches the retained change alongside the independent
compiler and function-construction changes. Preserve the named-path
regression for a refinement that keeps a single keyword-validation contract.
The new test checks original key and value identities, repeated record
construction and mutation, error order, and an asynchronous file read inside
the callee. Both APIs passed and the Python output matches CPython. The
original focused suite passed 397 cases per target on Windows .NET 11 and
Ubuntu .NET 10. Both full worker images matched five CPython catalog goldens.

The pair ran on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3
on 2026-11-19 UTC. Ranges span all four process-median pairs. All 120 timed
batches passed in 275.69 seconds. Workers joined, fixtures were removed and
the persistent archive was verified under the lease. Warm includes mediated
RAM-backed I/O, goldens and prior-result cleanup. Compile-to-result adds
compilation and disposal of its owner. Process CPU has a wider boundary.

[Elapsed, CPU and allocation](summary.json) · [Raw observations](observations.tgz) ·
[Source, goldens, tests and procedures](checks-and-procedures.tar.xz).
