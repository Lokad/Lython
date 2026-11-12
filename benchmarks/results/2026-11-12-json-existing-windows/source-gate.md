# R279 — JSON token storage over existing windows

Base b3574b5c, production qualified41bd869b/frozenf0b736b2. Previous goal turn
made authoritative progress: GREEN qualification/source proof, seven selected
CPython jobs and redesign gate committed/pushedb3574b5c. No external jobs remained.

Current source inspection closes two repeated suggestions: literals/static format
widths are already prepared; builders transfer payload backing; encoded output is
already bounded/batched. Native locals/operands/binding share existing storage.
Do not replay the slower R268 prefix or R272 segment-entry forms.

R54/R60 shared JSON tokens but changed every string's representation/accessor or
hash policy. They were withdrawn, with adverse measurements preserved. R271 has
since introduced array/offset/tag immutable windows independently. R279 therefore
changes the JSON storage boundary without adding a field, global accessor shape,
hash policy, pool, owner, tracing or a second parser. The existing storage tag is
mutable only to move a same-owner input backing to command lifetime. This can have
JIT/control costs; unchanged controls must measure them, not assume they vanish.

Before a plain non-inline view is allocated, admission covers its shell and,
on the first owned-input transition,32B array metadata. Commit that metadata and
mark the source as an immutable window before publishing its child. Subsequent
source-shell cleanup cannot refund the backing capacity/header. New views refund
only shells; full backing conservatively ends with the command owner. An existing
reader window already has that owner. Other-owner/unowned, escaped and inline
values copy. Initial denied/cancelled admission leaves the source ownership intact;
no retry/fallback follows refused admission. No new public API or independent cap.

Tradeoff: small surviving native values can retain a large input/full spare
capacity. Exact arbitrary-reference recovery is not promised; bounded streaming
uses host-driven commands. Public CLR projection can copy strings and end the
guest lifetime; retained projected storage keeps its separate owner until disposed.
This distinction corrected the new public fixture's initially invalid assertion.

Final production snapshot:568files, exactly two changed, core4f57ca33. Initial
nonincremental Release solution build and later test-only builds recorded. Final
focused253internal/891public per actual11/10 pass (2288), including genuinely
pending JSON hooks/late cancellation, full backing, nested offsets, cross-owner
copies, terminal refusal and shared result reuse. A spare-capacity case is added
after launch, with its final check recorded separately. It changes no production.

Seven existing complete scripts pass both APIs, compilation and compile-to-result,
independent goldens/shared balances/joined fixture cleanup. Report allocation−4.68%
UTF8/−4.46..4.49%UTF16; JSON-lines allocation flat, compilation flat. Desktop clocks
are diagnostic: original/adverse async report+3..7%, compilation controls up to75%
spread remain. Two launcher mistakes stopped before a complete-job observation:
folder paths instead of worker DLLs, then revision text instead of the existing
Python hash witness. Original stderr/state retained; correct protocol/driver reused.
A nullable test-return compile failure is likewise retained, with corrected checks.

B634: same worker/adapter/runtime/normal GC/tiering, four jobs(report/JSON/text/CSV),
two processes/image,7rotated500ms rounds;8swarmup affected,12scaptured,5sCSV.
300sowner/30sstop/30smargin. Fresh idle lease and unchanged264MiB staging floor
pass; no old evidence/image removed. Do not observe until the recorded deadline.
Keep all adversity, classify allocation/CPU/architecture separately, and explicitly
decide retention debt. Final platform/stress/installed consumers follow only if
the coherent prototype is retained. Publication and broad performance goal stay open.

## Retention checkpoint

Final spare-capacity checks pass254internal/891public per actual target=2290.
B634four208.984s: report elapsed−5.66..−3.03%/widerCPU−2.32..−1.20%; favorable
text control and moving JSON/CSV references limit attribution. B635three143.230s
uses unchanged cores/profile to check actual original async adversity. UTF8report
elapsed−3.57..−2.49%/CPU−2.46..−0.62%; UTF16 and CSV CPU remain uncertain. Retain
copy removal/allocation reduction/modest UTF8report result, with explicit control,
retention and unqualified UTF16 debt. No further unchanged timing for this decision;
not a permanent closure. Both terminal states cached/localSHA verified; no VM users,
RAM fixtures or evidence deletion. Full retained-source delivery follows.
