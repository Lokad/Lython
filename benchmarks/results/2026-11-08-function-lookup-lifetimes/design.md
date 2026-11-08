# R121 bounded source gate: function-lifetime lookup facts

Source ca83509f/core9ae02fed, R119d05 full CI independent. No prototype yet.

Hypothesis: record helpers repeatedly read fixed fields but frame-local facts
cannot survive calls. R119 initially allocated96B each render; its refinement
correctly avoids once-only cache slots. A coherent design may move function lookup
facts to the existing function-definition lifetime, removing per-call cache
construction and resolving immutable row layouts across calls. This uses the same
canonical executable field reads/format instructions, not a second formatter or
speculative field-type proof. Affected: report render(record), ordinary CSV/JSON
helper/attribute workloads. R108dictionary~13% leaf motivates investigation but
inclusive routing is useful work; no dominant-cost or speedup prediction.

Assess~20min before code. Inspect existing function owner and bound-call workstate
borrows. Prefer existing function storage/lifetime and existing flat lookup entries,
no new owner/cache framework/policy/ledger, global code dictionary, arena or
cross-execution cache. Define exactly how ordinary and eligible native operations
borrow facts, without copying/binding carriers or losing current lookup semantics.
Displace function per-frame cache storage rather than retaining two redundant banks.
Modules retain their existing execution lifetime. Avoid speculative type assumptions:
format admission may mutate later fields into suspending protocols; actual reads
must recheck layout/current live value and use canonical suspension/error handling.

Prove definition/execution/cache/retained-function ownership, reentrant first cache
admission and callback-established backing, awaited pending users and recursion,
closures/class cells, concurrent independent runs, stale receiver retirement,
terminal first refusal before allocation and alias funding. Do not assume a guest
function can cross executions safely. If this requires new ownership machinery,
heap reachability tracing or broad API redesign, stop that candidate and record
specific uncertainty, not a permanent failed-design verdict.

If source proof passes, name displaced work/code, focused semantics/cancellation/
ownership actual11/10 and one bounded complete report/CSV/JSON before/after with
unchanged text/custom controls. Separate compile/allocation/CPU; preserve adverse.
Batch full platform/Stress/installed-core with R120 at retained coherent boundary.
No new qualification merely to repeat d05 or older b1. Regex remains independent,
publication postponed. No runtime change justified merely by this proposal.


Initial source findings, no prototype:
- PyFunctionBase already retains Closure and its MemoryGovernor; function metadata
  uses that execution/result owner. PublicProjection explicitly rejects function/
  unsupported runtime values, so do not invent cross-execution public call support.
  Imported/nested functions still use their existing closure-state lifetime.
- The canonical report's native body is absent; ordinary BeginBoundCall owns its
  borrowed bound-local workspace. Pass a reference to the existing function as the
  lookup fact owner through that workstate, instead of a global code-key dictionary
  or new scope abstraction. Module/generator lifetimes are already invocation-long.
- Initial scope should leave actual native-return execution unchanged: allocating
  a cache between a native mutable guard and work would invalidate that guard.
  Ordinary executable instructions can instead recheck current layout after first
  backing admission, preserving actual custom protocols/suspension per instruction.
- A function-owned first cache allocation must recheck after admission whether
  reentrancy already established its backing. Reuse that array and refund only the
  unallocated pending reservation; never overwrite/refund backing with active users.
  Existing TemporaryMemoryReservation can fund/commit it to the same governor.
- Function cache storage may remain conservatively charged until execution/result
  ownership ends even if the function is withdrawn; no tracing/refcounts needed.
  This changes retention timing, so document earlier denial and test bounded host
  batches/retained aliases/concurrent independent runs. Clear stale entries at the
  same successful-resolution boundary; never retain row values for field positions.

Still prove initializer entry paths and ownership handoff for ordinary/mirrored/
closure calls before selecting implementation. Remove function per-frame cache
allocation/cleanup, rather than add two banks or a fallback owner mechanism.


Entry-path proof: deferred BeginBoundCall/CreateExecutableWorkState and full
InitializeExecutableFrame cover linear executable calls; PyExecutableFunction's
ExecuteBody/ExecuteBodyAsync cover raw/lowered protocol invocation. All can pass
that same existing function reference, with no new call carrier. Modules/imports
keep their own invocation bank; generators already keep one bank over resumptions.
PyFunctionBinding creates function frames from Closure, sharing its execution state.
No public cross-execution function value exists: PublicProjection rejects it.
Existing function metadata is charged to the same closure MemoryGovernor.

Source gate supports a bounded prototype, not promotion. Add lazy flat fact storage
to the existing PyExecutableFunction object, borrow it from ExecutableValueStorage
for ordinary function instructions, and retain its capacity to execution/result
cleanup. Eliminate function per-frame cache allocation/refunds. Enable function
field sites that can repeat across calls; keep module loop/eager-comprehension gate.
Actual native-return execution remains unchanged to avoid admission between its
mutable proof and work. Document conservative function-cache retention. Validate
reentrant first allocation, fields mutated after earlier formatting/admission,
actual async protocols, closures/recursion, concurrent runs and allowance reuse.
No runtime edit yet. This is the next substantial phase, with source scope/lifetime
acceptance recorded before implementation; no speedup prediction.

Ownership refinement: public runs dispose execution scope after synchronous/awaited runtime cleanup; projection rejects functions and copies supported results to the result owner. Cache follows the existing execution-owned function metadata, not a new result lifetime/lease. Modules instantiate guest functions per execution; shared compiled code does not hold them. Preserve caller contract to await all runs before compiled-script disposal.


Initial c54a3ad3: focused28internal/286public per actual11/10/all5case golden/
ownership gates pass. Compile allocation unchanged. Desktop controls unstable
(report sync−7.94..+5.42%, async−5.33..−10.72%; custom async control22.1%spread).
Custom allocation+0.871% is reproducible. Census proves ExecutableValueStorage
72->80B and WorkState120->128B: the added owner reference widens every frame,
including custom frames that never cache. ~98KiB/4096records agrees with3*8B/frame.
All initial raw/source/core/logs preserved before refinement.

Bounded refinement: reuse the existing single lookup-storage reference for either
its private module/generator array or the existing function definition. Remove the
separate owner field, restoring frame width. Closed private managed shapes only;
no new owner/ledger/framework/options or guest value representation. Source semantics
and lifetime tests unchanged. Recheck focused actual11/10, one same-driver diagnostic
and census. Preserve initial adverse values; do not claim CPU from frame/byte proof.


Refined c6172fec passes83internal/2133public per actual11/10, all five complete
local golden/ownership/isolation gates. Census restores ValueStorage72/WorkState120B.
Custom allocation returns near neutral, other warm/compile allocation near neutral.
Desktop CPU/elapsed remain inconclusive: initial custom+0.871% allocation explained,
refined sync custom+22.1–25.4% elapsed adverse (reference spread2.62%); float/report
reference spreads16.3%/12.9%; async custom−1.15..+2.55%, report−4.27..−10.51%.
Preserve every pair; no timing retake to erase it. A single bounded quiet comparison
is justified by whole field-resolution work removed, source/ownership proof and
actual direct diagnostics uncertainty. Use qualified d05/core19b8 as final controlled
baseline for coherent R120+R121, not claim isolated CPU from incremental9ae pair.
8unchanged complete cases/API jobs as before, controls/cpu/goldens/balances/quiet/
join/unmount/archive/local gates; compare both roles to both old controls. Retain,
refine or reject with explicit debt. Publication and final adapter qualification
remain independent; never requery cached CI42519822299.
