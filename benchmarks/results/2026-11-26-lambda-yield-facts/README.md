# B1777 Collect lambda yield facts with lexical bindings

The trial against `7045bcb35` uses the binding walk to classify lambdas, including
their defaults and nested lexical owners. Ordinary executable and deferred
namespace emission reuse that classification. The patch removes the separate
body walk for 48 lambdas in the ordinary factory and 64 in generated record rules.
The common binding walker now computes a yield flag. The syntax field adds
384 B and 512 B of CLR allocation on those cases, respectively. Retained
compiler admission matches in all four cases. [Lambda inventory](lambda-inventory.json) ·
[Compiler allocation](allocation-summary.json)

The candidate passes 96 white-box and 522 public focused Windows .NET 11
Release checks, including generator lambdas in deferred type aliases. Both
32-file images match four independent CPython output and file goldens. The
initial build showed that annotated-statement lowering also uses the original
yield query. The measured patch preserves its visibility and the annotated
statement path. [Source, verification and test logs](procedures.tar.xz)

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Small manifest, no lambdas | Compilation | −9.86% to +14.88% | −6.48% to +15.00% |
| Ordinary factory | Compilation | −2.17% to +1.44% | −3.29% to +0.21% |
| Generated record rules | Compilation | −9.64% to +1.77% | −2.13% to +0.03% |
| Ordinary factory | Compile-to-result | −13.31% to +9.98% | −23.33% to +9.06% |
| Sparse guards, no lambdas | Compilation | −3.52% to +0.40% | −3.98% to −2.51% |

The local Ubuntu WSL .NET 11 RC3 diagnostic uses two workers per source,
four seconds of warmup and seven rotated batches targeting 350 ms. Changes
span all four pairings of worker medians. Negative changes mean lower time.
Compilation includes compiled-owner disposal. The combined clock includes
execution, output verification and cleanup. CPU also covers request framing.
All 140 batches complete and the archive verifies. [Medians and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Frozen images](identity.json) ·
[Terminal receipt](archive-receipt.json)

Keep the current frontend. Both target compile CPU ranges cross zero, and the
combined factory reference workers differ by 39.40% in CPU. The lambda-free
sparse case uses less CPU despite having no lambda walk to remove. A later
trial needs an existing fact storage location that keeps the allocation flat
and evidence that the repeated body walk is material.
