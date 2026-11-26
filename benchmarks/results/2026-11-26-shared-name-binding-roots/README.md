# Sharing function discovery with syntax validation

On 2026-11-26, a private patch against `4f7c085cd` collected the outermost
functions during syntax validation. Static name-binding diagnostics read the
worklist in source order and still analyzed function bodies and class scopes.
The patch removed a separate statement walk covering 14 statements on the
small manifest, 66 on generated record rules and five on sparse guards. [Source
inventory](discovery-inventory.json)

Both frozen images matched the same independent CPython golden results on
four inputs. The candidate passed 104 white-box and 1,217 public Release checks
on Windows .NET 11. A focused test confirmed diagnostic order and normalized
source spans through nested classes, defaults and sibling branches on both
images. Retained compiler admission matched in all four allocation diagnostics.
The worklist added 1,104 CLR bytes on generated rules and eight on sparse
guards. The manifest used 200 fewer bytes. [Allocation diagnostics](allocation-summary.json) ·
[Patch, verification and test logs](procedures.tar.xz)

The local Ubuntu WSL .NET 11 comparison ran two workers per source, each with
four seconds of warmup and seven rotated batches targeting 350 ms per batch.
The table spans candidate/reference changes across the worker medians for each
lane. Negative values mean lower time. Compile-to-result uses the existing
manifest harness. The empty compile control fluctuated despite identical
source within each pair. [Worker medians and control spreads](summary.json) ·
[Raw observations](observations.tar.xz) · [Image hashes](identity.json)

| Workload | Boundary | Elapsed change | CPU change |
| --- | --- | ---: | ---: |
| Empty control | Compile | −4.38% to +20.43% | −9.09% to +22.22% |
| Small manifest | Compile | −10.81% to −1.44% | −9.72% to +6.07% |
| Generated record rules | Compile | −33.62% to +6.68% | +1.35% to +7.89% |
| Sparse lexical guards | Compile | +2.49% to +4.32% | +8.99% to +17.19% |
| Small manifest | Compile-to-result | −42.51% to +64.74% | −37.51% to +29.17% |

Production keeps the existing walk. The removed scan was small on sparse
guards, CPU observations were adverse on both generated inputs, and the
manifest CPU ranges crossed zero. The timings are diagnostic given the control
variation. Revisit if another consumer can use the worklist or profiling
identifies a larger repeated scope traversal.
