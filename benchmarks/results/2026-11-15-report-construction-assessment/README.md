# Complete JSON-to-report construction assessment

R381/B771,2026-11-15, committed a7ab41f5 / frozen716176a8. One current complete
3,072-record JSON-to-Markdown script; no runtime edit or timing comparison.
Earlier R275 attribution at43541e78 predates shared executable storage. Current
attribution is justified to distinguish remaining complete construction costs,
not to repeat historical timing or infer a current CPython ratio.

After8guest-seconds warmup, one5s199Hz cpu-clock/fp capture records972guest-thread
samples over4.9guest CPU seconds;108unmapped leaves (62CoreCLR/36kernel), zero
ambiguous/malformed map entries. Wider thread CPU and every raw stack survive.
Exact source/core/auxiliary/catalog/golden/file/ownership/quiet/join/archive gates
pass; perf maps are the only diagnostic runtime override. No JIT disassembly.

Leaf counts include NativeRunCore58, instruction prefix58, append formatting50,
JSON string scan39, iterative construction37, native formatting35, call entry33,
file writing28 and padding24. Numeric formatting has20Lython/21BCL leaf samples.
Shared-budget Monitor enter/exit have40/41leaves; nearest Lython Reserve has97
samples. Other nearest frames include JSON string51, construction45, formatting45
and native-call36. These overlap and include useful/inlined work; they are not
removable shares, wall-clock attribution or a speedup forecast.

## Source and displacement decision

Current code already has a single iterative JSON container loop, direct final
record/shared-key construction, borrowed UTF8 token windows, exact admitted
escaped-string output and continuations only for actual suspended callbacks.
Formatted output already writes native bytes into final owned storage, prepares
constant specifiers once, and sizes a small initial builder from literals/widths
in the existing compiler walk. Numeric formatting uses native spans (a managed
char bridge on.NET10), not a missing per-number CLR-string conversion stage.
Direct field/receiver binding and final shared slots also already exist.

Do not add another parser/formatter engine, field-range inventory, speculative
value-proof cache or call arena from these samples. Earlier admission callbacks
can change later guest values; inspecting all fields up front would lose actual
evaluation order. The old separate formatted evaluator was removed for that
correctness reason, not merely a rigid performance threshold.

Budget synchronization is real work: the standalone allowance supports concurrent
owners, while a supplied host may reenter during admission. Reserve publishes
pending ownership before callbacks and preserves terminal refusal/rollback.
Removing locks, deferring admission, or handing out an independent chunk allowance
would change the contract rather than delete redundant construction. A future
operation-level combined preflight needs a complete capacity/overlap proof and
actual owner/semantic benefit; this capture does not establish one. Do not start
another memory-audit or hidden-credit subsystem to chase the samples.

Close this bounded gate by declining unsupported patches, not by claiming these
costs irreducible or the wider performance objective complete. Future complete
redesigns require a named displaced stage, maintained errors/protocols/order,
current cancellation, managed stack protection, terminal refusal, aliases and
joined funded cleanup, then a bounded warm/fresh complete-script source pair
with unchanged controls. Allocation and CPU remain separate decisions.

All workers finish normally and RAM traces are persistently archived and verified
under the lease before unmount. Initial setup used a nonexistent catalog filename
and stopped before root creation/service/guest work; a fresh lease-held inspection
proves this and the corrected source/hash/layout are retained. No sampling was
replayed. Current images/catalog/auxiliaries/toolchains and254/252MiBfloors remain;
no retirement or new core/catalog upload. Final free268,636,160B. RawSHA256:
`dba52b6839b2044f1601665f04dcbb3accbc8057ba173a699861176d6f1d31d6`.

[Current attribution](attribution-report.json), [raw](observations.tgz),
[source/procedure/original setup failure](source-and-analysis.tar.xz),
[source identity](source-map.json), [shared 585-input proof](../2026-11-15-csv-final-payload/delivery/source-proof.json).
[Full R379 qualification passes](../2026-11-15-csv-final-payload/delivery/README.md)
independently. No timing/CPU improvement, current CPython ratio,
startup/heap/embedding-tail or publication claim.
