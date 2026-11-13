# Native JSON byte input and conservative shared storage

2026-11-13. Final source frozenbff487b5/576production inputs. Native JSON byte input
accepts Python UTF8/16/32 detection, binary readers and ordinary custom decoders/hooks,
using the existing parser. Same-command immutable UTF8 borrows the existing window
representation; private source cleanup cannot refund a lent payload. Other owners
copy with admission. UTF16 reuses the shared codec; UTF32 writes admitted UTF8.
Decoder methods remain str-only; bytearray/lone-surrogate strings remain unsupported.
The displaced text-only stream readers/validator have no callers and are removed.

This removes a required guest decode call and duplicate payload charging. The old
full-array UTF8 decoder could already share its array; universal copy removal or
an allocation reduction is not demonstrated. No performance improvement is claimed.

Source checks: first prototype344public/33internal Release11;152public/33internal10;
three additional genuinely pending alias/cancel/refusal cases pass per11/10. Refined
5fc9ef6c passes124public/36internal11. Final arity wording passes45focused public11.
31independent CPython3.13.2 goldens run through both public APIs. Ownership cases
cover refusal before publication, borrowed backing after private cleanup, actual
pending users through cancellation/refusal, retained results and baseline reuse.
Full final Windows/Ubuntu11/10, stress and installed-consumer qualification is pending.

## Measurements and decision

All comparisons use the same fixed B685 worker/optional0.4 adapter against qualified
976f9439/frozen97bb and CPython3.13.16 on the existing quiet F4as_v7 VM. ZIP/config
intentionally changes the script from explicit UTF8 decode to native bytes; this is
an API/workflow comparison, not same-script runtime-only attribution. The text-input
JSON aggregation control is unchanged. Compilation/startup/reset/protocol are
excluded from warm guest timing; mediated RAM I/O, complete stdout/file checking
and prior-result cleanup are included. Wider process CPU includes reset/protocol.
Each job has two processes per engine/source and five rotating rounds. All three
collections pass image/golden/quiet/join/persistent-archive gates and stay below
their180s owners. No allocation stacks or embedding tail latency are measured.

| Collection | Candidate source | Batches / total collection | ZIP elapsed change | Unchanged JSON elapsed / wider CPU |
| --- | --- | --- | --- | --- |
| [B687 original](first-comparison/README.md) |b7306a8f|250ms /87.369s|−1.13..+0.44%|+1.00..+6.20% /+2.56..+6.15% |
| B688 cold-path refinement |5fc9ef6c|250ms /87.125s|−0.89..−0.47%|−1.07..+3.26% /+1.11..+5.88% |
| B689 same-binary confirmation |5fc9ef6c|1s /140.477s|−2.54..+2.69%|−2.97..+4.10% /−3.46..+5.28% |

B687 identifies an enlarged str helper and extra execution check; refinement moves
cold byte decoding out and removes that duplicate check. B688 still has moving
candidate controls and coarse10ms process-CPU ticks. B689 uses longer batches,
retaining the same binaries, warmups, scripts, rotation and180s owner. Its JSON
process spreads are3.26%candidate/3.74%reference/0.50%CPython. Small regression debt
therefore remains uncertain; the original adversity is not declared disproved.
Retain the bounded compatibility/storage improvement, with no CPU gain claim.
Do not retime unchanged images without a new attribution or observability reason.
This is a stopping criterion for this iteration, not a permanent design rejection.

The finalbff source differs from timed5fc only in arity diagnostic text (str or bytes).
Its exact sources/binary and the timed refinement are preserved separately; final
source timing is not inferred. These measurements do not replace the qualified
representative CPython table or close the remaining whole-script performance goal.

Initial development also caught a missing fixture-copy declaration, stale frontend
str/binary-stream validation and a text-only reader assertion. They are corrected;
the initial local logs were overwritten during development, so no original full-log
claim is made. All original performance conditions/raw observations are retained.
Publication remains postponed.
