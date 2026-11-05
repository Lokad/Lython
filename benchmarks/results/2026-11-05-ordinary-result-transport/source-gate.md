# D62 operation-result transport — first source proof, no prototype

Current committed a63708a7. D61 full local passes both actual runtimes; independent
CI42081657838 active. Frozen comparison source/core identities stay unchanged.

Evidence already available: D58 complete-loop instruction/store/operator samples;
D52 text/ordinaryCSV samples; D61 basicloop-16.05/16.54% elapsed but CSV+0.77/+0.99%
confirmation debt. No universal boxing/call/context dominance inferred. Broad
whole-script objective remains open; do not call this one fused operation its end.

First actual source facts (no additional measurement needed to establish them):
- ExecutableInstruction is opcode+span reference+five ints. It has no independent
  operation-versus-binding span. Assignment's CompileExpression uses expression
  spans while CompileStoreBoundName uses assignment span. D61 only merges equal
  spans, which does not generalize to ordinary x=f(...)/x=a+b or multiline RHS.
- ResultSlot is also the original physical retirement start in actual value
  helpers. Forwarded InputStart may be local/constant and cannot stand in for it.
  Redirecting ResultSlot alone could clear newly stored locals or live inputs.
- Calls publish through three completed/pending helpers plus executable-child
  completion, not just ExecuteOperandValueOperation. Private builders/cursors/
  comprehensions publish multiple resource slots and are not guest results.
- Existing simple global/nonlocal/member targets and Python StoreLocalValue/cell/
  namespace mirroring still have real semantics. Generic direct destinations
  must preserve their distinct effects/errors, snapshot order, ownership and
  before-publication token/terminal-refusal checks after actual await.
- Native helper programs prepare before ordinary ForwardValues and keep source
  instruction references. Do not generalize a store rewrite across their program.

Next bounded proof: enumerate the existing publication/completion families and
representation choices preserving TWO spans and physical retirement extent.
Document changed storage/compile cost before choosing a prototype. Do not add
one opcode per arithmetic/method case, packed artificial slot ceilings, phase
flags, a second dispatcher or a new owner/ledger. If no coherent removal wins
this proof, stop/refine the candidate and continue remaining call/record/frontend
roadmap; do not mark the broad objective failed/complete. No timing campaign for
a design that has no viable source/retirement contract yet.

Other source checks completed while D61 qualified: final whitespace fragments/list
are ALREADY admitted together; capacity-aware CSV records ALREADY exist. Native
PyDict StoreItem ALREADY performs one native SetNativeItem probe. These cannot be
claimed as newly removed work. A context/interpreter merge changes escaping
lexical/error lifetimes and construction15/1017CPU leaves does not establish a
large opportunity. No prototype or blanket permanent rejection follows.

Regex independent read-only status: sibling48180b61/dev35, PythonRe package source
unchanged from6cf84468 except its BENCHMARKS.md. Existing exact provider gaps are
unchanged; no new qualification or adoption of uncommitted producer work.
