# Executable-prefix instruction attribution — R193

2026-11-10, committed1a83c64f-equivalent frozenac6edb6f; R192 focused/quiet gates
pass, full deliveryCI42792541280 was pending during collection and is now separately
[cachedGREEN](../2026-11-10-receiver-native-entry/qualification/delivery-ci.json). Same complete maintained
CSV/text scripts, worker/adapter0.4/dependencies/goldens. F4as_v7 Ubuntu24.04.4,
actual.NET11RC3, ordinaryGC/tiering plus explicit perf-map/JIT-output diagnostics.
Two6second warmups/captures;12.033execution seconds, no elapsed comparison.

| Complete script | Guest samples | Prefix leaves | Mapped prefix leaves | Three common fetch/check/dispatch-entry blocks |
| --- | ---: | ---: | ---: | ---: |
| Validated CSV | 1217 | 168 | 168 | 100 (8.22%guest) |
| Text summary | 1149 | 269 | 269 | 116 (10.10%guest) |

Every mapped sample matches a unique method version by tier and exact code size,
then a validated native instruction/group offset. All group offsets and code-byte
sums match the JIT's declared sizes. Mapping ambiguity is zero for these prefix
leaves; this does not claim zero unknowns in the wider profile. Selected methods
are final Tier1,10822/13657bytes. Required checks and useful work remain inside
these shares; no dispatch tax or speedup follows from inclusive categories.

The common blocks reload the current position/array bounds, form the instruction,
check admission/cancellation, and enter opcode dispatch. The loop's jumped local
is stored/read on the stack across the per-instruction exception boundary. This
supports assessing a continuous instruction run with cold exception routing and
unchanged per-instruction failure checks. It does not prove the exception region
causes every reload/spill or that changing it will improve CPU. Native call/input
groups still perform useful work; don't replace the existing engine with an arena
or routing protocol on the basis of these samples.

Bounded source checks reject absent costs: module facts already share preparation,
native CSV feeds its owned parser buffer, valid JSON strings already avoid CLR
string roundtrips, and short split fragments already use inline storage. No product
code changed for this attribution. No cancellation check/frequency change selected.

The installed SDK's local format check uses the documented
[JIT disassembly options](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/viewing-jit-dumps.md).
Its first byte counter accidentally counted lowercase jump-table dd directives as
hex bytes; the same output validates after a parser correction, without rerunning.
The mapping subsequently uses exact compilation indexes: two OSR bodies can share
a header but differ in size. All observed sampled groups/instructions are unchanged
by this correction. Both originals and corrections remain.

Identity/output/ownership/quiet checks, Closed/joined workers, temporary-map retirement,
RAM removal, archive-under-lease and localSHA pass. TerminalB541 cached. Initial
staging refused28KiB-short predicted free space without mutation; exact additional
archived catalog-copy retirement fixes sizing, preserving currentac6/qualifiede364.
No measurement restarted, active image or sole evidence retired. Publication off.

[Assessment](assessment.json), [exact mapping/assembly groups](prefix-mapping.json),
[raw samples/maps/JIT output](observations.tgz), [receipt](archive-receipt.json),
[source proof](source-equivalence.json), [prospective scope](milestone.md),
[tooling/original failures/corrections](preparation.tar.xz), [retirement receipt](space-recovery.json).
