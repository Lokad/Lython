# Current runtime costs after native input/storage simplification

2026-11-12. Fully qualified runtime `2ec9d67f`, production unchanged in `16a6f23b`,
exact frozen `c9829194` core and `fec79afd` worker, normal optional adapter 0.4.
Two bounded synchronous captures on the quiet F4as_v7 use actual loaded maps/code.

| Complete script | Guest CPU seconds | Guest leaf samples | Unknown |
| --- | ---: | ---: | ---: |
| Captured text / Run | 5.85 | 1,162 | 63 |
| Keyword-default file search / Run | 4.53 | 895 | 38 |

Captured text associates 210 leaves with the histogram segment, 116 with split
preparation, 111 with shared instruction dispatch, 61 with word consumption,
60 with native proof, 50 with native evaluation and 49 with closure stores.
Search associates 157 with dispatch, 92 with native proof, 62 with real native
binding, 35 with input dispatch and 28 with native evaluation. Inlining/useful work
overlaps labels; these are not additive removable shares or predicted gains.

The next source gate assesses replacing recursive native proof with a linear pass
over its existing bounded postorder nodes. Compilation must validate consumption;
the proof keeps only transient enum shapes, never values cached across admission
or effects. This is a hypothesis, not a selected speedup. Preserve conditional/
closure/format semantics, dynamic protocols, missing arguments, refusal, genuine
async suspension and managed stack admission. No call-arena/known-call-map revival.
Histogram construction/preparation remains a later independent source target.

Two six-second collection windows contain 10.981 seconds of execution clocks;
guest CPU has a different boundary. Normal GC/tiering plus recorded perf/JIT
instrumentation; host/reset/protocol threads, all samples and unknowns survive.
No elapsed comparison, allocation attribution or source-pair gain is claimed.

All loaded-image/output/file/allowance/quiet/join/archive gates pass. All 37 archive
payloads are independently SHA-verified while persistent; owned RAM is unmounted,
workers joined and B599 terminal/cached. Only terminal B598 raw/catalog and B440
raw/catalog duplicates were retired after exact committed Git, path, UID, inode
and all-hardlink proofs; selected old/current cores remain protected.

[Assessment](assessment.json), [raw/maps/code](observations.tgz), [source proof](source-identity.json),
[receipt](archive-receipt.json), [preparation](preparation.tar.xz) and
[archive identities](archive-identity.json). Current CPython timings remain in
[BENCHMARKS](../../../docs/BENCHMARKS.md); full delivery is already green and is
not repeated for this unchanged-source capture. Publication remains postponed.
