# Current complete-script CPU source gate — B126

2026-11-02, exact90785737/core137b563a, read-only assessment after formatting,
plain JSON and native context removal. No runtime prototype or timing speedup.
Existing Linux perf cpu-clock199Hz/frame-pointer protocol, ordinary GC/tiering,
only perf-map diagnostic override. Quiet F4as_v7, before/after full goldens and
actual named guest-thread attribution. Both workers/collectors exit0 and join;
service26.163s/inactive, shared lease free. Initial archive retrieval lacked
permission for root-owned perf.data; a complete privileged read preserved it.

| Capture | Batch wall time | Guest CPU samples | Unknown leaves |
| --- | ---: | ---: | ---: |
| Search,4,096 complete jobs |8.121s|304|5.59%|
| Text,1,024 complete jobs |5.734s|791|6.83%|

This is on-CPU guest-thread sampling, not a complete elapsed attribution. Search
has much less process CPU than elapsed in separate current comparisons; these
captures do not establish the cause of waiting or resolve B125's unstable search
controls. Frame-pointer/inlining/native transitions leave incomplete attribution;
no exact removable fraction or universal scalar/frame cause is inferred.

ExecuteUntilCall is about14.8%/16.1% of search/text leaf samples. Search's checked
enumerator is6.25% leaf/14.8% inclusive; text's generic iterator adapter is4.3%
leaf, with substantial interface checks. Native module returns remain13.5%/30.7%
inclusive; these include useful operations, not just calling overhead. Monitor
leaves are~4.3%/7.5%; source ownership checks cannot simply be removed. The old
context-constructor allocation is absent from the selected successful calls.

Source gate selects one coherent next design: consolidate executable synchronous
iteration onto the existing Cursor already used asynchronously, rather than
ToSequence/CheckedSequence/CheckedEnumerator/EnumerateIterator layering. Reuse
that cursor for checked enumerable consumers where lifetime/error semantics allow;
retire duplicated checked-enumerator/range machinery and invalid iterator peek
helpers only when their real callers migrate. Avoid constructing then discarding
native enumerators merely to validate a source. Keep exact source mutation and
acquisition semantics, native snapshots, cancellation and awaited cleanup.

This is a hypothesis with source-level deletion targets, not an automatically
approved optimization. Range, dictionaries, mutable lists, custom protocols,
retained iterators/generators, real suspension, cleanup failures, terminal refusal
and budget reuse must pass. Compare complete text/search with CSV/range controls
and unchanged workers; preserve adverse clocks. The instruction already has two
compact operand slots, so another hypothetical wide-instruction compression is
not justified. Arenas/mutable dispatchers that measured slower remain lessons.

Raw perf data/maps/chains, loaded identities, scripts and offline derivations are
retained. No new core dependency, pool, backend, fuel or memory infrastructure.
The substantial performance objective remains open after this source gate.
