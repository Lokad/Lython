# Runtime roadmap completion review

2026-11-15, committed runtime `a7ab41f5` / frozen `716176a8`. Bounded source and
existing-test assessment, **not new timing, backend qualification or a claim that
the whole performance objective is complete**. No production or test edits.
[Exact committed source/test map](source-and-test-map.json).
[This runtime subsequently passes full R379 qualification](../2026-11-15-csv-final-payload/delivery/README.md).

## Remaining frontend and invocation question

The hypothesis was that repeated frontend analysis or retained representations
still constitute a substantial removable compile-to-result stage. The current
source establishes:

- Lexical preparation supplies module/function/lambda/comprehension facts to
  analysis and executable construction. Scope validation records resolved nested
  nonlocal demand. The shared graph correction includes callback inspection;
  its temporary iterative worklist deduplicates shared facts and checks the
  current token/refusal. No runtime tracing or persistent fact cache is involved.
- Annotation flow returns immediately when neither annotation checks nor regex
  analysis require it. Its dead/deferred-scope behavior differs from ordinary
  refinement; simply merging those state machines would change diagnostics.
- Tokenization/parsing, scope validity, abstract interpretation, definite name
  binding, lowering and instruction construction have different outputs. Counting
  passes is not attribution. Entry syntax/lowered wrappers do not survive just
  to provide a whole-program alternate engine; retained lowered consumers have
  real class, annotation and protocol uses. Imports already compile executable
  code. Prepared parameter layouts and final invocation slots also already exist.

