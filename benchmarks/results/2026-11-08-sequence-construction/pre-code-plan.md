# R72 intrinsic sequence construction without lexical-context promotion

Selected source facts: plain list/tuple items are evaluated before MakeList/MakeTuple;
starred displays use another existing protocol path. Shared instruction prefix groups
these intrinsic opcodes with sets/dicts/match and requests a lexical context. Their
existing static builders require only scope's shared governor/checks and active value
storage. No user protocol occurs during copying native values. Deferred helpers thus
allocate an otherwise unused lexical invocation for a plain returned sequence.

Execute the entire MakeList/MakeTuple family directly in the shared prefix with
existing admitted builders; delete their ExecuteStructure cases. Keep set/dict/match/
starred protocols and all real context consumers. No new constructor/return shape,
binder/storage policy/context cache. Add the tuple builder's missing post-admission
cancellation check, matching list construction, before allocating its private backing.
Retain preflight, rollback, capacity/scalar headroom, input retirement/publication,
exact identity/order, aliases/escaping owners and actual pending cleanup.

Affected complete scripts: returned-record validation CSV (plain tuple results),
sequence-producing helper functions/comprehensions; ordinary CSV/text/JSON/report
controls. Native helper return optimizations remain. No predicted percentage or
universal CPU claim. R71/B427 corrects proven added Linux descriptor transport;
large CSV concern not reproduced, original adverse sources/data retained. This is
not an attempt to bury that original result under another gain.

Existing/new public sequence identity, pending-I/O cancellation, withdrawn aliases,
escaping projection and shared-budget reuse: sixteen cases first pass on unchanged
R71; tuple projects as object[], list as List, preserving existing contract. Add
storage refusal/trace/stopped-side-effect coverage before runtime edits, then focused
function/collection/stack/publication/admission/cancellation checks on actual11/10.
Existing five-script allocation diagnostic and one four-job affected/control source
pair only if semantically retained. Finish with removed cases, exact source/measurement
status and explicit remaining regression disposition. Batch corrected R71+R72 full
platform/Stress/installed-core qualification, not another old-source full cycle.
