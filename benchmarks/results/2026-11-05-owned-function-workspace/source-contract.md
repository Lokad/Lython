# D54 execution-owned function workspace, before code

Source baseline d608789a, production1fa4773e. D53 full42051400891 running;
earlier6c776d0d fully qualified. Current ordinary executable binding allocates its
combined argument/local/operand object[] on every call when capacity>3. D52 has
18 ordinary-CSV lock leaves first called by RentFunctionArgumentStorage and11 by
CloseInvocation; these are not total/removable CPU shares. Only widths1/2/3 use
three ThreadStatic pools. Existing binding already fills final locals; do not
claim a second copy or new direct calling convention.

Select one actual cleared array cached by the existing execution MemoryGovernor.
All function-binding arrays, including generated dataclass dunders, share this
owner. A cached exact-capacity array remains conservatively charged until reused,
evicted or execution resource cleanup ends. Taking it removes the idle-cache
reference; active/nested/suspended calls retain their existing bound ownership.
Return clears references only after every real user and cleanup finish. Keep one
idle array; if another is already idle, discard/refund the returned array. Evict
an idle different-sized array before one new admission, never in response to a
denial. Refusal is terminal even when a cache could satisfy later work. No size
classes, arena, slot windows, saved activation records, ledger, pool maps, retry,
new allowance, public option, token storage or changed frame/descriptor execution.

Use existing64+16*capacity conservative storage charge for every nonempty function
binding buffer before allocation, covering array/metadata/presence overflow
headroom. No cache hit refund/re-reserve. End idle reuse in State.CloseResources
after awaited root cleanup; release only idle capacity, never an active borrow.
Late returns after reuse ends release funding instead of rebuilding a cache.
Zero-capacity binds use Array.Empty and no storage reservation. General builtin
binding remains unchanged. Cancellation/stack/function admission remain at their
current effective boundaries. No argument/reference/user identity changes.

Delete the three ThreadStatic fields, RentBoundValues/ReturnBoundValues and their
size-specific cases. Consolidate bound-buffer admission/clear/refund in the
governor's owned cache. Update three dataclass finally blocks to the same complete
bound return. Preserve existing bind/entry/dataclass semantic cleanup controls;
replace array-identity-only observations with ownership and clearing assertions
where they no longer describe a supported invariant. Do not wholesale revert
allocation preflights, rollback or native helper-return optimizations.

Tradeoffs: widths1/2/3 become explicitly funded; one cleared idle buffer remains
charged between calls, so tight shared budgets can refuse earlier. No identical
tiny threshold guarantee. A different-size request ends idle ownership first;
replacement never has simultaneous semantic users. This caches actual storage,
not budget credit. One execution's cache cannot fund another execution. Storage
does not accumulate with sequential call count. No copy elimination, object-frame
allocation elimination or speedup claim before measurement. B69 arena's slower
window/save-state design and B61/D46 actual regressions remain separate evidence.

Acceptance: exact-size reuse with stable funded balance, no cross-execution reuse,
nested/held async arrays distinct and funded until cleanup, mismatched-capacity
eviction, no cache recovery after first refusal, clearing all references,
resource close releases only idle storage and late returns cannot recache,
sequential cleanup/budget reuse. Focus public functions/defaults/keywords/bound
methods, descriptor phases, generators/closures/locals, recursion, cancellation,
refusal and actual suspended callbacks; existing semantic controls first.

One<=180s complete ordinaryCSV warm/fresh/async, text and JSON comparison after
focused Release actual11/10; same frozen reference/current-control/CPython,
normalGC/tiering, quiet identity/golden/Closed/finalzero/joins/fixture gates.
Keep allocation distinct from elapsed; a short existing allocation diagnostic may
quantify only that dimension. Retain/refine/withdraw with explicit debt; bounded
confirmation/correction allowed for real evidence, no rigid percentage veto.
Batch full platform/stress/package at a coherent retained milestone. Whole
call/frame redesign and the performance objective remain open.
