# B1223 Grouped abstract expression dispatch

Compares `d948e0682` and private `4060f7972`. The 2026-11-20 UTC local
screening run used Ubuntu under WSL on an Intel Core i7-14700KF and
.NET 11 RC3. It uses two separately warmed processes per source,
six-second warmups, five rotated rounds and 0.5-second batch targets.
Ranges cover all four pairings of process medians. Negative values
favor the candidate.

| Workload | Boundary | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compilation | −21.77% to +43.18% | −21.84% to +42.07% | 66.23% / 10.11% |
| Generated validator | Compile to result | −4.80% to +8.85% | −2.56% to +6.95% | 9.74% / 4.19% |
| Configuration manifest | Compilation | −10.20% to +13.83% | −7.37% to +10.04% | 12.57% / 12.60% |
| Configuration manifest | Compile to result | −3.66% to +7.49% | −3.30% to +8.35% | 2.43% / 8.93% |
| Configuration manifest | Warm control | −6.04% to +4.78% | −7.18% to +3.60% | 4.91% / 6.29% |

Normalize parentheses before the binding-state lookup and collapse
abstract resolution into one dispatch. The prototype removes 19 net
production lines and preserves binding-state invalidation and resolved
expression spans. Compilation allocation matches on the validator,
manifest, inherited dataclasses and eager rules. Windows .NET 11 and Ubuntu .NET 10
each passed 1,397 public and 516 white-box focused checks. Both exact
32-file images verified the four complete workload goldens alongside
CPython. All 100 local batches returned complete results. The workers
joined and fixture scratch was removed. All observations are persistent.

Warm includes RAM-backed mediated I/O, golden checks and previous-result
cleanup. Compilation includes code-owner disposal. Compile to result
includes both phases. The wider process CPU covers framing and reset.
The control and every affected lane span zero on both clocks, with large
process variation. Keep the prototype private. A quiet comparison can
reevaluate it if a larger frontend change uses the same normalization.

[Process medians and ranges](summary.json) ·
[Raw observations and procedures](observations-and-procedures.tar.xz).
