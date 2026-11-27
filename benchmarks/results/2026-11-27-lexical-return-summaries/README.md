# B1901 Compute return-summary eligibility with lexical binding facts

The trial removes the independent function-body eligibility walk. The
lexical binding pass computes the same predicate while visiting each
suite. A `with` passes through its body's return fact. An `if` with an
`else` requires both branches to provide a return, while a missing
`else` leaves the existing single-branch rule. An unsupported statement
still prevents a summary. The analyzer's header checks and captured
bindings stay on their original path.

The frozen reference is `9d048800a`. Both images have 32 verified files
and differ only in the core DLL. Five independent CPython 3.13.16 output
and file goldens verify. The compiler witnesses compare 18 script tapes,
134 admission levels, cancellation and 16 diagnostic specimens. The
diagnostic cases include supported and unsupported branches, nested
`with`, decorated and variadic functions, and exact diagnostic ordering.
Each Windows and Ubuntu WSL Release target passes 815 focused tests.

Windows compilation allocates 2,000 B less per ordinary or escaping
definition workload and 7,680 B less per guarded report. The generated
configuration, which has no functions, allocates the same amount.
Peak compiled ownership and cleanup match across the compiler witness.

## Timing on November 27, 2026 UTC

The first pass uses Ubuntu WSL on CPU 27. The repeat reverses initial
worker placement on the Azure F4as_v7 running Ubuntu 24.04.4. Both use
.NET 11 RC3 and the same frozen images. Each image has two workers with
five seconds or more of warmup and seven rotated batches per worker.
Ranges span all pairings of worker medians. Compile includes prior-code
disposal. Warm and compile-to-result also include applicable mediated
file operations and result cleanup. Process CPU includes fixture reset
and framing. Startup sits outside the clocks.

| Workload | Lane | WSL elapsed | WSL CPU | Quiet elapsed | Quiet CPU |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary definitions | Compile | −1.47% to +0.40% | +0.12% to +2.13% | +0.10% to +0.76% | −0.44% to +2.13% |
| Function-free configuration | Compile | −5.39% to +1.61% | −2.23% to +0.85% | −1.39% to +0.85% | +0.67% to +3.12% |
| Nested JSON control | Warm | +0.73% to +8.27% | +2.56% to +7.29% | — | — |
| Escaping definitions | Compile | −3.93% to +0.80% | −3.11% to +1.38% | — | — |
| Guarded report | Compile to result | −1.00% to +1.03% | −0.68% to +2.19% | −3.85% to −1.10% | −4.10% to +0.35% |

Withdraw the trial. The combined walk performs additional eligibility
work in scopes with no function summary. The quiet configuration CPU
increase and ordinary timings leave the modest allocation savings
insufficient for retention. Guarded elapsed improves on the quiet VM
with CPU spanning zero. Preserve the adverse WSL nested JSON control
at its original conditions.

All 224 batches verify. The quiet run passes both machine gates, joins
all workers, removes fixtures and verifies its persistent archive under
the lease. Staging reuses 63 files across the two images.

[Compiler parity](compiler-parity.json) · [WSL timing](summary.json) ·
[Quiet timing](quiet-summary.json) · [Image identity](identity.json) ·
[WSL observations](observations.tar.xz) · [Quiet observations](quiet-observations.tar.xz) ·
[Patch, checks and procedures](procedures.tar.xz).
