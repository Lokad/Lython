# B1944 Match both Lython revisions on the quiet VM

The existing frozen `e036d6597` and `bf10eeaea` images run the same
file-fed map and ordinary tuple reduction on the B1943 VM. All 32 files
in each image match their recorded hashes. The binaries differ in core
and optional adapter. Both workloads use the CSV and numeric operations,
and the adapter stays outside their execution. Both images verify all
four CPython 3.13.16 goldens before timing.

Two workers per image warm for five seconds and supply seven rotated
batches for each `RunAsync` lane. Ranges pair all worker medians and
report the current build relative to the reference. The measurement
boundary matches the warm B1943 comparison. All 56 batches verify,
both quiet gates pass and the archive verifies after fixture removal.

| Workload | Elapsed change | Process CPU change | Within-image CPU spread, reference / current |
| --- | ---: | ---: | ---: |
| File-fed CSV quantity map | +1.75% to +3.89% | +0.73% to +4.08% | 1.36% / 1.94% |
| Ordinary tuple reduction | −8.12% to −2.27% | −8.96% to −3.06% | 2.15% / 4.23% |

The matched result supports both the ordinary reduction gain and a small
file-fed regression. Attribution to individual changes remains open.
The two builds emit identical opcode sequences and operand ownership
bounds for the file-fed map and its `quantity` helper. A layout witness
also finds that B1940's conservative generator-expression rule raises the
last potentially owning slot in the iterable shipment module from 8 to 11.
The outer generator expression feeds the two-argument `starmap` call.
The new bound puts those extra slots in the range for cleanup checks.
Tightening the bound to the handoff's eligible call shape could remove
checks. Its timing effect remains a hypothesis.

[External comparison](../README.md) · [Timing](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Compiled operand layout](operand-layout.json) · [Layout procedures](layout-procedures.tar.xz).
