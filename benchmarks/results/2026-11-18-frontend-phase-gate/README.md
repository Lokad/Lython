# Frontend phase and allocation source gate

R526,2026-11-18UTC. Exact qualified **3f873227 / a5038a28** core, six unchanged
catalog scripts. Managed diagnostics reuse the existing white-box assembly access;
no production hooks, counters, framework or runtime changes. Local Windows,
normalGC/tiering,30warmups/5x30compilations. Phase timing remains diagnostic:
different call shapes, tiering/GC and concurrent local tests limit it. This is not
a quiet-VM comparison, removable CPU percentage or predicted whole-script gain.

| Source | Frontend allocated bytes | Lowered tree | Executable code | Abstract interpretation within frontend |
| --- | --- | --- | --- | --- |
| CSV invoice | 161616 | 12840 | 54920 | 54704 |
| Report rendering | 74144 | 4912 | 27248 | 17704 |
| Positional calls | 29024 | 1984 | 13256 | 9432 |
| Helper text | 83216 | 6416 | 30984 | 18088 |
| JSON helpers | 90504 | 6408 | 31560 | 24744 |
| Floating JSON | 92184 | 7936 | 38184 | 24312 |

Figures are separate median allocations from diagnostic boundaries, not additive
CPU measurements. Frontend includes parsing/validation/fact preparation/analysis.
Lowering is roughly4.5–5.7%of the three-phase compiler allocation: these observations
do not justify a wholesale syntax-to-code compiler replacement. Parsing is also
material; abstract interpretation supplies86–94%of the separate semantic-analysis
allocation in the pass decomposition. No redundant whole parser or validation
pass is established, and the scope/callback correction must stay intact.

Source inspection finds a contained whole-layer simplification inside abstract
analysis: fixed arrays drive four statement and three expression singleton
analyzers through private interfaces, boolean family refusal and repeated syntax
switches. Even leaves test all expression families. No plugin/extensibility user
exists. A selected prototype can remove both interfaces/arrays/seven singleton
classes and route syntax once to existing semantic bodies. Lift capturing local
helpers into explicit static helpers where necessary; do not union unrelated
activation state into a large shared frame (earlier call-controller lesson).

Preserve binding updates, branch/refinement/short-circuit order, directive and
closure facts, annotations/comprehensions, regex analysis/provider semantics,
diagnostic codes/spans/order, compilation cancellation/refusal/owner cleanup.
Existing focused actual11/10 semantic checks precede a bounded compile/warm/fresh
whole-script pair with controls. This is a proposed deletion map, not evidence
that family selection dominates CPU or that a particular speedup will follow.
No type-fact cache, new AST inventory, universal visitor framework or public mode.
Keep all observed costs/adverse outcomes and decide any retained regression debt.

The initial three-phase program is in diagnostics/inspect. The nine-phase
decomposition maps to decompose-initial-Program.cs.txt; the eleven-phase passes
artifact maps to diagnostics/decompose/Program.cs. The decomposition omits the
private leading-indentation precheck, normalization and public-owner overhead;
all supplied sources were normalized and validated first. It follows the actual
frontend ordering, retains all diagnostics and does not compare internal sums to
the public/CPython clock. No full qualification replay or publication.

[Compiler phases](phases.json),[frontend decomposition](decomposition.json),
[semantic passes](passes.json),[allocation summary](allocation-summary.json),
[diagnostics/source](diagnostics.tar.xz).
