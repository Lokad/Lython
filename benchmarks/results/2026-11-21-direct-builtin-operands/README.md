# B1276 bind wide builtin calls from executable operands

The existing binder reads evaluated slots directly for wide ordinary
calls to standard builtins and the JSON module wrappers. It creates
the bound values and, where required, the exact-size array of extra
constructor keywords. The caller holds their admission through
callback suspension and cleanup. Dynamic callables and prepared
constructors still use owned argument packets.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
21, 2026 UTC. Reference `798091df6` uses the frozen image built from
source-equivalent `9bcd34aa7`. The final candidate is `5232caad5`.
Each lane has two independent workers per revision and seven rotated
batches per worker. Ranges cross all four pairings of process medians.
Warm elapsed includes mediated RAM-backed I/O, full output verification
and prior-result cleanup. Process CPU covers the wider request.
Compilation, process startup and final disposal sit outside guest clocks.

| Complete script | Warm elapsed change | Process CPU change | Local CLR allocation change |
| --- | ---: | ---: | ---: |
| JSON Lines export, five explicit options | −5.09 to −2.39% | −5.20 to −3.16% | −6.57% |
| Tolerance check, four operands | −5.40 to +5.06% | −4.84 to +5.18% | −32.34% |
| JSON Lines export, expanded options | −3.86 to −2.13% | −3.47 to 0.00% | −0.02% |
| JSON Lines export, two explicit options | −0.75 to +1.70% | −0.65 to +3.17% | −0.05% |
| Guest forwarding report | −3.12 to +0.59% | −3.41 to +1.78% | −0.01% |

The export reads 3,072 records from 189,441 bytes and writes the same
file for all three option forms. The tolerance report performs 4,096
comparisons through a guest helper. Its two reference process medians
differ by 7.97% in elapsed time despite tight batches. Its timing remains
inconclusive. The wide JSON control spreads are 0.91% for the reference
and 1.92% for the candidate. The short JSON case may use more CPU.
Retain the direct path for the measured wide JSON benefit and the removed
argument copy. Record the timing uncertainty on the other workloads.

Windows .NET 11 allocation diagnostics show 7,858,824 → 7,342,640 bytes
for the wide export and 1,519,624 → 1,028,104 bytes for the tolerance
report. The accounted peak rises 128 bytes on the wide export and
matches on the other four scripts. Each execution returns the shared
allowance to its baseline.

The first candidate, `1ee2fab48`, bypassed the packet for standard
builtins only. Its wide JSON allocation stayed effectively flat.
The local Windows elapsed diagnostic placed that export 1.39–15.52%
slower, with substantial variation on duplicate control processes.
The final candidate extends slot binding to the JSON wrappers and
folds extra-keyword collection into the existing binder passes.
The procedures archive preserves the first measurements and the
subsequent ref-struct compile error before the corrected build.

Each candidate passed 840 white-box and 1,368 public Release checks on
Windows .NET 11 and Ubuntu .NET 10. The final wide JSON callback cases
verify retained aliases, cancellation during pending host I/O, joined
cleanup and allowance reuse. Both complete 32-file images passed
all five CPython 3.13.2 output checks on Windows before staging. The
quiet comparison passed all 140 output-checked batches and both machine
gates in 277 seconds. Every worker joined, and the persistent raw archive
was verified under the VM lease.

The collector's inherited header says 16 seconds of warmup and a
0.8-second batch target. The archived loop actually uses a minimum of
8 seconds of full requests per worker and a 0.65-second target. The
original header and service name are preserved in the raw evidence.

[Timing](summary.json) · [Allocation](allocation-summary.json)
· [Initial allocation](initial-allocation-summary.json)
· [Raw observations](observations.tgz)
· [Checks and procedures](checks-and-procedures.tar.xz).
