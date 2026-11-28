# B1912 Admit one computed positional default with its callable

The existing default operand now carries the fixed callable charge through
evaluation and suspension. Publishing the function drops the construction tag
and transfers the charge. Abandoning the operand refunds both parts. This
eliminates a second admission for eligible definitions. The combined request
can be refused earlier at the definition span, before evaluating the default.

The generated definition scripts from the existing catalog were adapted to use
one computed positional default per function. Each builds 96 functions 16
times. The ordinary factory returns three functions and the escaping factory
returns all 96. The keyword-default escaping factory is an unchanged control.
All three outputs match independent CPython 3.13.16 goldens on both images.

| Factory | Growth requests before → after | Peak run admission |
| --- | ---: | ---: |
| Ordinary positional | 3,095 → 1,559 | 484,379 B |
| Escaping positional | 3,111 → 1,575 | 511,779 B |
| Escaping keyword control | 1,573 → 1,573 | 999,082 B |

The counts match in both hosting modes. Windows allocation checks find
identical managed allocation and peak compile and run admission within each
mode, with every allowance returned to baseline after cleanup. On the
reference, both new refusal tests reach the host clock callback during the
default and then fail admission. The candidate refuses before that callback.
Focused Release checks pass 396 public and 55 white-box cases per target on
Windows .NET 10 and 11 and Ubuntu WSL .NET 11.

## Local timing

Local Ubuntu 24.04 WSL with .NET 11 RC3, on November 28, 2026. Both complete
images differ only in the Lython core DLL. The baseline is `2fde05280` and the
candidate is the recorded source patch. Each image uses two workers, five
seconds of request warmup per worker and seven rotated batches. The ranges
span the worker-median pairings. Elapsed includes validation and
previous-result disposal. Process CPU also includes fixture reset and request
framing. Compile to result includes compilation and compiled-owner cleanup.
Startup is excluded.

| Factory | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary positional | Warm sync | −6.69% to −3.56% | −7.91% to −7.40% |
| Ordinary positional | Warm async | −9.04% to −7.86% | −9.80% to −7.57% |
| Escaping positional | Warm sync | −11.65% to −8.70% | −12.16% to −7.17% |
| Escaping positional | Warm async | −14.57% to −5.95% | −14.27% to −6.54% |
| Escaping keyword control | Warm async | −1.27% to +3.49% | −0.62% to +4.45% |
| Escaping positional | Compile to result | −7.03% to +6.12% | −2.41% to +6.30% |

All 168 timed batches verify. The escaping warm async reference workers
differ by 7.58% in CPU. The warm results support the request-count finding
for these generated scripts. Compile-to-result timing remains inconclusive.
Keep the change for the eliminated admission and refusal before default
effects. These local comparisons leave the quiet CPython checkpoint as the
external timing baseline at its recorded revisions.

[Admission counts](admission-summary.json) · [Allocation](allocation-summary.json) ·
[Timing](summary.json) · [Images](identity.json) ·
[Observations](observations.tar.xz) · [Source and checks](procedures.tar.xz).
