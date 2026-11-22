# B1315 Prepare lexical work during scope binding

Syntax validation already visits statement expressions. It now records
which statements require lexical expression preparation in the same map
that carries potential mutation calls. The binding walk uses that fact
to collect bindings where needed and queue the remaining declarations,
annotations and expression work. Ordinary non-leaf suites lose their
second statement walk. A single comprehension, lambda or implicit
`super` no longer triggers expression walks in unrelated statements.

The first fused candidate `ccb277177` queued all statements in a
complex module. Its 4,096-statement `super()` diagnostic allocates
274 KB more than the original compiler. The sparse refinement removes
that backlog. It allocates 20.0% less on both 1,024- and 4,096-statement
complex diagnostics. Their ordinary counterparts are effectively flat.
These wide scripts isolate the mechanism. They are separate from the
representative comparison.

## Quiet comparison

Compared `745f8833f` with `d5abc29d4` on November 22, 2026 UTC on
the quiet Azure F4as_v7, running Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per revision warmed independently and contributed seven
rotated batches each. Ranges cover all process-median pairings.
Compilation includes code-owner disposal. Time to result adds execution,
mediated RAM-backed file I/O and result disposal. The process CPU clock
covers the wider worker request. Negative changes favor the candidate.

| Whole script | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Ordinary generated validator | Compile | −2.21 to −1.27% | −2.14 to −1.73% | 0.16 / 0.79% |
| Postponed factories | Compile | −0.15 to +1.15% | −0.88 to +1.63% | 0.75 / 0.55% |
| Inherited dataclass configuration | Compile | −9.86 to −7.81% | −10.62 to −7.84% | 1.70 / 0.57% |
| Validator with an error-counting generator | Compile | −5.95 to −5.55% | −6.47 to −5.84% | 0.37 / 0.04% |
| Validator with an error-counting generator | Time to result | −6.30 to −4.61% | −5.49 to −4.06% | 1.35 / 0.44% |

Retain the syntax facts and fused walk. Local compilation allocation
falls 8.75% for dataclasses, 1.56% for the validator with the generator,
and 0.84% for the ordinary validator. Factory timing spans zero.
These are within-Lython changes. The VM workers matched CPython
outputs before timing, and CPython elapsed was outside this collection.

Windows .NET 11 and Ubuntu .NET 10 each pass 111 white-box and 984
public focused Release checks. Executable metadata matches across
95 code objects, including protected definitions, nested closures,
postponed annotations and the large suites. Six cases have identical
accounted peaks. Admission at an actual working-storage resize
gives the same refusal span in five cases. Cancellation and refusal
refund the compiler scope, and later compiles reuse the allowance.
The ordinary validator has no positive resize and contributes only
its peak. Early probe and staging attempts used incorrect local
assumptions about lease growth and the staging template. Cross-target
testing uses sequential restores after shared build state caused an
initial Windows setup failure. Original attempts are in the procedures.

The exact 32-file images pass all four independent CPython goldens.
All 140 timed batches validate. Both machine checks pass, workers
join, and the persistent archive verifies under the lease after
fixture cleanup. The collection took 272 seconds.

[Timing](summary.json) · [Representative allocation](representative-compile-allocation.json) ·
[Wide-scope diagnostics](wide-suite-allocation.json) · [Observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-22-csv-quoted-ranges/candidate-core.dll.gz).
