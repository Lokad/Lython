# B1986 Borrow evaluated positional arguments for fixed builtins

Selected math builtins, `sum` and fixed-arity module-level regex operations
share one call path. Valid positional calls read the evaluated operand slots
directly. The common signature binder still handles keyword placement and
invalid calls. Regex operations capture the values they need before a lookup
can suspend, preserving the distinction between an omitted option and `None`.

Both APIs keep the same admitted peaks, compiled charges and allowance growth
counts on all four scripts. All allowances return to baseline. Managed
allocations match within 174 B per command, the single difference appearing
on the synchronous compiled-pattern control. The removed stage previously
used a managed stack buffer.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 1,056 public
and 232 white-box focused Release checks. A supplemental Windows .NET 11
run passes 133 checks for bound members and call diagnostics. The first
white-box builds detected an old reflection signature in the existing `sum`
checks. The updated checks retain their allocation, admission and cancellation
assertions. All four CPython 3.13.16 goldens match both images locally and on
the VM.

## Quiet warm synchronous comparison

November 28, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
The two images differ only in the core DLL, comparing `3396752f6` with
the archived candidate patch. Each collection has two workers per image,
five seconds or more of warmup and seven rotated timing batches per worker.
Elapsed includes mediated RAM-backed I/O where used, result verification
and previous-result disposal. Process CPU also includes fixture reset
and worker framing. Both clocks exclude compilation and process launch.
Ranges span pairings of worker medians. Negative changes indicate less time.

| Workload | First elapsed | First process CPU | Repeat elapsed | Repeat process CPU |
| --- | ---: | ---: | ---: | ---: |
| Module-level regex search, 3,072 lines | −3.63% to −1.68% | −3.61% to −1.84% | −2.45% to −0.82% | −0.85% to −0.51% |
| Fixed positional math, 4,096 iterations | −19.92% to −3.42% | −19.70% to −4.00% | −13.60% to −8.57% | −14.11% to −10.08% |
| Compiled-pattern regex search, 3,072 lines | +0.40% to +2.34% | +0.92% to +2.78% | −1.44% to +0.46% | −0.46% to +0.74% |
| Configuration edit, 181 B | −0.13% to +1.77% | −0.01% to +2.51% | −0.60% to +0.37% | −0.22% to +0.81% |

All 224 timed batches and both gates in each collection verify. The
repeat reverses workload and worker order with identical images. The math
reference CPU worker spread falls from 16.77% to 2.20% on repeat. Both
controls span zero on repeat, and the initial adverse compiled-search
result remains in the table. This comparison measures a Lython change.
The external ratios retain their earlier measurement revision. [B1985](../2026-11-28-current-regex-cpython/README.md).

[First summary](summary.json) · [Repeat summary](repeat-summary.json) ·
[Allocation](allocation-summary.json) · [Admission](admission-summary.json) ·
[Worker hashes](identity.json) · [Repeat worker hashes](repeat-identity.json) ·
[First observations](observations.tar.xz) · [Repeat observations](repeat-observations.tar.xz) ·
[Patch and checks](procedures.tar.xz) · [Repeat procedure](repeat-procedures.tar.xz).
