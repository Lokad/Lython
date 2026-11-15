# Current native function-entry attribution

R385/B773, 2026-11-15 UTC, **810379be / 8fbfa3f0**. One 5 s, 199 Hz cpu-clock/fp capture
after 8 guest-seconds warmup on the unchanged complete protected file-search script.
Same-process StartNativeCall disassembly and perf-map ranges agree exactly:
one optimized Tier1 range, 3,694 bytes. Runtime subsequently passes
[full qualification](../2026-11-15-function-slot-binding/delivery/README.md).

There are **749 guest leaves / 3.79 guest CPU seconds**, 137 unknown (including kernel
and runtime leaves), zero ambiguous. Entry has 53 leaves; ordinary dispatch 85,
native Run 55, input dispatch 52, call selection 32, iteration 32 remain visible. Wider
thread CPU/reset/validation is preserved separately. These overlap useful work
and are not removable shares or elapsed ratios.

Largest entry instruction groups include live keyword-default dictionary query
and bucket probing. Four prologue samples do not establish that slot initialization
is a dominant removable stage. Function presence/overflow/transport is already
removed. Prepared final slots, direct receivers, live defaults, deferred contexts
and actual suspension transfer remain; ordinary class/nested/lambda definitions
already create prepared executable functions. Replaying an old arena would target
allocations which have since disappeared and ignore its original 5–6.8% adversity.

Close this bounded gate with an explicit evidence gap. No new arena, mutable-default
position cache, exception-shell deletion or duplicate dispatcher is promoted from
these samples. A cache would add metadata and mutation/lifetime guards; these
observations alone do not establish a substantial whole-script benefit. Future
changed designs remain eligible, without a blanket retiming or permanent ban.
Next assess a concrete helper-rich config/manifest script's compile-to-result
phases, rather than infer frontend cost from passes or long-record warm runs.

All identity/golden/quiet/ownership/join/archive gates pass. RAM traces are archived
and hash-verified persistently under lease before unmount. Only five exact owned
aliases of superseded 6164 were retired, after incoming payload and committed archive
custody verification. Current 7161 reference, new 8fbf image, all archives/catalogs/
workers/toolchains and 254/252 MiB floors remain; final free space 267,145,216 B. No users or
RAM fixtures remain. Original read-only preparation syntax error occurred before
VM work and was corrected; no collection was restarted. No new timing, allocation,
CPython, heap/RSS or embedding-tail claim. Publication remains held.

[Mapped samples](attribution-file.json), [exact instruction groups](native-entry-instruction-groups.json),
[source/displacement decision](decision.json), [raw trace, machine gates, JIT and custody](observations.tgz),
[archive receipt](archive-receipt.json), [terminal state](terminal-status.json).
