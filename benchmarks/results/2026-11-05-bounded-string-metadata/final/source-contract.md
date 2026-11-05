# D48 bounded UTF8 scalar/UTF16 metadata counts

2026-11-05, before code. Retained source9c1d/code-identicalc2b85e03, frozen6cb05f8e.
D47 checkpoint closed440.59s/all gates; current full42029990429 running. No new
string representation/offset, fragment sharing, cache tier or native SIMD backend.

Demonstrated: initial PyString.Length and GetUtf16CodeUnitCount walk whole
strings without checking the active token. Native len routes through the sized
interface without supplying context. Projection checks before each value, then
walks that string before admission. These are nonallocating unbounded loops.
Performance magnitude is a hypothesis, not current word-summary CPU attribution.

For valid resident UTF8, scalar count=byte count−continuation byte count; UTF16
units add four-byte leads. Use .NET10/11 managed MemoryExtensions.CountAny and
SearchValues in<=4096-byte chunks, with current per-operation token/span. Tiny
immutable byte-class lookup metadata only, no per-operation arrays/reservations.
Remove local CountRunes/rune-width walk and four-way UTF16 scalar loop. Keep
existing cached scalar length, native backing/capacity/estimates/ownership.

Native len explicitly forwards its current token/span; property/helpers use
current ambient context where available, never a string owner's prior token.
Projection forwards its existing budget token, preserves first admission refusal
before new counting work and exact astral UTF16 preflight. No cached token or
new cancellation policy/counter/watchdog. Context-free metadata calls have no
host token; do not claim every internal caller gains actual interruption merely
from entry/exit tests. Bounded source checkpoints and in-flight evidence separate.

Short/Unicode costs and a second scan for UTF16 can regress; measure complete
ASCII/Unicode file scripts plus unchanged text/CSV controls, one<=180s job. Reuse
existing comparison workers/goldens/current roles, baseline6cb, no framework.
No implied CPU/alloc/peak reduction or fixed-percent acceptance veto.

Focused both actual targets: scalar-width extremes, supplementary text/empty/
cache and chunk boundaries, current caller token reuse, cancelled counts without
partial cache publication, original first refusal, projection widths/preflight,
retained storage and baseline reuse. Add no fuel/checkpoint-count architecture
tests, callback hooks, new stress campaign or relaxed timeout. Retain/refine/
withdraw the complete count family on evidence; full delivery at retention.

## Final correctness-only implementation before diagnostic
InitialB273 Unicode+306percent; refinedB274 Unicode+36.6/37.1percent and shortUnicode+13.7/6.9percent. Both complete performance algorithms withdrawn and preserved. Keep a shared scalar-width walk using existing GetRuneLength, bounded<=4096byte ranges, checked per-chunk totals, current native len/projection token forwarding and first refusal. No lookup metadata, BCL bulk-count strategy, ASCII special case, cache/layout change or speedup claim. One short three-workload diagnostic only catches gross regression; no continued refinement campaign. Full Release/platform/stress/installed consumers at coherent correctness delivery. Previous whole-performance objective remains open.
