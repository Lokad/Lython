# Temporary reservation callback ownership

R301 reviews the complete executable call/storage lifecycle. Bound arguments already
serve as locals, native completion avoids pending calls, and real continuation
transfers final slots once. Lazy namespaces and compiler operand forwarding also
already exist; no duplicate frame, argument arena or new execution controller is
selected from this review.

The shared temporary reservation has two reproduced ownership failures:

- `Shrink` refunds before reducing its own balance. A refund callback disposing
  that owner returns the same bytes twice, leaving an independent 96-byte live
  reservation funded by only 80 bytes. Throwing callbacks reproduce the same loss.
- `Grow` can return after its admission callback disposes the owner, allowing a
  subsequent allocation without an active owner and stranding new reservation bytes.

The correction publishes shrink/disposal balances before external refunds and
returns unallocated growth when its owner ended during admission. Growth then
stops, preserving any first memory refusal, including refusal raised by its refund.
Existing scopes/reservations remain; no new policy, owner type or public API.

The before-fix tests reproduce three failures and one passing first-refusal case.
The corrected suite adds the growth-refund first-refusal case. Each actual .NET11
and .NET10 target passes **854 internal /216 public checks**, including all five
new reservation cases, cancellation, shared allowance and call/JSON ownership.
Release builds pass. [Validation](validation.json),
[frozen source, binaries and original logs](source-checks.tar.xz).

One short before/after diagnostic reuses the existing seven complete scripts and
unchanged worker/driver. All golden outputs, final balances and fixture cleanup
pass. Allocation is approximately flat. Desktop elapsed controls move by up to
22.43%, so **no CPU/elapsed improvement or equivalence is claimed**. The correction
is justified by reproduced ownership failures. No extra timing campaign is needed.

Measured source-pair cores are reference `0370472f` and corrected `6b468bf1`, both
built at Git revision `3a063cb0`; the source archive records all571 production
inputs and their one-file delta. Full platform/stress/installed-package delivery
is pending from the retained-source commit. Publication remains postponed.

Runtime **fc2531b8** is now fully qualified by GREEN43364639355;
[cached platform/stress/package delivery](delivery/README.md). The original pending
state in the frozen validation describes its recording time. Later invoice
workload/catalog tests are excluded; production source remains unchanged.
