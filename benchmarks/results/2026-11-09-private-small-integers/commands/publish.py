from pathlib import Path
import json, shutil
root=Path('D:/code/Lython'); home=Path(__file__).parent; out=root/'benchmarks/results/2026-11-09-private-small-integers'
table=[]
for name,label in [('desktop.json','Wrapper'),('refined/desktop.json','Numeric-view refinement')]:
    data=json.loads((out/name).read_text(encoding='utf-8'))
    for phase in ['warm-sync','warm-async']:
        for row in data[phase]:
            title=row['case'].removeprefix('scripts.')
            alloc=' / '.join(f'{v:+.2f}%' for v in row['allocationPercent'])
            elapsed=' / '.join(f'{v:+.2f}%' for v in row['elapsedPercent'])
            table.append(f'| {label} | {phase} | {title} | {alloc} | {elapsed} |')
readme='''# Contract-preserving private small integers — R176

2026-11-09, private variants based on b44fd910 with production equivalent to
qualified cac4acf8. Both variants are fully withdrawn. This is an allocation
experiment and bounded design assessment, not a CPU speedup or CPython comparison.

The initial source inventory selected a private immutable signed32 scalar: a24B
box rather than a32B BigInteger box. Larger integers retain BigInteger. CLR ingress
types and public guest BigInteger projection remain unchanged. Typed fresh producers
select the representation before boxing; public projection reserves32B before
materializing its result. No scalar cache, identity ledger, reclamation, fuel,
unsafe code or replacement memory allowance was introduced.

The coherent wrapper covered literals, ordinary arithmetic/parsing, shared numeric
classification, native eligibility, integer methods and module consumers. A bounded
refinement carried signed32 through the existing PyNumber view and common arithmetic,
truth and comparisons instead of reconstructing BigInteger at every such use.
The final variant changed58production files. Sixteen white-box fixtures required
representation-independent integer extraction while preserving values, exact integer
acceptance, refusal, side effects, ownership and cleanup assertions.

## Decision and measured limits

Both variants save6.91% complete-text allocation, about1.49% CSV,1.54% report and
3.79% Unicode control. Compilation allocation changes are below0.04% relative to
both unchanged controls. These are CLR allocations, not host allowance estimates.
Elapsed observations are mixed and include adverse results. Shared desktop process
JIT/GC and unstable controls prevent a CPU, equivalence or regression-size claim.
No quiet-VM confirmation was run and no historical CPython ratio is updated.

Withdraw because the dual representation and expanded numeric view do not simplify
the execution architecture, and the current evidence does not justify their source
cost for this milestone. This is not a percentage veto or permanent rejection of
machine integers. The existing text attribution also does not identify scalar CPU
as the dominant removable phase. Preserve the measured native-helper optimization;
continue the independent call/operand design assessment rather than adding further
scalar special cases to this version.

Every pair below is relative to baseline / independent baseline-control. Negative
means less allocation or shorter elapsed. Desktop elapsed is diagnostic throughout.

| Variant | API | Complete script | Allocation | Diagnostic elapsed |
| --- | --- | --- | ---: | ---: |
'''+ '\n'.join(table)+'''

## Conditions and correctness

Windows desktop, .NET11RC3 pinned SDK, existing complete-jobs driver and five-job
catalog. Two byte-identical qualified baseline roles; candidate changes only core.
Warm runs include mediated file I/O, full independent output checks and prior-result
cleanup; exclude compilation/startup/reset/protocol/final caches. Compilation is a
separate lane; its outer construction/disposal clock includes driver overhead.
All six lane captures pass goldens, zero returned balances, image isolation and
owned-fixture removal. No benchmark framework, policy or quiet-VM work was added.

Initial176numeric/lifetime checks pass. The first broad public run passes16838of
16931 and fails93: internal concrete BigInteger casts were incompatible with the
new private value. All original conditions/logs remain. Shared validated extraction
fixes the affected821public checks. The ownership/cancellation selection passes
all733existing cases; four new fixture failures are preserved: two required an
unjustified extra charge for an existing minor BigInteger box, and the alias case
included unsupported explicit int.__pos__. Corrected five public cases pass.
They still test unary+, exact promotion, alias methods, mixed numeric keys,
BigInteger projection, both APIs and accounting reuse. The unsupported member is
an existing frontend boundary, not removed supported behavior.

Both final wrapper/refined sources pass824focused internal cases, including new
projection admission/first-refusal and ownership checks. White-box failures on
concrete scalar representation and the two fixture compilation mistakes remain
in the original logs. No full-suite, platform, stress or installed-consumer
qualification is claimed for either discarded source.

Restore all production and all16modified white-box fixtures exactly; remove
PySmallInteger, its dual numeric view, helpers/guards/projection path and its two
representation-specific unit tests. Keep the five externally observable public
cases. Force fresh nonincremental core builds and run48retained numeric/lifetime
checks eachactual11/10: all pass. Runtime remains the already qualified source;
no repeated full platform/package qualification is necessary for this withdrawal.
The substantial whole-script CPU objective remains open.

[Wrapper identity](identity.json), [refined identity](refined/identity.json),
[wrapper observations](desktop.json), [refined observations](refined/desktop.json),
[restored checks](restored-checks.json), [withdrawal](withdrawal.json),
[source variants, measured cores, catalog and commands](source-checks.tar.xz).
Raw lane captures remain in warm-sync/, warm-async/, compile-sync/ and refined/;
all original failure/build/check logs remain in qualification/.
'''
(out/'README.md').write_text(readme,encoding='utf-8',newline='\n')
for name in ['verify-restored.py','withdraw.py','publish.py']:
    dest=out/'commands'/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(home/name,dest)

