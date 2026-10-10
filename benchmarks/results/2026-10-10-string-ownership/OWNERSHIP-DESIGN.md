# Pending design: fresh split ownership

Historical proposal: implemented with a storage decorator and per-item escapes
in the subsequent round. See the [implemented ownership design](../2026-10-10-deferred-split/OWNERSHIP.md)
and [comparison report](../2026-10-10-deferred-split/README.md). The text below
preserves the original tentative representation and its acceptance requirements.
The short boundary diagnostics identify adoption as a target without proving
native-handle cost. The direct string guard did not improve the public pipeline.
Further guard/layout-only changes need a concrete new hypothesis; do not repeat
rejected experiments to seek favorable results.

The structural hypothesis is to keep fresh, unique split strings funded under
a shared ownership record until they escape the container. A read-only builtin
such as string join could borrow those values without publishing them to guest
code. Normal guest indexing, iteration, aliases, copies and projection must
acquire independent ownership before exposure. This can avoid eager per-item
registrations in the split/join path while retaining full Python behavior.

The prototype must first establish an explicit ownership state machine. One
possible design is a shared funded coupon record plus a pending-owner link in
each eligible string. The shared record must not strongly retain its container
or siblings. Pending strings and their container keep it alive. All construction
and metadata charges remain accounted; potential registration tickets can be
prefunded conservatively rather than dropping ordinary safety accounting.
Independent registration transfers an existing funded coupon transactionally.
Tier growth still funds before allocation. Failed transfer leaves the original
ownership intact, with no published mark, duplicate refund or uncharged value.

This design remains uncertain. A shared arena or parent reference that pins all
siblings behind one escaped item can cause unacceptable retained memory. A weak
container link that loses funding while an escaped item stays live is unsafe.
A missed escape path must never undercharge; conservative funding does not
excuse avoidable long-term retention or repeated false denial. Stop and revise
the design if these properties cannot be proved.

Before coding the prototype, map all paths that can expose or discard list
elements: guest indexing, iteration, slicing, copy, concatenation, repetition,
pop, remove, delete, clear, assignment, sorting callbacks, container conversions
and public projection. Internal storage/enumeration paths can bypass the public
list getter. Decide explicitly which paths acquire ownership and which are
proven borrows. String join may borrow exact native strings without guest
callbacks; arbitrary iterables or callback-bearing operations must keep their
existing behavior.

Limit eligibility initially to fresh, unique governed strings from a split
construction transaction. Shared empty strings remain unowned. Existing tracked
strings, aliases, cross-container/cross-pool values and cache-bearing values
must preserve their established ownership. Generalizing other split-family
producers requires evidence of the same construction and escape invariants.

Required evidence before acceptance:

- Retaining one item and dropping its list releases other items without losing
  the retained item's construction/cache charge or pinning sibling payloads.
- Copies, slices, repeated references, nested containers and projection preserve
  reference identity and exact ownership transfer without premature refunds.
- Removal, clear, storage replacement and abandoned sources release only
  genuinely orphaned charges, while retained items remain funded.
- Denials at metadata creation, tier growth and publication roll back exactly;
  caught failures and funded retries recover. No budgets are relaxed.
- Cache construction after escape updates the independent coupon; cache denial
  preserves earlier ownership and charges.
- Default limits, checkpoints, cancellation and host-mediated boundaries remain
  active. Untouched generic registrations preserve their contracts.

Prepare meaningful lifecycle/accounting regressions, then compare one frozen
candidate against the current runtime on short split/count and split/join cases,
with loop/empty and Unicode controls. Declare order and repetitions before
collection; keep all failures and variation. Accept only repeatable public
latency evidence plus correctness. Full lanes wait for a separately declared
milestone. C05 causal diagnosis and Utf8Regex integration remain separate.
