## Priority 1 — B167 completed-fault driver correction/qualification

B165 original41647907097 terminal: Ubuntu fully PASS, Windows direct-Sum allocation
outlier. Fixture-only76725065 preserves input/bound and isolates/warms measurement.
Corrected41651037909 terminal: Windows fully PASS; Ubuntu byte-sum/whitebox4040PASS
but copyhook guard fixture reaches unchanged25s/26.81CPU s. Both terminal receipts/
logs retained; NEVER re-query. No timeout/input/stack-bound relaxation or retry-until-green.
Quiet VM paired original3-rejection fixture: B16210.43s/11.08CPU, B16511.33s/12.08CPU,
both all goldens PASS under25s; exact CI timeout cause not established. Initial
private fixture DLL-link/load failure retained; corrected bootstrap copies used.

Source demonstrates completed faulted/cancelled ValueTasks are sent through async
forwarders despite having finished. B167 hypothesis: classify completion by
IsCompleted, consume once in the native prefix (including faults), retain async
forwarders ONLY for actual incomplete operations. This removes needless async
fault/cancellation relay frames/allocations; no new framework, fields, policy or
owner. Preserve originating spans, guest routing, first refusal/cancellation,
awaited cleanup and genuine pending users. Source scope: shared prefix/completion,
member/iterator/disposal and call completion only, not a blanket repo replacement.
Focused BOTH-API failure/cleanup/refusal/cancellation and existing full owned stack
scenarios, plus one paired25s copyhook witness and short complete-script control.
Keep all previous adverse evidence; no unmeasured normal-path speedup claim. Freeze
after joined checks, then established platform/Stress/package delivery. JSON gate
below stays next; overall whole-script objective OPEN. Publication postponed.

