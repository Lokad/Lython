# R281: authoritative captured slots

Qualified production is b3369e3e, documentation98585baa. R280 is withdrawn.
Source demonstrates duplicate cell/local storage, not another frame/argument
array: ExecutableCell.Value writes its defining frame's LocalCells-indexed cache;
generator resume resynchronizes it. Ordinary variable resolution, enumeration,
binding, pattern/import/fallback writes also carry the sparse cell array.

Represent captured slots by their existing cell object in the already-owned local/
operand array. All guest reads unwrap it; all writes update that one cell. Remove
the sparse LocalCells array, owner-slot backpointer, active-frame cache propagation,
generator cell-to-slot synchronization and duplicate method parameters. Keep live
namespace mirroring, incoming closure identities, unbound diagnostics and actual
generator storage. This is neither raw-long cell storage nor the slower R268
prefix/R272 segment/R280 check-removal design. No new backend/cache/opcode/policy.

Captured-text and closure-heavy scripts are affected. Existing R272 native guard/
evaluation and instruction samples establish recurring execution but do not isolate
the setter's CPU or predict savings. Guaranteed outcome sought is one authoritative
storage location and one fewer per-captured-frame array; CPU remains a hypothesis.
Uncaptured loads acquire an internal-cell type test, a potential general-path debt.
Native dictionary loops prove relevant slots uncaptured and retain their raw access.

Acceptance: exact aliases and live updates through defining/sibling functions,
generators, class/annotation fallback, namespace mirroring, match/import/handler
deletion, real protocol suspension, terminal refusal, cancellation, retained users
and shared-budget reuse. Borrowed argument arrays remain alive and are cleared only
after frame users end. Captured cells keep their existing retained owner/funding.
Remove obsolete internal representation assertions, preserve semantic tests.

Complete migration then focused Release actual11/10 and seven unchanged scripts/
both APIs/compile/fresh. One bounded captured-text/general-load-control comparison
will decide retention/debt; refine only for a demonstrated semantic/cost issue.
No automatic wider tagged-value/closure/compiler project or unchanged profile replay.
If retained, batch full platforms/stress/installed consumers at delivery. Publication
held; missing regex producer capabilities block only affected final qualification.

## Implemented source checkpoint

Seven production files now use authoritative cells in the local array. Two
existing white-box fixtures build that storage directly; all independent refusal,
funding, retention and value assertions survive. The namespace retention handover
must seed cells before setting MirrorLocalStores. Initial actual11 checks pass1055
internal but fail three public generic-function cases; original logs survive under
initial/. The correction removes the old cache-dependent handover. migrate.py
reproduces the initial source; namespace-corrected.patch describes the corrected
source, not a frozen first-form binary. Do not relabel the initial failures.

Corrected nonincremental build succeeds. Focused1055internal/2674public per actual
11/10 pass (7458 executions). A second, overlapping public annotation/match/import/
locals/context-manager filter passes3683per target; these are not additional unique
test counts. Cancellation/refusal/actual pending cleanup checks remain unchanged.
All seven existing scripts pass both APIs, compilation and fresh execution; every
golden, shared balance and joined fixture check passes. Compile allocation flat;
whole warm allocations effectively flat (-0.00..-0.04%, not a substantial reduction).
The sparse array removal is per captured frame, not per native helper invocation.

Desktop capturedRun elapsed-5..-9%/Async-13..-14% is diagnostic. CSVRun+1.8..2.5%
and Async+3.9..4.1% with stable local controls are adverse; retain them. Fresh captured
+4..26%/17%control and freshCSV123%control remain uncertain. No source-independent
compiler CPU change or causal JIT explanation follows. Initial four-job VM
preparation is preserved; before launch it expands to five to include the actual
CSVAsync adversity. Same source, not retiming. B638 has360sowner/30sstop/30smargin.
