# Current native call costs — 2026-11-11

Two short profiles of retained `6904093e`, frozen `ccdd` core, after the captured
helper phase changed in R243. Existing fixed worker/protocol, F4as_v7 quiet VM,
normal GC/tiering and six-second collection windows. Perf maps and native listings
come from these actual processes; no old offsets transfer.

| Script | Actual guest CPU seconds | Guest leaf samples | Unknown |
| --- | ---: | ---: | ---: |
| Captured text/counter | 5.74 | 1,139 | 65 |
| Keyword-default file search | 4.42 | 872 | 48 |

Captured leaves include dictionary-loop work157, word preparation104, shared
instruction loop77, recursive native proof73, native iteration69 and native
evaluation54. Search includes shared instruction loop129, native argument entry62,
proof37 and evaluation34. Inlining and useful work overlap these labels; they are
not additive removable percentages. Search also spends CPU on host/reset/protocol/
validation threads outside the guest. All thread observations remain in the raw
record. No source-pair timing, allocation attribution or CPython ratio is claimed.

The next bounded source gate assesses reusing compiler-known call binding facts
in the existing native and owned destination binder. Immutable signature identity
can guard a prepared argument-to-slot mapping; defaults remain live runtime reads,
and mismatched/protocol/dynamic calls retain their general binder. The target is
repeated argument normalization and slot/presence discovery, without a call cache,
arena, new dispatch or ownership mechanism. This is a hypothesis, not a retained
implementation or predicted speedup. Preserve rebindings, receivers, errors,
default suspension, cancellation, first refusal and retained aliases. One source
gate, focused semantics and a bounded whole-script pair decide promotion.

Blind descriptor merging has no second backing array to remove after R241. The
actual short-word fixtures do not justify a new scratch vector; dictionary values
must retain immutable integer aliases. Complete pre-effect native type proof is
still necessary; omitting it is not a valid optimization.

[Assessment](assessment.json), [raw code/maps/profiles](observations.tgz),
[identity](identity.json), [source hashes](source-identity.json),
[archive hashes](archive-identity.json) and [receipt](archive-receipt.json)
preserve the observations and unknowns. B585 completes both collection windows
with quiet before/after gates, joined users, removed RAM fixtures and verified
persistent archives under the owned lease and after retrieval. It is terminal/
cached. Only duplicate historical B577/B582 archives were retired after fresh
absolute-path/UID/inode/all-alias checks against exact committed canonical Git
evidence. Every original result and current core role remains available.

Current external ratios belong in [BENCHMARKS](../../../docs/BENCHMARKS.md).
The performance objective continues; regex development is independent and
publication remains postponed.
