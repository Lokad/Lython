# B1155 compile-owned postponed annotations

The Python spelling of a postponed annotation is encoded to UTF-8 once
with the compiled script. The code owner reserves its backing and any
scalar index during compilation. Ordinary definition instructions load
the prepared constants, with a fresh mutable annotations dictionary on
each definition. This removes per-definition text conversion, the dedicated
module value instruction and the full invocation required solely to
evaluate prepared metadata in an otherwise eligible factory.

The measured source pair is `e025a7f1c` and `a6928c942`. Each 32-file
image passed six CPython 3.13 goldens. Windows .NET 11 and Ubuntu .NET 10
passed 919 public and 69 white-box focused Release checks per platform.
The two new public regression cases fail on the original core under the
same execution allowance. They cover sharing, Unicode indexing, separate
compilation and result lifetimes, and allowance return through both APIs.
The procedures preserve the initial probe setup failures. Building the
Windows probe and providing the Ubuntu probe with CPython 3.13 resolved them.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Warm elapsed includes mediated RAM-backed file I/O, output
and file verification, and prior-result disposal. Compile-to-result
adds compilation and compiled-owner disposal. Process CPU covers the
wider worker boundary, including fixture reset and protocol. Startup
is outside the guest clocks. Ranges compare the four combinations of
duplicate worker medians. CPython supplied correctness goldens for
these cases. The timing observations compare two Lython revisions.

| Workload | Phase | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Postponed annotated rules | Warm | −22.72…−22.47% | −23.38…−22.10% |
| Postponed nested factories | Warm | −19.76…−16.69% | −19.70…−16.92% |
| Postponed rules with metadata read on each definition | Warm | −18.94…−16.68% | −18.60…−15.30% |
| Postponed annotated rules | Compile-to-result | −24.29…−23.11% | −25.77…−22.24% |
| Single postponed definition | Compile-to-result | −1.19…+4.29% | −1.08…+5.19% |
| Eager annotated rules control | Warm | −1.74…+3.38% | +0.45…+2.62% |
| CSV invoice control | Warm | +0.97…+1.28% | −2.00…+0.66% |

Warm allocation drops 47.97%, 41.14% and 46.86% across the three
postponed factory scripts. Each execution's accounted peak falls
by 1,073,128 bytes. The two eager and CSV controls have effectively
flat allocation and identical peaks. Compile-only allocation increases
0.47–3.09% on the postponed cases. Independent compilation, execution
and result budgets now fund the text, metadata and projected copies
at their respective lifetimes. The comparison confirmed reuse of
the shared allowance after cleanup.

Retain the change for the deleted execution stages and the supported
factory CPU and allocation gains. The single-definition outcome is
inconclusive. The eager control has 4.01% candidate process spread
in guest elapsed and higher process CPU. The smaller invoice elapsed
increase has mixed CPU. Keep both controls in subsequent checkpoints.
The earlier eager-factory CPython measurements retain their scope.

All 140 measured batches matched the goldens. Both quiet gates
passed. Workers joined, fixtures were removed, and the persistent
archive was verified under the lease. The reference core is the
[B1152 measured core](../2026-11-19-prepared-mutation-calls/measured-core.dll.gz).

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).
