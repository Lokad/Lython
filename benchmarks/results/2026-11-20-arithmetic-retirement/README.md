# B1177 operand retirement after arithmetic

Private `49a59b8ce` reuses the arithmetic path's ownership check and clears
retired references without scanning for private cleanup again. Both paths
complete the same prepared result consumer. They preserve pending disposal,
cancellation and refusal before a binding becomes visible. The reference is
the frozen `48c661256`, whose production, test and worker sources match
`33481a879`.

| Warm workload | Guest elapsed change | Process CPU change |
| --- | ---: | ---: |
| Integer reduction, 65,536 iterations | −1.85% to +1.02% | −1.03% to +1.04% |
| Eager annotated rules, 2,048 definitions | +2.21% to +3.91% | +0.92% to +8.34% |
| Nested annotated factories, 2,048 definitions | −0.69% to +4.87% | −0.40% to +3.21% |
| File-backed CSV invoice, 1,024 rows | +0.18% to +3.74% | +0.64% to +3.30% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. The elapsed boundary includes guest I/O, golden checks and
prior-result disposal. Startup, compilation, fixture reset and final
teardown are outside the guest clock. Process CPU covers the wider batch.
The ranges cross the medians of two independent processes per revision.
All 80 batches passed and the persistent archive was verified under the
lease after the workers joined and the guest fixtures were removed.

Each 32-file image matched four CPython goldens. Windows .NET 11 and Ubuntu
.NET 10 each passed 1,656 focused Release checks, including operand disposal,
async cancellation and terminal admission refusal. Warm allocation changes
round to zero, accounted peaks match and all four workloads restore the
shared allowance in the local diagnostic.

Keep the candidate private. The scalar result spans zero and every eager
factory pairing is slower. The file-backed invoice also uses more CPU.
Skipping the scan changes too little work to justify the measured cost of
restructuring publication. This pair does not isolate which instruction
layout or inlining change produced the factory and invoice results.

Two staging preflights found insufficient margin before mutating the VM.
The successful launch kept the 168 MiB reserve and retired only exact
binary replicas with source images and observations verified against
committed archives.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
