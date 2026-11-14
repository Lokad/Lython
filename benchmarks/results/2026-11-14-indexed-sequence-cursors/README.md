# Indexed builtin sequence cursors

2026-11-14, R346: committed runtime `3d78d429`/frozen `8306699f` versus
prototype `8ae1ae5f`. The existing cursor keeps the exact sealed list/tuple's
position directly. Its separate CLR enumerator and interface pull disappear;
ordinary executable storage shares the same live advancement kernel. No pool,
engine, memory policy or public API is added. Generic/acquired enumerators retain
precedence, and sticky exhaustion prevents append from reviving an ended cursor.

Retained pending full delivery. Focused Release checks pass 149 white-box and
1,013 public cases per actual .NET 11/10 (2,324 total). Coverage includes live
replacement/growth/shrink, aliases, Current/Dispose, cancellation, native/ordinary
transfer, admission refusal, awaited cleanup and budget reuse. The final public
case replaces list backing during genuinely asynchronous `max` key callbacks;
its exact output agrees with isolated CPython 3.13.2.

| Complete warm script | Allocated bytes | Elapsed | Wider process CPU |
| --- | ---: | ---: | ---: |
| CSV checksum, list input, 4,096 rows | −1.53% | −5.20..−3.30% | −4.58..−2.63% |
| Same CSV, text input, 4,096 rows | +0.69% | −0.11..+0.77% | +0.45..+1.74% |
| Captured text counter, unchanged control | about +16 B/job, effectively flat | −6.29..+0.21% | −6.51..+0.88% |

Percentages compare candidate with reference for every pair of fresh-process
medians; negative means less. These are Lython source comparisons, not CPython
ratios. The control's reference process spread is 4.85%, so neutrality is
uncertain. List process spreads are 1.92% candidate/0.08% reference; text
0.83%/0.05%. Retain the simpler representation and supported list gain with the
small text allocation/CPU adversity explicit. No general speedup is established.

B716 runs on the quiet Azure F4as_v7 Ubuntu 24.04.4 VM, ordinary .NET tiering/GC,
fixed B685 worker and optional adapter 0.4. Four fresh processes per script use
12-second complete-request warmup and five one-second measured batches. The
226.38-second collection stays inside its 300-second owned service. Warm timing
includes mediated RAM-backed I/O, golden verification and prior result cleanup;
it excludes compilation, startup, fixture reset/protocol and final cache shutdown.
Wider process CPU includes reset/protocol. No new fresh/compilation/CPython timing
was necessary for this representation change. Separate Windows allocation checks
use four processes with exact goldens and budget-baseline checks; their elapsed
clocks are not performance evidence.

All sources/cores, complete auxiliary hashes, raw observations, tools and checks
are in [the archive](sources-checks-and-observations.tar.xz).
[Timing](timing-summary.json), [decision](decision.json) and
[identity](candidate-identity.json) distinguish measurement from delivery.
Raw VM archive SHA256 is
`2489ff87b08a978e6dd7100315530abf86bdd1fbe7c4925500854ece44bb5b20`.
All workers join, guest fixtures disappear and persistent evidence is verified
under the lease. The first staging preflight failed before starting a service
because desktop-only allocation-helper files were absent from the VM manifest;
the corrected manifest verifies every actual VM auxiliary against both frozen
images. The failed manifest/payload survive. Before staging, four hardlinks to
two withdrawn B713 cores were retired only after canonical Git archive/binary
proof; current images, evidence and disk floors were preserved.

The reference's original full CI failed five Windows CRLF-sensitive expected
outputs; Ubuntu completed .NET 11/10, original stress and installed consumers.
Test-only correction `c9edb0ee` uses explicit LF expectations without normalizing
actual output. This candidate includes it and will batch full qualification.
The previous fully qualified runtime remains R341 until that delivery succeeds.
Publication and optional adapter promotion remain held.
