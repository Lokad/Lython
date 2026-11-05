# D27 correction and completed JSON string scanning design, before reader code

2026-11-05. Preserve original B243 encoder source/plan/binaries/conditions and
observations. Its profile attribution incorrectly called ReadJsonString's6.13%
leaf share emission. The report does not invoke the encoder; JSON summary emits
only two keys. Its six-case pair principally observes unaffected controls, not
the proposed representative encoder improvement. This is a source/measurement
coverage correction, not a failed design or permission to discard observations.
Do not repeat B243 as an encoder CPU claim. B243 remains live and owns its cleanup;
the next collection must wait for verified terminal/archived state and free lease.

Concrete selected target: ReadJsonString still scans every native token byte to
find quotes, escapes and bare controls, then separately validates UTF8 as needed.
Use managed SearchValues<byte> bounded span searches for its ordinary runs too.
This removes the executed per-byte boundary branch in the existing scanner; keep
the same escaped/closed/strictControl/control-count state, exact token boundary,
lenient rewrite, backend errors, UTF8 validation, name reuse and final-copy owner.
No alternative decoder, token tape, collection builder, option, new owner or cache.
The encoder uses its own grammar-appropriate immutable printable-ASCII search;
the reader permits native high bytes and validates them at its existing funnel.
Never skip malformed UTF8 checks in either direction. Read windows <=1024bytes,
write windows <=4096; escape skipping/scalars may cross one boundary, and existing
active cancellation/first-refusal/awaited cleanup remains on the same paths.

A completed source pair tests both reader/encoder run scans against the ORIGINAL
cf3dc9cd/43fdf658, not a gain added to B243. Same six complete report warm/async/
fresh, JSON warm, text warm and report compile controls, <=240s owned service,
6s normal-runtime guest warmups,2ssettle/9rotated200ms. Exact source/DLLs/independent
CPython goldens/quiet endpoints/balances and owned joins required. Report and JSON
exercise the reader strongly; no representative encoder magnitude can be inferred
from report. Allocation expected flat. Preserve original adverse/uncertain data.
Focused original JSON/token/escape/admission/cancellation/retention tests on both
actual runtimes, add only boundary/cancellation gaps. Stop retain/debt/withdraw;
full delivery only after retained coherent source. Whole-script objective open.
