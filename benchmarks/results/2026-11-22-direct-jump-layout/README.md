# B1332 Resolve jump targets directly from emitted blocks

Private `f069dc7ab` uses the target array itself to mark the current
walk and resolves chains against the emitted block contents. This
removes a full successor scan, the successor array and the path array.
Cycles retain their individual entry targets. Protected entry blocks
retain their own identity and operand depth.

Compared `52611f900` and `f069dc7ab` on November 22, 2026 UTC
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Both frozen images use the same worker and adapter. Two processes per
revision warm for at least twelve seconds and supply seven rotated
batches each. Ranges cover the four process-median pairings.
Compilation includes code-owner disposal. Time to result adds mediated
RAM-backed execution and prior-result cleanup. Process CPU covers the
wider request. Startup and final teardown are separate.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Sparse generated validator | Compile | −1.32 to +1.44% | −2.61 to +1.10% | 1.23 / 1.54% |
| Postponed factory | Compile | +0.17 to +1.60% | −0.33 to +1.70% | 1.32 / 0.11% |
| Inherited dataclass | Compile | −1.66 to −0.19% | −2.00 to −0.59% | 0.45 / 1.04% |
| Dead-heavy control | Compile | −2.66 to +0.78% | −1.69 to −0.56% | 2.10 / 1.41% |
| Sparse generated validator | Time to result | −2.83 to +1.29% | −2.68 to −0.25% | 1.59 / 2.61% |

Retain the removed work and scratch storage. The factory's small
elapsed regression is a measured tradeoff. Validator time to result,
dataclass compilation and dead-heavy compilation use slightly less
process CPU. The validator's elapsed result spans zero. The local
allocation diagnostics show small reductions where controls agree.
They do not establish a whole-script elapsed improvement.

Windows .NET 11 and Ubuntu .NET 10 each pass 355 white-box and 1,400
public focused Release checks. Inspection matches 79 final code objects
across 11 scripts and all compile owners refund. Both 32-file images
match four CPython goldens. All 140 timed batches pass, workers join and
the persistent archive verifies under the quiet lease. The procedures
preserve the initial headroom rejection and the attempt naming an
already retired copy. The successful staging retired only two working
copies whose contents survive in verified archives.

[Timing](summary.json) · [Allocation](compile-allocation-summary.json) ·
[Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz).
