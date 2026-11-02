# Passive compilation attribution — 2 November 2026

One owned 6.099-second service captures passive native CPU stacks during unchanged
compilation. **No runtime candidate, latency comparison or predicted speedup.**
This fills the ordinary CPU-attribution gap left by the intrusive allocation
sampler; it does not retime B81/B83 or qualify their removed implementations.

Runtime source remains delivered 4cae38f4; later revision 019f417f contains tests
and evidence only. Core SHA-256 is
`57a0649b03547c2f46cf8576fa3d5d74089fcb5d2fce7f4805ad2ec1b0f2ff3a`.
The Azure reference VM uses .NET 11 RC3, CPython 3.13.16 and ordinary adapter 0.4.0.
The owned worker reuses the existing borrowed-budget wrapper and complete text
summary helper source. No core dependency or native/unsafe implementation change.

After 128 compilation warmups and two seconds settling, cpu-clock at 199 Hz/fp
collects 2.01576 seconds, including the final batch of 32, and 2,080 compilations.
Normal GC/tiering; DOTNET_PerfMapEnabled=1 supplies diagnostic symbol metadata.
There is no EventPipe allocation capture. Pre/post complete Lython stdout/file
and CPython reference goldens pass; compile batches have no guest output.
Both quiet gates pass, final allowance is zero, actual worker/collector users
are joined and the owned fixture is removed normally. Service/lease proof remains
in the archive. This measures compiler CPU stacks, not execution/I/O/startup times.

All actual threads are preserved. The named execution worker consumes zero CPU
in this compilation window. Four actual compiler workers consume approximately
0.33/0.81/0.46/0.11 CPU seconds, while tiered JIT consumes another 0.32 seconds.
Actual CompileCore frames identify 335 of 402 sampled stacks across those four
workers. Selection uses neither the busiest thread nor core types mentioned in
other assemblies' signatures. Three declaring-assembly parser controls pass.

There are 37 distinct rooted stacks containing syntax/source-span GetHashCode;
all 37 pass through AbstractState's existing cache lookup/insertion. This selects
a source hypothesis: the cache asks record keys to hash syntax structure even
though its entries belong to particular immutable nodes in one binding state.
The state also stores versions while clearing the cache on every mutation.
These counts support a bounded existing-cache redesign, not a universal AST/value
rewrite or an exact removable CPU fraction. Recursive/inclusive method totals
must not be added together.

Limits remain explicit: 158 rooted leaf samples are unresolved native addresses;
316 distinct unknown addresses occur across all threads, with zero ambiguous
managed-map addresses. Other observed costs include traversal, casts, parsing,
abstract interpretation and executable construction. The window contains tiering
activity. Native origins may omit inlined/missing frames; this is not an elapsed
phase decomposition or proof of a particular speedup.

[pre-collection-plan.md](pre-collection-plan.md), [identity.json](identity.json),
[analyze.py](analyze.py), [native-inventory.json](native-inventory.json) and
[assessment.json](assessment.json) retain the question, source, overlap-aware map
resolution and counts. [evidence.tar.gz](evidence.tar.gz) contains the immutable
perf data, jitdump/map, full saved chains, original protocol/gate/cleanup records
and collector sources. A first metadata export omitted frame fields; its headers
are preserved separately. The corrected export reads the same perf.data; no
workload or capture is restarted. Ordinary .NET 11/Linux BCL native symbols in
those diagnostic stacks are not Lython implementation dependencies.
