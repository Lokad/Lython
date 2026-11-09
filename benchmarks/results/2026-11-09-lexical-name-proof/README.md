# Native lexical-name proof correction

R139a,2026-11-09. Full CI42563792033/source df3 found two existing public
class-scope regressions on both Windows/Ubuntu actual.NET11: unresolved method
names incorrectly read module globals instead of their enclosing function cell.
All4446 internal checks passed;16857 public passed/two failed perplatform. Later
actual.NET10, package and original Stress stages were skipped. The source is not
fully qualified. Terminal observation and complete failures remain here.

R138 removed a lexical-parent eligibility barrier. Separately compiled class
methods can have zero explicit captured slots while using LoadName for incomplete
lexical facts. Native reading used a parent-depth heuristic to treat some such
names as globals. Zero slots therefore did not establish the claimed closed proof.

Decline native proof for unresolved LoadName; only explicit LoadGlobal qualifies.
Delete the runtime depth/class/nonlocal heuristic. Ordinary executable resolution
retains Python lexical semantics and promotes its context where required. Complete
module/nested functions already carry explicit global opcodes; their optimized
paths remain. No new flag, backend, cache or policy; supported behavior preserved.

The existing two failures reproduce locally (35pass/two fail). After correction,
101 focused public checks pass on each actual.NET11/10, including both original
regressions. These runs include the active incoming-cell-read prototype; this is
not standalone source qualification. A fresh corrected baseline and final candidate
will be frozen separately before the next source pair. No performance claim from
this correctness change. Prior measured R138 snapshots and adverse evidence remain
historical; full corrected platform/package/Stress delivery is still required.
