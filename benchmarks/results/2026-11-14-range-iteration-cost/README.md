# Remaining range-iteration stage: source selection

R362/B743, 2026-11-14 UTC, committed `3bc513b8` / frozen `ca1e2331`.
Full 43618009133 is still in progress at capture time. Same quiet F4as_v7 /
Ubuntu 24.04.4 / four EPYC 9V45 / .NET 11 RC3, fixed worker and optional 0.4
adapter. Original complete inside-range 100,000-add script, exact stdout/budget.
One 5-second 199 Hz cpu-clock/fp capture after eight guest seconds warmup;
same-process cursor-pull JIT map/disassembly. PerfMap/JIT overrides are diagnostic,
not normal timing. No source-pair, allocation-stack or CPython comparison.

995 guest leaves /5.0 guest CPU seconds, 38 unknown, zero ambiguous.

| Leaf owner, including inlined useful work | Samples |
| --- | ---: |
| Generated PyRange.Iterate.MoveNext | 326 |
| Native RunCore | 221 |
| Native TryIntegerOperation | 179 |
| Cursor.TryMoveNextCore | 66 |
| TryMachineBinary | 52 |
| AugmentedBinaryOperator | 38 |

These are attribution counts, not removable shares. Range iteration is now a
demonstrated substantial component after the shared arithmetic/native-storage
deliveries. R361's approximately 3.2 MB/100,000 pulls independently supports the
boxing-volume hypothesis; it does not assign CPU time to allocation.

Select one iteration-stage prototype: replace range's generated enumerator with
shared explicit state, expose a primitive pull through the existing cursor and
publish through native integer storage only where already prepared. Generic
object iteration shares that state. Preserve range's CLR/Python interfaces,
independent enumerators, Current/EOF, exact large bounds, stable aliases and large
yield funding. Avoid adding CLR collection interfaces that might change generic
serialization/projection. A small iterable adapter may add legacy acquisition
overhead; materialization/short controls must expose it. No ordinary-frame scalar
state, new compiler pass, universal values, controller, pool or ownership layer.

This is a source selection, **not an implemented or measured redesign**. Concrete
deletions, ownership limits, focused acceptance and the bounded counter/fresh/
CSV/materialization/short/observed comparison are in the source gate. No speedup
forecast. Whole-script and independent regex work remain active; publication held.

All image/golden/budget/quiet/join/archive checks pass. The trace was SHA-verified
persistently under the lease before RAM unmount; no users/fixtures remain.
Only exact committed raw-result duplicate files were retired for staging; current
images, catalogs, auxiliaries, toolchains and 254/252 MiB floors remain unchanged.
Raw SHA256 `1d60785ff7088274d48e9637422d420df3faf4e9b21001fc2c57f608ea3bc3a6`.

[Source/ownership design](source-gate.json), [attribution](attribution-range.json),
[all leaves](leaf-census.json), [raw trace/maps/conditions](observations.tgz),
[collector/analysis](collection-and-analysis.tar.xz).