Named B764 fulfillment compilation is about 0.130 ms versus about 8 ms execution.
[B707's small configuration case](../2026-11-14-config-phases/README.md) separately
reports about 37 us compilation and 137 us fresh, with compilation already close
to CPython at its named revision. Independently warmed phase medians are not
additive parts of one execution. These cross-source observations are not a
current phase profile or an upper bound on all scripts. They do not establish a
dominant removable pass or warrant a compiler
rewrite. Close this source gate without another speculative implementation or
unchanged timing campaign. A new frontend milestone needs a concrete newly
generated script, phase attribution and an entire displaced stage, preserving
all diagnostic families, lazy annotations, scope/nonlocal rules, admission and
cooperative cancellation. Compile, warm execution, I/O and cleanup stay separate.

The [current file-call map](../2026-11-15-file-call-construction-assessment/README.md)
and [withdrawn entry design](../2026-11-15-unified-helper-entry-withdrawn/README.md)
likewise do not prove that another arena or exception-shell deletion is useful.
Future complete activation designs remain eligible; moving the same caller
state into the ordinary runner is insufficient. Shared final slots, direct
binding/receiver dispatch, prepared metadata and deferred contexts must be taken
as the starting point, not reproposed as missing machinery.

## Explicit 0.11 retention and regression decisions

Retain the selected completed architecture for the concrete benefits below.
Accept the original measured debts/uncertainties for 0.11; they remain evidence,
not noise to discard. This is a retention decision, not a claim that later changes
recovered them. Percentages are separate source-pair observations and cannot be
added. Superseded machinery does not survive merely to preserve an experiment.

| Historical source pair | Concrete benefit / retained invariant | Debt or limitation accepted for 0.11 |
| --- | --- | --- |
| [R359 primitive storage](../2026-11-14-native-primitive-storage/README.md) | CSV while-validator warm/fresh about 21–23% lower elapsed, 40.9% less allocation; exact promotion | Short helpers +3.7–10.2% elapsed, observed +2.3–4.0%, small JSON CPU adversity. R373 subsequently removes separate primitive banks; that deletion does not prove timing debt recovered. |
| [R361 range construction](../2026-11-14-closed-range-construction/README.md) | Final native-only constructor: counter elapsed about 32–33% lower, allocation −57.23%; one execution-owned factory and ordinary-router restoration | Short helpers +0.59–7.86% elapsed with moving controls. Keep uncertainty and the final source; do not substitute the earlier shared mutable constructor version. |
| [R362 range iteration](../2026-11-14-range-iteration-stage/refined/README.md) | Exact admitted progress/storage replaces per-step objects; counter elapsed about 28–31% lower, allocation −99.52% | Materialization allocation +1.26%, elapsed mixed. Original text pair up to +12.91%; justified same-bits B746 confirmation did not reproduce it, but combined pairs still include it. Magnitude/cause unresolved, not neutrality. |
| [R365 workspace](../2026-11-14-native-integer-workspace/completion/README.md) | Existing funded idle-workspace ownership removes per-call temporary storage; fulfillment allocation −14.57% | Short helpers +0.62–2.91% elapsed, observed small/mixed. Cleanup refinement was withdrawn and restored. R373 removes the separate long-array plumbing; funding/cleanup invariants remain. |
| [R368 protected helpers](../2026-11-14-protected-helper-activation/README.md) | Invoice warm about 17–18.5% faster / fresh 16–19%; search about 41–43% faster; allocation −29.19%/−74.65% | Short helpers +0.48–3.38% elapsed, text −0.30..+3.72% uncertain. Preserve fault provenance, stack admission and native cleanup. |
| [R370 builtin scalar results](../2026-11-14-builtin-scalar-publication/README.md) | Fulfillment warm about 3–4% faster / fresh 2.6–5.6%; allocation −18.20% | Invoice allocation −5% but elapsed +0.94–1.87% / wider CPU +2–4%; text/short/observed uncertainty retained. |
| [R373 shared executable slots](../2026-11-15-shared-executable-slots/README.md) | Removes duplicate value/word banks, loans, flags and transport; invoice allocation −7.89%, elapsed −1.81–2.80% with CPU support | Observed helper elapsed +2.83–4.65% / CPU +4.28–5.04%; possible fulfillment fresh +0.30–2.76%. |
| [R376 word segment](../2026-11-15-closed-word-segment/README.md) | One prepaid private segment replaces per-word activations; text warm elapsed −2.89–5.68% with CPU support | Allocation flat; fresh reference spread 8.77%; JSON elapsed +0.80–2.70% / CPU 0..+3.41%, unresolved after ABI restoration. |
| [R379 final CSV payload](../2026-11-15-csv-final-payload/README.md) | One immutable long-field payload replaces per-field arrays; metadata-heavy complete CSV allocation −5.557% | Small warm observation, no dependable broad CPU magnitude; fresh elapsed −1.53..+4.46% inconclusive; possible text CPU +0.26–0.99%. Conservative sibling retention is intentional. |

[Historical native helper returns](../2026-11-02-native-function-returns/README.md)
already have completed qualification at8f07764a: about19–20% less allocation is
established; the elapsed changes remain diagnostic. The expanded
[shared compiler-fact correction](../2026-11-02-shared-compiler-facts/README.md)
is fully qualified at61237f58, including the formerly separate callback query.
[Formatting confirmation](../2026-11-02-formatting-confirmation/README.md) supports
retention, with about 5–8% lower report elapsed/CPU and separate established
8.67% allocation reduction. Original adverse 15.125 ms remains unexplained.
Neither requires repeating unchanged qualification. Older closed thresholds and
single-attempt language describe their original protocols, not current policy.

## Independent regex development: core acceptance versus adapter gaps

The core owns Python flags/captures/errors, UTF-8 names and subjects, absolute
byte captures, scalar positions and original-subject windows, including reversed
windows/lookbehind. Compilation, imports and runtime bind the same provider.
Pattern preparation, matching, cursor construction/advancement and result
processing receive the current token; cached programs do not save caller tokens.
Explicit owners fund retained programs, subjects, capture storage and pending
users; cleanup precedes refunds and refused admission remains terminal.

The mapped existing contract tests cover native metadata after borrowed handles
close, malformed range rejection, replacements/split/expansion, flag analysis,
cache/purge/concurrent runs, current independent tokens, shared budgets, refusal
and awaited cleanup. Importantly, the cooperating provider blocks **inside** each
work stage; cancellation interrupts that work, then completion waits while cleanup
remains funded. These are contract controls, not proof of every real backend's
interruption/performance. Existing real-adapter tests qualify their explicit
capabilities separately. Core has no Utf8Regex package reference. Central version
metadata in restore files is not a runtime dependency edge; the local resolved
net11/net10 target graphs also contain no Utf8Regex edges. Native fnmatch/glob
matching uses its own cancellable wildcard implementation, not the regex provider
or a silent replacement dialect for `re`.

The optional nonpackable adapter stays at PythonRe 0.4.0. Read-only producer
[4d923414 assessment](../2026-11-14-regex-provider-capabilities/source-update-4d923414.json)
does not promote a pin: separate one-match scratch/escaping-result owners,
native UTF-8 group-name enumeration/copy, and token-independent cursor creation
with cancellable preparation remain affected final-adapter gaps. Native group
lookup alone does not satisfy enumeration. Development producers and adapter-local
bridges remain permitted; no blanket core development stop. Actual package,
managed-safety, backend interruption and performance qualification are distinct.

The broader objective remains open for new demonstrated whole-script bottlenecks;
this review does not manufacture another speculative redesign to keep it busy.
Publication/tagging remain postponed. Startup, contention, pending-I/O latency,
RSS/heap peaks and embedding tails remain unmeasured.
