# One class-pattern extraction phase — 2026-11-06

R06 removes PyType.TryGetMatchArgs, the complete per-match name array/whole-tuple
decoding, URL duplicate HashSet and three interleaved extraction routes. One
descriptor-aware extraction phase retains attributes in admitted compile-sized
scratch before child patterns. Only selected names are decoded; their temporary
funding ends after references clear. Namedtuple metadata reuses its field tuple.
Value patterns use existing contextual equality/truth. No new dispatcher/cache.

[Source identity](identity.json), [contract](contract.md), [validation](validation.json),
[build/focused logs](focused-checks.tgz), [full local.NET11](full-net11.log).
Sixteen isolated CPython cases pass on actual.NET11RC3/.NET10.0.8, including the
[original nine disagreements](source-cases.json), descriptor metadata, duplicate
and malformed-name effect order, AttributeError distinction, mutation snapshots,
object root, named records, reflected equality and truth. Original failed probe
outputs remain alongside corrected outputs. Public goldens run both APIs.

Focused3,057 public/320 internal pass per actual runtime; full local.NET11 passes
4,239 internal/16,160 public. New native controls make cancellation/refusal end
the command even when a descriptor throws AttributeError or equality returns;
later effects/captures stop and shared allowance recovers after cleanup. Name and
frame storage refuse before the corresponding guest effects. A genuinely suspended
body keeps the extracted alias funded after subject mutation, then cancels and
awaits cleanup to the compilation baseline and finally zero.

## B339 complete affected script

Source ddbda0cf plus exact recorded R06 patch/hashes, frozen **f940bd54**; finalR07
reference4ace68ff/corea45b5c5d. Unchanged b94fa98c worker/protocol2 and ordinary
.NET11RC3/CPython3.13.16 GC/tiering on the quiet F4as_v7 VM. Same4,096-record CSV
fixture, full output/file goldens, two identical current processes, reference and
CPython; five-second warmup/role/job, seven rotated150ms rounds. **86.64s /3jobs**,
all quiet/identity/Closed/funding/join/fixture-removal gates pass. Raw archive
locally hash-verified; no live worker remains.

| Whole script | Candidate / identical control | Final R07 reference | Changes |
| --- | ---: | ---: | ---: |
| CSV class records, warm |9.714 /9.684ms|9.104ms|+6.71 /+6.37%|
| CSV class records, compile-to-result |9.938 /10.007ms|9.613ms|+3.38 /+4.09%|
| Unchanged original CSV, warm |4.816 /4.807ms|4.708ms|+2.29 /+2.12%|

Class has three ordinary metadata fields; matching selects two. No wide unused
synthetic tuple. Current-role spread0.31% warm/0.69% fresh; current IQR0.72%/2.15%.
The unchanged CSV control also worsens; it does not explain the entire affected
gap. [Every paired result](assessment.json), [all medians/IQRs](summary.json),
[raw evidence](observations.tgz), [hash/join receipt](archive-receipt.json).

**Decision:** retain the corrected Python behavior and common extraction, with
the above regression debt explicit. This removes copying and duplicate routes
but establishes no performance improvement. Descriptor semantics and conservative
admission now add real work the old incorrect route omitted. Timing does not
attribute the gap to any one mechanism. A further change must identify a common
metadata/member cost to eliminate; do not restore raw lookup or a class-only
semantic bypass merely to improve this table. No allocation percentage, causal
CPU, RSS or primary-workload claim. The whole-script objective remains open.

Timing includes mediated RAM I/O, complete validation and prior-result cleanup;
fresh includes compilation/disposal. Startup, fixture staging/reset, transport
and final teardown are outside. Actual async body/cleanup behavior is separately
tested. Pattern hooks themselves still require synchronous completion, including
under RunAsync; this milestone does not claim genuinely asynchronous pattern hooks.

Final platform, originalStress and installed-package qualification batch at
delivery. R07's earlier receipt excludes this source. Publication stays postponed.

Committed **a2a61290**. Both actual [installed package consumers](package-consumer.log)
pass; [validation](validation.json) records the exact package hash. Delivery
CI42191963552 is running independently; latest observation both platforms have
passed Release11/10 and package gates and are executing originalStress. Later
constructor work is excluded. No unchanged local suite replay.
