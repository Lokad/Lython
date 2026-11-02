# B81 - dense compiler CFG normalization, frozen before code

Current B80 allocation capture (not CPU shares) resolves repeated per-query
HashSet<int> construction in FinalJumpTarget, dense-block reachability sets,
ordering and dictionary remapping. Source makes the duplication concrete: targets
are walked independently in reachability, instruction rewriting and handler
rewriting. No missing interpreter/value path or universal compiler rewrite.

Hypothesis: one graph-wide canonical target table and dense block indexing reduce
compilation cost across ordinary helper scripts. Preserve executable output/runtime.
Delete per-target seen sets, reachable HashSet/sort and dictionary indexMap;
use compile-local arrays, one iterative path for pure-jump chains and a reachable
bitmap. No alternate engine, static operand layout, opcode fusion, runtime changes,
public option, dependency, pool/cache/ownership framework or unsafe code.

Canonicalization must exactly preserve old behavior: only blocks containing ONE
ordinary Jump are threaded; non-jumps/empty/multi-instruction blocks terminate a
chain. Every pure-jump cycle node maps to itself; tails map to their first cycle
entry. Resolve without CLR recursion; no silently chosen alternate cycle leader.
AbruptJump keeps its original destination so protected-region cleanup/iterator
lifetimes are unchanged. Reachability keeps original entry, fallthrough and
exception-handler reachability; normalized order and every target/span stay exact.
Detect invalid internal references as compiler defects, not silent fallback.

Existing compilation owner preflight covers rough AST/code/working capacities;
new dense working arrays replace many transient sets and remain compile-local.
No storage escapes unowned. Preserve first-origin terminal refusal and current
compilation stage token checks. Existing post-parse analysis cancellation remains
between stages; do not claim a new finer interruption policy. Runtime cancellation,
managed stack admission, genuine async calls, closures/generators and cleanup stay.

Validation: independent known cycle/tail cases and exhaustive small functional
graphs against the old reference resolver; long chain verifies iterative handling.
Focused existing Release executable control flow/loop/with/try/finally/generator,
frontend diagnostics, compilation admission/cancellation and import semantics on
actual.NET11/10. Do not weaken internal balances/timeouts or add architecture-only
compatibility scaffolding. Baseline/current source frozen in original/, DLLs in
baseline/; package/worker hashes verified against delivered4cae38f4 correction.

ONE <=45s owned VM before/candidate/identical-before comparison, shared CoreLib,
actual hash witnesses, identical auxiliary bits, normal GC/tiering/no forced GC.
Cases: compile-only unchanged complete CSV-helper and text-helper sources,
fresh compile+full file-search job, warm full text-helper runtime control.
128 interleaved warmups/image/case,2s settling,7 rotating batches. Compile batches
1024, fresh file batches64, warm text batches32. Include compiled-owner disposal,
full stdout/file goldens where executed, result cleanup, per-job budget baseline,
final0, owned fixture removal and both quiet gates. Compile/startup boundaries
labeled; no Python timing or external ratio. No new benchmark framework.

Retain only >=7.5% elapsed compilation improvement in BOTH compile cases versus
BOTH originals, fresh/warm control regressions and original drift<=3%, IQR<=10%,
batches>50ms, every semantic/ownership/quiet/hash/cleanup gate passing. This targets
compilation; do not imply the whole CPython gap closes. Otherwise withdraw the
ENTIRE graph prototype, fresh rebuild, keep independent semantic regressions and
all adverse evidence. No retiming/rescue/threshold changes or retained fragment.
Full platform/originalStress/installed-package qualification only at retained
runtime delivery; never requery prior terminal CI or republish anything.
