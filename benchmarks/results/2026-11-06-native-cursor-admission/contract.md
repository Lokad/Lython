# Native cursor working ownership — development contract

One existing Lython reservation in the cursor child scope funds the native live
working high-water capacity. Admission precedes covered native allocations and
replacement overlap. Native release ends the corresponding live borrow; funded
capacity remains conservatively reserved until Close/exhaustion, then all native
users finish before scope disposal. This is reusable capacity, not cumulative
allocation counting. No per-buffer ledger or new allowance/policy/collector.

The bridge's Release never calls a potentially throwing/cancelled host hook.
Refusal while growing the aggregate reservation throws LythonRegexException with
MemoryAdmission immediately: do not return false and allow producer optional
storage recovery. Native failure unwinds before refunds. Program and working
owners remain independent. Fixed conservative metadata/range-conversion overlap
is funded before native cursor creation/publication. Current-token CTS forwarding
is still required because native creation tokens remain authoritative on advances.

Intentional tradeoff: earlier denial can occur while relinquished native scratch
capacity remains reserved until cursor completion. Long match streams remain
bounded by peak live working storage, not the number of published matches.

Keep one-shot working estimates/program preflights and adapter-local name bridges:
the one-shot API cannot select Prepare's independent working owner, and UTF8 name
lookup does not enumerate all names. No core backend dependency, normal pin
promotion, released-producer safety claim or production publication.