history=root/'benchmarks/EXPERIMENTS.md';text=history.read_text(encoding='utf-8'); marker='The [R175 ordinary call-input batch]'
entry='''The [R176 contract-preserving small-integer assessment](results/2026-11-09-private-small-integers/README.md)
preserves public BigInteger/CLR ingress and reproduces6.91% text allocation savings.
Both wrapper and numeric-view refinement are withdrawn:58production files change,
without a simpler execution architecture or established CPU benefit. Desktop
adverse/control-limited observations and original failures remain.824focused
internal checks pass each variant; affected public checks are labelled by source.
Keep five public semantic cases; exact qualified production restored and48retained
checks pass eachactual11/10 after forced builds. No quiet/CPU or repeat platform
qualification claim. Machine integers remain a possible future coherent design.

'''
assert marker in text;history.write_text(text.replace(marker,entry+marker,1),encoding='utf-8',newline='\n')

plan=root/'PLAN.md';text=plan.read_text(encoding='utf-8')
text=text.replace('| Active prototypes / unresolved measurements | R174/R175 prototypes fully removed after bounded source pairs; R176 source/proof phase next. No uncommitted runtime prototype.',
'''| R176 completed bounded assessment | Both contract-preserving signed32 variants removed, including58production-file migration and16white-box fixture changes. Text allocation−6.91% established; desktop elapsed mixed/adverse, no CPU claim or quiet comparison.824focused internal checks each variant; preserve source-labelled public outcomes and all failures in R176 evidence. Five public cases retained;48checks eachactual11/10 after forced restore builds. |
| Active prototypes / unresolved measurements | R174–R176 variants fully removed. R177 operand/storage source proof is active; no uncommitted runtime prototype.''')
start=text.index('1. **R174/R175 bounded batches closed;')
end=text.index('3. **Bulk records, text conversions and frontend consolidation.**',start)
text=text[:start]+'''1. **R177: consolidate operand storage and closed helper execution.**
   Active source/proof phase, not a runtime prototype or CPU claim. Existing R93/
   R168/R174–R176 evidence remains under EXPERIMENTS. R176's compatible private
   integers save allocation but add a dual representation across58production files;
   both variants removed.48retained public checks pass eachactual11/10 after exact
   production restore/forced builds. No permanent veto on a different scalar design.

   Confirmed current invariants: locals/temporaries already share one object array;
   work/value storage are structs; ordinary binding writes final reusable storage;
   lexical contexts are deferred; local-result publication already skips redundant
   stores, retaining their cleanup continuation. Native helpers borrow evaluated
   inputs without binding/workspace. Do not recreate any of those optimizations.

   Hypothesis: the existing instruction prefix can consume an admitted managed
   operand window for closed helpers, displacing their separate expression-tree
   execution while preserving borrowed inputs and measured allocation savings.
   First prove the concrete buffer layout: read-only incoming arguments, writable
   scratch, capacity/overlap admission before allocation, no mutation of caller
   packets, and all users funded through cleanup. Use existing array-backed spans;
   no unsafe APIs, raw tricks or a new stack policy. Actual suspension/context
   observation must receive owned storage before starting, with current values,
   instruction position and cleanup preserved, never replaying arguments/effects.

   Selected workloads: complete text helpers, validated CSV helpers and report
   rendering; config/Unicode unchanged controls. Source gate20minutes: identify
   the actual operand accesses/temporary storage eliminated and guard-before-effects
   proof before code. Existing profiles do not make all VM-prefix time removable.
   Stop this source phase at a named storage/compatibility obstacle; revise design
   or select item2 rather than append accessors or repeat an unchanged profile.
   A feasibility prototype may temporarily retain old native proof, but completion
   must remove displaced NativeReturnValue storage/recursive evaluator and duplicate
   compiler preparation, while preserving shared primitive helpers with real callers.
   No arena merely renaming storage, second dispatcher/backend or public option.

   Acceptance: native allocation benefit, mutable-global guards before effects,
   diagnostics/order, closures/descriptors/defaults/kwargs, generators, real pending
   I/O, cancellation/refusal, retained aliases and joined cleanup. Short controlled
   whole-script before/after, compilation separate; record debt/remaining uncertainty.
   Old call-arena and call-control slowdowns remain evidence, not permanent bans.

2. **Bulk records, text conversions and frontend consolidation.**'''+text[end+len('3. **Bulk records, text conversions and frontend consolidation.**'):]
text=text.replace('4. **Regex development continues independently.**','3. **Regex development continues independently.**')
text=text.replace('5. **Coherent delivery checkpoint.**','4. **Coherent delivery checkpoint.**')
text=text.replace('Broader frame/span work stays item2.','Broader frame/span work stays item1.')
plan.write_text(text,encoding='utf-8',newline='\n')
print('R176 evidence/history preserved; concise active roadmap advances to R177 proof, with existing optimizations explicitly protected.')
