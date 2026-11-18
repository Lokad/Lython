# Prepared compiler names: measured footprint withdrawn

R525/B956, 2026-11-18 UTC. Fully qualified **3f873227 / a5038a28** reference
versus private **f96cd8a9**. Thirteen production files consolidate name text,
live-module location and existing String/Dict method definitions. Separate slot
backing and repeated immediate method-name selection disappear; cased-split
preparation reuses the same definition. Exact module/receiver guards, generic
attribute values, protocols, async operations, cancellation and owners remain.
No runtime cache, new policy, dispatch framework or guest-value/token cache.

The one-pool estimate grows from roughly12to32bytes/name (headers differ).
Local synchronous compile-plus-disposal allocation increases **0.161%** for calls,
**0.792%** for helper text (+960B), **0.279%** for JSON helpers and **0.352%** for
floating; all borrowed-budget baselines restore. These are allocation diagnostics,
not CPU measurements. Run allocation is effectively flat and accounted peaks are
unchanged across13existing cases.

Six quiet-VM jobs complete in277.94seconds, unchanged self-controls,8second rotated
warmups and defaultGC/tiering. Ranges compare process medians, not confidence
intervals; negative means candidate lower. No causal subtraction between phases.

| Whole script / phase | Elapsed change | CPU change | Control spread reference/candidate |
| --- | --- | --- | --- |
| Helper text / compile | −0.21..+1.70% | −1.56..+1.33% | 0.11/1.81% |
| Helper text / warm | −1.46..+0.43% | −1.90..+0.24% | 1.03/0.89% |
| Helper text / compile-and-run | +0.44..+1.86% | +0.53..+1.05% | 0.61/0.79% |
| JSON helpers / warm | +0.98..+2.11% | −0.54..+3.26% | 0.81/0.30% |
| Floating JSON / warm | −2.02..+1.51% | −3.16..+2.48% | 2.75/0.82% |
| Positional calls / warm | −1.04..+0.71% | −1.22..+0.82% | 1.19/0.57% |

Warm includes mediated RAM I/O, output checks and prior-result cleanup; fresh
adds compilation and compiled-owner disposal. Compile is separate. Startup,
fixture reset, protocol and final cache teardown are outside guest clocks;
external process CPU is wider. No new CPython ratio or universal speedup claim.

Both actual.NET11/10 pass233internal/86public focused and separate63internal/
150public namespace checks (overlaps not added), including cancellation, refusal,
aliases, live globals, classes, imports and actual suspended cleanup. Actual copied
32-file images pass13existing goldens/allocation/reuse and both five-case combined
catalog checks. Quiet/hash/golden/Closed/join/fixture-removal/SHAarchive gates pass.
Only exact committed withdrawn028core (both aliases), B954raw/whole and B952raw
replicas retire after fresh proof; canonical evidence/current images/floors remain.

**Decision: withdraw this footprint.** Removing one parallel array and runtime
selection does not justify wider metadata and cross-cutting compiler/runtime
changes on these observations. Useful semantic regressions remain. This is not a
fixed-percentage veto, proof that prepared names cannot help, or permanent ban.
No unchanged confirmation campaign: text benefit is unconfirmed, fresh/text-record
debt remains, and the broader objective needs a more consequential deletion target.

All2390measured inputs were verified before restoring13production files and the
name-representation assertion. Private tree is clean; main production remains
qualified3f. Four mixed-global/member, bound-value/override, string/bytes and
missing-member error-order cases remain. A local build overlapped their source
copy (8versus12cases); those observations are preserved and not accepted. A settled
nonincremental rebuild then passes **12receiver cases on each actual framework**.
No unchanged full-platform replay or dormant alternative. Frontend phase attribution
continues separately; publication and the wider performance objective remain open.

[Source/image identity](frozen-identity.json), [prototype](prototype.patch.gz),
[source](candidate-sources.tar.xz), [timings](summary.json),
[raw](observations.tgz), [compile allocation](compile-allocation-summary.json),
[run allocation](allocation-summary.json), [checks/procedures](procedures-and-checks.tar.xz),
[restored checks](restored-checks.tar.xz), [custody](archive-receipt.json).
