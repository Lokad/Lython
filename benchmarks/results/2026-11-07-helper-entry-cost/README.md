# Complete protected-search call-entry cost gate — R48/B403

2026-11-07, qualified8c9fd0d7/frozen d3a6c778, precommit SourceLink3480aa55;
unchanged worker7cf37ca5/host/adapter27211dbe/normal0.4. Quiet F4as_v7 Ubuntu24.04.4,
.NET11RC3 normal GC/tiering with declared PerfMap3 and five JIT listings. One
maintained complete file-search.large,6s guest warmup/2s settle/6s wall199Hz
cpu-clock/frame-pointer capture. Job clocks3.66655s, actual guest CPU3.28s,
645guest samples/34unknown leaves; all threads/raw stacks/maps/inlining limits stay.
This is diagnostic CPU evidence, not a source-pair elapsed or speedup measurement.

| Work | Self | Inclusive | Interpretation |
| --- | ---: | ---: | --- |
| ExecuteInstructionPrefix |71|590|Dispatch, required checks and useful inlined work. |
| TryExecuteNativeCall |32|169|Includes actual native helper work. |
| GuardNativeReturn |29|Not a saved-CPU claim|Eligibility proof before effects. |
| EvaluateNativeReturn |38|66|Actual string/value execution plus routing. |
| ResolveName |29|Not a saved-CPU claim|Namespace resolution, not all binding overhead. |
| EnterInterpreterFrame |10|Overlaps callers|Managed nesting, cancellation and admission. |
| EnsureInterpreterStack |9|Overlaps callers|Supported managed stack policy. |
| EnterFunctionCall |6|Overlaps callers|Recursion, token and stack admission. |

The three named entry leaves total25/645samples (~3.88%); not all are removable,
and inlining/unknowns prevent treating this as an upper bound. Inclusive stacks
overlap and cannot be added. Repeated stack admission exists, but this capture
does not establish a dominant removable admission phase. Do not introduce a
native-only policy, split admission invariant, wider opcode hoisting, or a new
frame/dispatch backend to delete those checks. The single immediately duplicate
caller check is recorded, not promoted as a substantial redesign.

Source/census show a different actual duplication: module compiled locals contain
nine bindings but omit wanted; the loop reads its target through LoadName.
Function declarations publish with StoreName, which already updates any existing
executable local/closure slot. Ordinary assignment declarations use those slots.
Imports/classes similarly publish through prepared binding paths. Assess actual
declaration bindings using existing metadata; keep dynamic global/namespace bridges
and before-binding builtin fallback. Do not eagerly intern every dead module name
solely to force a slot, or add another scope walk/cache/framework. The source fact
justifies a bounded prototype; its CPU/allocation benefit remains unmeasured.

All core/aux/Ready/golden/funding/quiet/Closed/exit/join/fixture gates pass in B403.
Raw RAM evidence archived/verified under the collection lease, then removed and
independently verified locally.120s owner+30s stop+30s margin, observed after180s.
No active workers, fixtures or traces. Preserve the exact raw archive; Windows
extraction skips only zero-byte Linux FIFO metadata, recorded in extraction.json.

B402 first refuses setup because an inherited relative old-root guard remains;
no payload or worker starts. Corrected new-root launch then fails before sampling:
the inherited warmup permits64requests*64jobs=4096jobs, insufficient for6guest
seconds on this short script. Cleanup records owned worker exit−9, not Closed;
no sampler starts. Failed raw state is preserved under a fresh maintenance lease
after terminal failure, before removing RAM traces; original gates remain incomplete.
The initial caption mistakenly says4096single-job requests and proposes batch8.
Exact source and warmup-correction.json correct it. B403 changes only warmup to
64requests*1024jobs maximum, still stopping at6guestseconds; its sampled6s/single-job
requests, source, images and diagnostic overrides are unchanged. This is one
bounded collection-size correction, not a restart after an observation timeout
or repeated favorable retiming. Both original failed conditions remain archived.

[Decision](decision.json), [all guest chains](guest-call-chains.json),
[compile-only census](census.json), [source invariants](source-invariants.json),
[successful raw capture](b403/observations.tgz), [receipt](b403/archive-receipt.json),
[original failed capture](b402/failed-observations.tgz),
[caption/size correction](b402/warmup-correction.json),
[all commands and contracts](development.tar.xz). No runtime change, new producer,
repeated full qualification or publication. The whole-script objective stays open.
