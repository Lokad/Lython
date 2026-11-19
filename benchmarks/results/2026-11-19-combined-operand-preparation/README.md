# B1068 combined executable operand preparation

The private R638 prototype `79ad2e6ba` reuses the builder's block list and
prepares branch targets, direct call results and operand forwarding in
one instruction scan. The reference is `12fabffb1`. Both 32-file images
passed four CPython goldens. Windows .NET 11 and WSL .NET 10 each passed
411 focused Release cases for control flow, operand ownership, direct
calls, compiler cancellation and shared memory. The original WSL run
missed three fixture directories in its separate build output. The
complete rerun used the repository fixtures and the same assertions.

Local synchronous compilation allocations fell 0.33% for manifest and
0.11% for validator. Config edit and shipment allocated 0.23% and 0.42%
less respectively. The borrowed allowance returned to baseline after
each compiled owner was disposed.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
2026-11-19 UTC. Ranges cover all four pairs of duplicate process medians.
Negative changes favor the prototype.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration manifest | Compilation | +0.54…+1.25% | −0.96…+1.00% |
| Configuration manifest | Compile-to-result | −4.23…+0.48% | −3.41…+0.25% |
| Generated validator | Compilation | −1.00…+1.12% | −2.49…+1.52% |
| Generated validator | Compile-to-result | +0.89…+2.66% | +1.49…+2.93% |
| Shipment report | Warm control | −2.98…−1.28% | −4.19…−0.78% |

The validator's compile-to-result regresses with higher CPU. Compiler-only
CPU remains mixed on both targets and the warm shipment control moves
favorably. Keep the combined scan private. Revisit block layout when a
design can remove intermediate storage without adding classification to
every instruction in the forwarding pass. The current results do not
identify the cause of the validator runtime movement.

Both quiet gates and 100 timed goldens passed in 232.63 seconds. Workers
joined, guest fixtures were removed and the archive verified under the
lease. The VM needed disk headroom first. Old test build outputs were
archived and checked byte-for-byte before moving them off the VM. The
measured case definitions and staging and archival gates were preserved.

Compilation includes compiled-owner disposal. Compile-to-result adds
execution, mediated I/O, golden checks and cleanup. Warm includes mediated
I/O, goldens and previous-result cleanup. Process CPU has a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) ·
[Compiler attribution](../2026-11-19-current-frontend-profile/README.md).
