# R120 delete unused member-cache acquisition

R119 measured custom allocation+0.575%,~65.7KB. Mutable instance member lookup can
never satisfy CanCacheRuntimeMemberValue, yet TryReadMemberCache admits/allocates
its table anyway. Flat24B versus8B reference slots makes4096cold entries~64KiB
larger. Reuse/consolidate the existing instance/type/super/function/dynamic receiver
exclusion before table acquisition. Remove duplicated eligibility checks and unused
array/reservation work. Preserve all actually cacheable receiver preflights; clear
an existing stale entry after uncached resolution without allocating a new table.
No new owner/pool/ledger/options, field-layout policy or CPU promise. Native
wrapper allocations remain covered by existing conservative major-capacity/
scalar headroom and their own substantial-storage preflights. This removes unused
capacity only; refused actual admission stays terminal and protocols may suspend.

Affected scripts: custom Quantity CSV and ordinary descriptor/helper workloads.
Existing member/receiver semantics, field admission callbacks, changing members,
actual host suspension/cancellation/refusal and cleanup on actual11/10. One short
same-driver5case diagnostic, compile separate only if metadata changes (none).
Allocation improvement and source simplification are distinct from elapsed/CPU.
R119 full CI is independently active for d05b3359; do not duplicate it. R120 stays
an active prototype until a coherent retention/delivery decision, no final package
claim for changed source. No edits to the frozen R119 source pair or evidence.
