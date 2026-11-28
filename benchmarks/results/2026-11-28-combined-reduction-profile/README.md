# B1961 Attribute the combined generator reduction changes

The exact `b663af554` image includes fixed-width `sum` binding and
the operand-owned subtotal. Two Ubuntu WSL EventPipe captures use the
same shipment catalog as B1958, .NET 11 RC3 and one logical CPU. Each
capture follows four seconds of warmup. Five independent goldens
verify before profiling. All 102 sampled batches verify and both
workers and trace processes join.

| Managed region | Tuple generator | Explicit loop |
| --- | ---: | ---: |
| Private generator lifecycle, inclusive | 36.3% | 0.0% |
| Borrowed generator instruction prefix, inclusive | 23.3% | 0.0% |
| Borrowed operand preparation, inclusive | 9.9% | 0.0% |
| Outer acquisition, inclusive | 2.3% | 0.0% |
| Executable frame construction, inclusive | 3.1% | 0.0% |
| Native instruction loop, nearest | Outside the top 35 | 38.2% |

Both complete scripts use `RunAsync` on the same 2,048 CSV rows.
Percentages describe resolved managed samples after filtering known
waits and unclassified native leaves. The lifecycle includes preparation
and cleanup. Inclusive regions overlap. The two scripts have different
denominators, and the profiles provide attribution for this revision.
[Earlier B1958 attribution and timing](../2026-11-28-post-generator-cpython/README.md).

The ordinary helper and the generator each use seven operand slots.
The execution's one-idle-array workspace can supply one of those arrays
while both are live on a pull. Sending the child through that cache
would still require one fresh array per overlapping pair. A simpler
ownership change could fund the exact outer iterator alongside the
operand. The current source reserves it separately and keeps the charge
to execution end. Promotion must preserve the same already-acquired
iterator and its charge if Python can observe `.0`.

[Image](identity.json) · [Attribution](attribution.json) · [Layouts](layout.json) ·
[Run summary](profile-summary.json) · [Raw captures and procedures](profiles.tar.xz).
