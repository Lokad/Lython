# Emission order for packed executable blocks

A Windows .NET 11 RC3 structural probe records instruction appends
before the compiler's forwarding and slot layout. Both probe variants
reproduce all 79 inspected code objects across 11 scripts. The probe
starts from private `f069dc7ab`. It adds diagnostics, and the second
variant emits protected-clause exit instructions earlier and patches their
targets after the labels are known. Neither image is timed.

| Fixture | Per-block capacity slots | Geometric packed slots, existing order | Geometric packed slots, early exits |
| --- | ---: | ---: | ---: |
| Generated validator, 485 blocks | 5,820 | 8,256 | 4,160 |
| Postponed factory | 84 | 92 | 92 |
| Dead-heavy control | 2,072 | 2,064 | 2,064 |
| Nested finally | 80 | 80 | 80 |

The existing validator emission reopens 192 nonempty blocks. A packed
append-at-tail buffer would copy 1,152 instructions and raise capacity.
Emitting and patching the same protected exits removes those reopens.
With that order, the replay predicts 28.5% fewer temporary capacity
slots for this fixture. The postponed factory may need more slots.
These are structural counts. They exclude buffer growth overlap,
metadata, admission and allocator effects, and establish no CPU gain.

Use early exits when evaluating a shared instruction backing. Keep
one block representation, allow general reopening with amortized
bounded copying, and keep the existing block address order, protected
ranges and final tape semantics. The probe's source, initial failed
reorder build and all emission traces are preserved. A real candidate
needs focused semantics, admission/refund checks and comparative
compilation and time-to-result evidence.

[Shape estimates](summary.json) · [Sources and observations](observations.tar.xz).
