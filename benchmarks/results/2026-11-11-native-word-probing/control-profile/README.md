# Paired async-search CPU profile

B596 profiles the exact frozen reference and R250 candidate used by B595, with
one process per image and at most two six-second capture windows. It is a bounded
follow-up to the normal-run async-search regression, not another timing comparison.

| Image | Guest CPU seconds | Guest leaf samples | Unknown |
| --- | ---: | ---: | ---: |
| Qualified reference | 4.41 | 870 | 40 |
| R250 candidate | 4.36 | 861 | 45 |

Actual command clocks total 10.126 seconds; the capture/CPU boundary is wider and
includes work outside command clocks. All thread CPU and samples survive. The
profiles enable perf maps/JIT diagnostics and use fresh processes; their per-job
diagnostic times do not settle B595's normal-profile regression.

Both associate material work with instruction dispatch, native argument binding,
guard checks and native evaluation. Of 840 common managed optimized method names,
801 have equal code extents. Some native binding/guard/prefix extents differ even
though their structural IL matches. Equal sizes do not prove equal native code;
different sizes do not establish the cause or direction of a timing change.
The captured profiles do not identify extra work attributable to the word kernel.
The stopping criterion is met: no further unchanged captures or retiming.

Keep R250 unpromoted while completing a coherent captured-input simplification.
The existing native helper path packs already-bound parameters and live cell
references into another fixed buffer, clears it, and treats appended cells as a
second domain within the parameter span. Borrowing the existing parameter prefix
and cell collection separately can delete that stage. The added evaluator input
transport must be assessed, not assumed faster. At that milestone, explicitly
retain/withdraw or accept remaining material async-search/CSV debt before full
platform/stress/installed-consumer qualification. Original adverse B595 results
and the original prototype decision remain unchanged.

Both images pass identity/output/file/allowance checks before and after capture;
quiet gates, joined users and under-lease archival verification pass. All 37
profile/evidence files are hashed and verified in persistent storage before
discarding the owned tmpfs. Independent retrieval verifies the archive; B596 is
terminal/cached with no remaining users or RAM fixtures. Initial eligibility
referenced an already-retired duplicate and refused before mutation. Fresh read-only
inventory selected only extant terminal catalog/archive duplicates with exact
committed Git byte and path/UID/inode/all-alias proofs. Both current image roles
remain protected. One local early observation guard rejected before any VM query.

[Summary](summary.json), [raw profiles/maps/code](observations.tgz),
[terminal receipt](terminal-status.json), [source identity](source-identity.json)
and the original failed inventory preserve the limits. This does not qualify the
prototype for delivery or update the external CPython baseline.
