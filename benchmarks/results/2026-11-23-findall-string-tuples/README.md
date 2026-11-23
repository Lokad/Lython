# Construct multi-group findall tuples from their captured strings

Retained `e05aae21c` replaces the per-match delegate and temporary
reservation object with one direct reservation and a loop filling
the final string array. Every item already owns its text. The tuple
needs no scalar charge scan. Lython preflights the array, checks
cancellation during projection and keeps unmatched groups as empty
strings. This change is in the core above the provider boundary.

| Script | Matches | Reference CLR allocation | Candidate CLR allocation | Reduction |
| --- | ---: | ---: | ---: | ---: |
| Two ASCII groups | 4,096 | 2,160,048 B | 1,799,544 B | 16.69% |
| Optional group with Unicode text | 4,096 | 1,974,208 B | 1,613,704 B | 18.26% |
| Eight groups | 1,024 | 1,398,928 B | 1,308,760 B | 6.45% |
| Single-group control | 4,096 | 828,568 B | 828,512 B | 0.01% |

All admitted execution peaks match and all budgets return to baseline.
These Windows x64 .NET 11 RC3 diagnostics on November 23, 2026 use
the same 0.4.0 optional adapter in both verified 32-file images.
Measurements compare `e4392967b` and the source-identical private
`4dc812eaf` build. Both images match all four CPython goldens. The
comparison establishes allocation savings and leaves CPU and elapsed
unmeasured.

Windows .NET 11 and Ubuntu .NET 10 each pass 677 public and 932 white-box
checks. The suites include provider cancellation and cleanup, shared
budget refusal, and public lists and captured tuples through both APIs.

[Allocation](allocation-summary.json) · [Image identity](identity.json) ·
[Goldens and checks](checks.tar.xz) ·
[Reference core](../2026-11-23-module-failure-bindings/README.md).
