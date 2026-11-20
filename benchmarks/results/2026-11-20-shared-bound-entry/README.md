# B1157 shared bound invocation entry

The linear call driver, callbacks and borrowed leaf calls use one
transition from final bound slots into deferred execution. This deletes
the duplicate borrowed entry and lets eligible callbacks complete before
allocating a context. A real context requirement still promotes the
exact live invocation. The fixed managed native buffer remains available.

The measured pair is `9bdd10a70` and `87a269f2b`. Windows .NET 11 and
Ubuntu .NET 10 each passed 3,714 public and 1,005 white-box focused Release
checks. The callback lifetime cases include a JSON hook that prepares a
nested definition, genuinely suspends on a host read, and completes,
cancels or reaches terminal memory refusal. Each run drains and checks
allowance reuse. Both 32-file images passed five CPython goldens.
The initial catalog rejection and corrected empty output-file goldens
are preserved with the procedures.

Two callback scripts read 768 records. The JSON hook returns a function
whose default and capture use the decoded record. The sorting key calls
a local helper that captures its record. The factory controls use eager
annotations and retained captures. The positional CSV invoice remains
the unchanged I/O control.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Warm elapsed includes mediated RAM-backed I/O, golden checks
and prior-result cleanup. Compile-to-result adds compilation and
compiled-owner disposal. Startup and fixture reset are outside the guest
clock. Process CPU includes the wider worker boundary. Ranges cover
all four pairings of duplicate process medians.

| Workload | Phase | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| JSON object hook rules | Warm | −10.91…−6.43% | −10.33…−8.02% |
| Record sorting key | Warm | −3.43…−0.49% | −2.76…−1.47% |
| Eager annotated rules | Warm | −3.82…−1.51% | −2.65…−2.06% |
| Captured rules | Warm | −1.36…+1.97% | −0.43…+1.96% |
| CSV invoice | Warm | −1.48…+1.10% | −1.92…+1.96% |
| JSON object hook rules | Compile-to-result | −9.46…−8.68% | −9.77…−8.17% |

Warm allocation falls 28.84% for JSON and 21.66% for sorting. The three
controls allocate the same amount in both images. All accounted peaks
match, and the shared allowance returns to baseline after cleanup.
Captured rules and invoice timing remain inconclusive.

Retain the change for the simpler transition and supported callback
improvements. The timing results compare Lython revisions. CPython
supplied the correctness goldens. Both quiet gates and all 120 measured
batches passed. All workers joined, fixtures were removed and the VM
verified the persistent archive under the lease.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).
