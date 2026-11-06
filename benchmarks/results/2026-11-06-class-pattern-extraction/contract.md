# R06 — one class-pattern extraction phase

Source hypothesis: PyType.TryGetMatchArgs copies/decode-validates every metadata
name per match; three matcher branches duplicate interleaved extraction and
subpattern testing. URL matching also allocates a duplicate HashSet. Ordinary
record scripts use only a subset of class metadata. This is demonstrated source
work, not proof it dominates representative performance.

Remove TryGetMatchArgs, the complete string array/whole-tuple decode, URL HashSet
and three extraction routes. Reuse actual instance/type checks and descriptor-aware
attribute lookup. Compile-sized scratch stores extracted values and selected
names before child patterns; temporary CLR names have one conservative capacity
reservation and references clear before refund. Namedtuple field metadata is
exposed as __match_args__ without another copy/owner. Value patterns use existing
contextual equality/truth; no new dispatcher, cache, fallback or configuration.

Contracts: all attributes before child tests; only requested names validated,
ordered getters before later duplicate/type errors; extraction AttributeError is
mismatch but child AttributeError propagates. Actual object root, aliases,
named records, builtin match-self and OR/private captures remain correct.
First refused admission and cancellation survive descriptor errors or finally
returns; no later effects, and scratch refunds only after actual use. Working
preflight may reject earlier/more conservatively; tiny thresholds not preserved.
No new async matcher: pattern hooks still require synchronous completion; actual
async body/cleanup and funded aliases continue through awaited operations.

Focused semantic/ownership/cancellation checks both actual runtimes, original
9 failing CPython probes plus7 independent cases, delayed-body alias/cleanup.
No exact checkpoint-count tests or timeout relaxation. Full platform/stress/package
qualification batches at delivery, previous4ace68ff receipt excludes this source.

Bounded comparison B339: existing worker b94/protocol2, quiet VM, three complete
jobs (CSV class-record warm/fresh and unchanged original CSV warm control),
two identical current roles, finalR07 reference and CPython. Same4096-record
CSV fixture/full output and file digests. Class has three metadata fields; match
uses two, an ordinary subset rather than a wide unused synthetic tuple. Five
warmup seconds/role, seven rotated150ms rounds,130s owned service. Clocks include
mediated RAM I/O/full validation/prior-result cleanup, fresh includes compilation;
startup/staging/reset/transport/final teardown out. Ordinary GC/tiering.
Preserve all raw/adverse/failed conditions, Loaded Ready/core identity and joined
users/fixture cleanup. No fixed improvement veto. Decide any meaningful debt at
this milestone; corrected Python behavior need not demonstrate a speedup. No
allocation percentage, broad CPU/RSS or primary-checkpoint refresh implied.
