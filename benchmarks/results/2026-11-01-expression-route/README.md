# Complete executable-expression lowering (B48)

Source **082dd130 + source.patch**, 2026-11-01. Architectural deletion and Python
correctness, with no speedup claim. [Prospective criteria](pre-code-plan.md) were
recorded before production changes; [exact patch](source.patch.gz) is preserved.

## What disappears and why

All ordinary lambdas/comprehensions/generator expressions already have executable
lowering. The remaining generic expression opcode has one real supported consumer:
starred variadic parameter annotations. Captured expressions are injected into
already-evaluated function/class headers after body compilation; their actual
lowered evaluation remains. Unknown-expression wrappers already throw in both
lowered evaluators and carry no supported behavior.

Compile starred annotations with the existing one-value `ExecutableUnpackValues`
operation. Delete `EvaluateFallbackExpression`, its factory/accessor, builder and
code-object storage/constructor argument, interning/yield-rejection helper,
sync/async detours and local-mirroring trigger. Replace `LoweredOtherExpression`
and its throwing dispatch branches with an immediate compiler-defect exception.
Remove obsolete empty-array assertions, preserving independent opcode, mirroring,
semantic and ownership checks. No new opcode, dispatch framework, cache, policy,
owner infrastructure or public option.

Keep genuine lowered class/annotation/lazy-metadata consumers, captured headers,
statement fallback and required namespace mirroring. One added effect on the
existing lowered interface preserves synchronous/asynchronous host behavior; its
small header helper reuses bounded materialization and unpacking errors.

## Correct Python behavior and containment

Python unpacks exactly one item from a starred annotation. Original Lython accepts
only type packs/generic aliases. Six of eight initial independent probes mismatch;
13 of the first 16 xUnit goldens fail on the original runtime. The fix supports
ordinary/custom iterables, correct arity/type errors and ordinary guest-raised
MemoryError. Shared unpacking errors now qualify `typing.TypeVar` correctly.

[22 isolated CPython 3.13.2 goldens](goldens.json) cover metadata, packs, arbitrary
values, Unicode/bytes, indexed iteration, bounded consumption, no length-hint
callback, definition order, class/closure/generator scopes, suspension in an
annotation and postponed evaluation. Both execution APIs pass. Six controlled
async tests cover module/existing-lowered class consumers, actual host suspension,
current tokens, cancellation/refusal despite guest suppression, retained result
funding, awaited outstanding users and shared allowance reuse. A subsequent run
uses a fresh token. No timers or checkpoint-count assertions provide the window.

Materialization reads at most two values to enforce semantic arity. Its existing
capacity admission can reject tighter budgets earlier than the old special case;
first refusal remains terminal. Original values stay aliased and funded through
cleanup. No tracing, weak reclamation, forced GC or abandoned work is introduced.

Fresh nonincremental build: zero warnings/errors. Focused **415 native / 608 public
per target** and full ordinary **3,962 native / 15,527 public per target** pass on
actual .NET 11 RC3 / 10.0.8. Final platform/stress/package outcomes are recorded in
PLAN.md when terminal; local checks do not imply those outcomes.

## One unchanged-workload cost control

Dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11
RC3, ordinary GC/tiering. One **26.112-second collection / 26.134-second service**,
three ordered before/candidate/identical-before warm CSV processes. Four warmups,
seven batches of 128 complete jobs. No profiler, retiming or favorable selection.

| Per complete warm CSV job | Before | Candidate | Identical before |
| --- | ---: | ---: | ---: |
| Process CPU, ms | 7.54609 | 7.72546 | 7.47547 |
| Elapsed, ms | 8.35134 | 8.30912 | 8.31332 |
| Allocated bytes | 9,542,705.69 | 9,542,259.88 | 9,542,656.81 |

**Possible CPU cost remains visible: +2.38% / +3.34%.** Elapsed changes
-0.51% / -0.05%, allocation essentially unchanged. Baseline CPU drift -0.94%,
allocation drift below 0.001%; prospective 5% / 0.2% control bounds pass. Every
CPU batch exceeds 100 ms. This short diagnostic establishes no causal fraction,
repeatability, external-baseline ratio or runtime speedup. Correctness and deletion
have independent value; no performance target excuses missing Python behavior.

Warm public Run includes fixture reset, mediated file I/O, full stdout/file goldens
and result disposal; compilation/startup excluded. Shared unbounded funding
exercises normal ownership; finite refusal is tested separately. All quiet,
golden, balance and cleanup gates pass; children join, service inactive/dead/success,
VM lease available. Auxiliary host/adapter binaries are byte-identical; only core
DLL/PDB differs, with fresh equal core/worker/test/probe copies before freezing.

[Identities](candidate-identity.json), [summary](summary.json), original probe/test
failures and [raw results](raw-results.tar.gz), SHA256
`c755d569962cb5e44f722e18aca83ab454e8fac81fdd66f6026ca67575e5c780`, preserve all
samples, measured source, catalog, machine gates and existing driver adaptations.
No new benchmark framework or core dependency.
