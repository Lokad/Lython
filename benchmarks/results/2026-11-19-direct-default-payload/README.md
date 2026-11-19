# B1111 construction in final default containers

Prototype `39fdf4fea` writes each evaluated default into its final positional
tuple or keyword dictionary. Executable declarations and lambdas retain one
private owner while evaluating defaults, and the lowered evaluator uses the
same construction. This removes the separate positional reservation, the
accumulated default operand range and the final positional scan and copy.
The tuple admits capacity and eventual small scalar boxes up front. A large
integer may require an additional charge before insertion. Lambda admission
can now refuse before default callbacks run.

The pair compares `3e67fd010` against `39fdf4fea` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026. The ranges cover
all four combinations of duplicate process medians. Negative means less time.

| Workload | Boundary | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Ordinary rules | Warm | +1.59…+3.89% | +1.99…+4.11% | 0.85 / 1.40% |
| Annotated rules | Warm | −3.02…+2.17% | −3.68…+1.12% | 0.16 / 5.19% |
| Captured rules | Warm | −3.46…−0.58% | −1.66…+0.85% | 1.33 / 1.63% |
| Ordinary rules | Compile-to-result | −2.41…+4.51% | −1.01…+4.12% | 3.24 / 3.72% |
| Computed defaults | Warm `Run` | −3.25…+0.54% | −3.05…+1.90% | 0.13 / 3.78% |
| Computed defaults | Warm `RunAsync` | −2.20…−0.20% | −1.49…+0.36% | 1.18 / 0.85% |
| Named forwarding | Warm | −0.88…−0.67% | −1.58…+0.12% | 0.20 / 0.005% |

Local warm allocation falls from 2,617,480 to 2,388,104 bytes for ordinary
rules (**8.76%**) and from 1,652,848 to 1,538,128 for annotated rules
(**6.94%**). The ordinary accounted peak matches and the annotated peak
falls by 48 bytes. The constant-default workload's peak rises by 40 bytes.
All borrowed allowances return to baseline.

Keep this source private. The ordinary warm regression has supporting CPU
and close duplicate medians. Annotated timing is inconclusive with divergent
candidate workers. Captured rules run no changed default construction and
have mixed CPU. Further work can use the existing result-consumer path to
avoid materializing and dispatching a separate append. The comparison does
not isolate the cause of the regression.

Both complete 32-file images passed six CPython goldens. Eight supplemental
scripts matched the prior Lython revision and CPython through both execution
APIs. Windows .NET 11 and Ubuntu .NET 10 passed focused Release checks for
default ordering, identity, refusal, generator suspension and cleanup. The
initial suspended-generator regression inspected the temporary reservation
bucket. The test-only follow-up verifies that the borrowed allowance remains
charged through suspension and is returned after cancellation or close.
The initial build name collision and WSL launcher quoting error are preserved.

Warm includes execution, mediated RAM-backed I/O, goldens and prior-result
cleanup. Compile-to-result adds compilation and compiled-owner disposal.
Process CPU covers the wider request. The 140 timed batches verified in
377.00 seconds. Both quiet gates passed, workers joined and the VM verified
the persistent archive before completing the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
