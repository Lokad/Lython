# B1099 positional defaults from evaluated slots

Private R669 (`88aaeecf3`) builds the final positional-default tuple from the
evaluated operands under the definition's existing reservation. The shared tuple
constructor removes a redundant size scan. Fresh machine integers can remain
unboxed. Omitted-argument binding borrows their identity from the live tuple.
Lowered definitions still collect object values during evaluation.

The exact reference image is `3156df3b9`, source-equivalent to the delivered
`f8a7920e7`. Both 32-file images matched seven CPython goldens. Each target
passed 438 focused Release checks, including terminal refusal before wider tuple
allocation and positional identity across actual asynchronous keyword lookup
that replaces the defaults. Local allocation falls 3.62% on ordinary rules and
2.89% on annotated rules. Accounted peaks rise from 2,754,951 to 2,918,791 B and
from 2,363,481 to 2,445,401 B respectively. The slot-backed tuples admit their
wider backing and future boxes. All allowance baselines recover.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Ranges cover all four combinations of duplicate process medians.
Warm includes mediated I/O, goldens and earlier-result cleanup. Compile-to-result
adds compilation and compiled-owner disposal. Wider process CPU includes other
worker work. The computed-default fixture uses `base + 1` in a factory and
then calls the returned function 2,048 times. The async lane runs the same
script with `RunAsync`. Actual suspended lookup is covered by the regression.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Warm | −4.85…−3.21% | −6.31…−4.86% |
| Annotated rules | Warm | −0.80…+0.33% | −1.69…+2.00% |
| Captured rules | Warm | −4.86…−1.34% | −5.93…−1.74% |
| Computed defaults | Warm | −13.99…+6.56% | −14.30…+9.31% |
| Computed defaults | `RunAsync` warm | +1.02…+11.21% | +1.76…+9.49% |
| Constant defaults | Warm | −1.79…+3.08% | −1.53…+3.63% |
| Expanded forwarding | Compile-to-result | −0.35…+1.70% | −1.28…−0.68% |
| Annotated rules | Compile-to-result | −2.80…−1.45% | −1.87…+0.46% |

The synchronous computed-default reference medians diverge by 20.88%.
Computed-default async elapsed and CPU are consistently adverse in this run,
with up to 8.01% batch IQR. Captured rules are a control for the default path
and also exercise the shared tuple builder. The comparisons cannot apportion
their change between the builder, default borrowing and process variance.
The [longer-warmup confirmation](confirmation/README.md) repeats the
ordinary-rule improvement. The computed and reused-default controls remain
mixed. The original candidate stays private while its lookup refinement is tested.

The collection completed 160 verified batches in 364.16 seconds. Both quiet
gates, worker joins, fixture removal and archive verification passed. The
first local build needed a nested-method accessibility correction. The image
check also needed to distinguish the equivalent reference source from
historical benchmark files. The check archive preserves both original logs.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) · [Lease receipt](archive-receipt.json).
