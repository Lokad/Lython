# Current activation and operand source gate

R504 examines committed62786960 and six existing complete-script sources. Static
IR is generated against exact qualified frozen5ae019db and priorcf847936; the
inspection executable verifies its loaded core hash. This is source evidence,
not CPU attribution or a forecast of removable time. No runtime change here.

| Current helper | Instructions | Slots / field-cache sites | Interpretation |
| --- | ---: | ---: | --- |
| JSON record amount |2|5 /1|Subscript, return; no mirrored locals|
| CSV reading helper |5|6 /1|Lookup, conversion call, arithmetic, return|
| Text generator |9|6 /1|Iteration, receiver call, yield/resume and loop|
| Floating helper |4|7 /0|Two arithmetic operations, local store, return|
| Floating generator |15|9 /4|Iteration, live globals/subscripts, call, yield/resume|
| Positional add |2|5 /0|Arithmetic and return|
| Record transform |4|6 /3|Two subscripts, final record construction, return|

Ordinary native calls already copy positional operands into the existing fixed
slot buffer or bind keywords directly into final storage. Receiver calls already
borrow operand windows; method implementations are receiver-independent. A new
argument arena or array-removal design would repeat delivered work. R503's cache
correction changes the floating generator's cache count from zero to four without
changing its instruction sequence. R495/R501's adverse dispatch rearrangements
remain evidence against treating selector reshuffling as a substantial redesign.

Join stages the iterable before validating/converting items. Rejecting a bad item
while consuming would suppress later iterator errors and effects. A general
streaming replacement must preserve ordering, hints, conversion, cancellation and
cleanup; no such redesign is selected from the presence of a temporary list alone.

Setup facts also differ from the earlier review: root functions/core types are
shared, and module variables do not copy builtins. Mutable callable/type mappings
remain per execution. Random state is a small scalar, not a large state array.
Sharing more metadata needs identity/isolation/concurrency proof and a measured
small-script cost. Existing config/report sources are selected for that missing
latency observation rather than another broad benchmark campaign.

One available contained text consolidation is reusing split's managed bulk ASCII
whitespace scanner for default trim, which currently decodes each ASCII scalar.
That removes per-character work across strip variants, with unknown whole-script
magnitude. It must retain Python's29whitespace scalars, explicit character sets,
UTF-8 boundaries, unchanged identity, admission and in-loop cancellation. It does
not close the larger activation/operand performance objective.

[Current IR](current-ir.json),[reference IR](ir.json),[source hashes](source-facts.json),
[inspection procedures/logs](procedures.tar.xz).
The first missing framework selection is preserved; no guest timing was performed.
