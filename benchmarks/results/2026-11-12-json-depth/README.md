# Iterative JSON decoding without an inherited backend ceiling

R269, 2026-11-12. Source baseline fcaca860 (qualified runtime da9e5066); only
Json.Construction and Json.Deserialize change. Remove the old 64-container
policy and its backend error/coordinate builder. The existing iterative frame
array remains the owner. Growth preserves checked array representation bounds,
capacity/replacement-overlap admission, cancellation before allocation, and
funding through pending callbacks and awaited cleanup. No new option, budget,
tracing or reclamation mechanism. Encoding, projection and guest callback nesting
protections stay unchanged. This is a contract simplification, not a speedup claim.

Focused actual .NET 11/10 checks pass: 20 internal +40 public per target, 120
total. Updated raw-decode depths 65/257 additionally pass both targets after the
final rebuild. Coverage includes decoded leaves above the old cap, child-before-
parent object hooks, raw scalar offsets, shared-budget reuse, two executions
while callbacks are pending, retained aliases/results, cancellation and refusal
before callbacks. Substantial frame-replacement interruption releases scratch
and preserves the first refused-admission origin. CPython 3.13.2 independently
confirms raw prefixes at depths64/65/257. Release build has zero warnings/errors;
initial shadowed local-name compilation error remains in build.log.

One short diagnostic reuses the complete-job driver: JSON-lines and report
processing, native-search control; three isolated core images sharing CoreLib,
normal GC/tiering,16warmups and5rotated rounds of8jobs. All independent stdout/file
goldens, loaded-image identities, shared allowance and cleanup checks pass for
both APIs and separate compilation. Precise allocation is essentially unchanged.

| Complete script / phase | Diagnostic elapsed change | Unchanged control spread |
| --- | --- | --- |
| JSON-lines / Run | +0.28..+2.95% | 2.60% |
| Report / Run | −2.75..−2.06% | 0.70% |
| Native search / Run | −1.92..+0.55% | 2.52% |
| JSON-lines / RunAsync | +1.76..+10.62% | 8.71% |
| Report / RunAsync | −1.88..+1.13% | 3.06% |
| Native search / RunAsync | +4.31..+5.83% | 1.46% |

Native async is an adverse unaffected-control result and remains unresolved; do
not attribute it to JSON or erase it. Compilation is mixed with moving controls.
No general CPU gain/equivalence, new CPython ratio, RSS or latency qualification.
Clocks include fixture reset, mediated I/O, complete output checks and immediate
result disposal; startup/compilation are excluded from warm phases. Run-only
clocks and wider process CPU remain in raw observations. This desktop diagnostic
is not a quiet-VM performance confirmation.

The unchanged-allocation outcome is expected: ordinary admitted documents still
construct the same storage. The justified retention is removal of an obsolete
policy and acceptance of deeper admitted input. Final platform/stress/installed
Release consumers will batch at the combined delivery boundary; not yet qualified.
