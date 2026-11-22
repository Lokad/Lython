# Cursor cancellation on the frozen 0.5.0 development producer

The Windows .NET 10 and .NET 11 probes load the same immutable
PythonRe 0.5.0 packages from `61228b20f` as the earlier boundary
checks. Both loaded library hashes agree with the package manifest.
The released adapter stays on 0.4.0.
[Producer and package identity](../2026-11-22-regex-050-frozen-boundary/README.md).

A named-capture cursor over a 32,769-byte subject makes a working
admission during construction and another on advance. Cancelling
from either admission stops the operation and returns the entire
working allowance. The advance test uses an independent token with
default creation cancellation. These checks establish interruption
during admitted work. They leave exact matching-loop interruption
for final adapter qualification.

The producer keeps the creation token active during later advances.
Cancelling it after construction cancels a subsequent advance whose
own token remains live. A temporary adapter-local forwarding
registration can detach the creation caller when construction ends.
With that bridge, the same late cancellation allows the later advance
to return its named match and last-close index, and EOF refunds scratch.

Keep the bridge at the optional adapter boundary. The current producer
API cannot accept the construction caller directly and restrict its
authority to construction. A construction-scoped token would let the
adapter use the producer's per-advance token without a retained
forwarding source. The probe makes no timing or released-package claim.

[Probe and outputs](procedures.tar.xz).

On the released 0.4.0 adapter, a separate regression reproduced
cancelled creation surfacing the adapter's private token. The repair
at `6ff90dfd2` maps the interruption to the caller's token and
checks it again after detaching the registration, before handing
off the cursor. The focused test checks the caller token, refunded
working allowance and successful use of the same prepared subject.
Windows .NET 11 and Ubuntu .NET 10 each pass 715 focused Release
checks, including the existing in-flight backend cancellation tests.
