# Final-slot range iteration: retained refinement

R436/B834,2026-11-16UTC; measured **0443b5cf** versus fully qualified
**6581454d /1c8e1be5**. Retained; full platform/package qualification pending.
[Original c7/B833 and adverse controls](../README.md) remain unchanged.

Ordinary/persistent and async ForNext now carry exact-range results directly in
existing executable slots, removing the intermediate boxed integer transport.
Native helpers retain direct cursor advancement; one redundant native publication
helper disappears. A computed exact-range fact avoids probing unrelated cursors.
Builtin sequences retain canonical references, general protocols and larger bounds
retain their exact paths. No new owner, ledger, policy, cache, controller or engine.
This closes one operand-transport boundary, not the broader call/frame redesign.

| Complete warm job | Elapsed change | Wider CPU change | Ref/candidate median spreads |
| --- | ---: | ---: | ---: |
| Integer module /Run | −25.55..−24.38% | −25.61..−23.14% | 0.81/0.76% |
| Integer module /RunAsync | −27.44..−27.03% | −27.08..−26.18% | 0.43/0.13% |
| Generator draining | −7.96..−6.05% | −8.30..−5.88% | 0.12/1.95% |
| Native helper control | −0.29..+0.91% | −1.90..+0.58% | 0.90/0.30% |
| Complete CSV validation control | −2.02..+5.12% | −1.74..+3.38% | 3.58/3.58% |

Four process pairings, not confidence intervals. Five jobs/171.940s, two processes
per source, unchanged four-case catalog,5s full-request warmup/five rotated roughly
0.5s rounds. All image/golden/quiet/Closed/exit/join/archive gates pass; IQR≤1.77%.
Native-helper result is approximately flat in this run, not causal proof that the
earlier adversity was solely the added helper. CSV is inconclusive; accept that
uncertainty without claiming broad file-script neutrality or retiming unchanged work.

Separate local natural-GC allocation: integer module−97.8%both APIs, generator
−13.7%; helper/positional-call/CSV/JSON effectively flat, all accounted peaks
unchanged. Later observation can still box.446internal/1,992public focused and
original14internal stress pass eachactual.NET11/10 after nonincremental Release
builds. Seven added public cases exercise call aliases, generator yield identity,
machine endpoints and real suspended I/O with cleanup/budget reuse. Existing checks
cover cancellation, refusal, protocol behavior and managed stack admission.

Quiet F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural
workstation GC/normal tiering. Warm includes stdout/file goldens, prior-result cleanup
and mediated RAM I/O where used; excludes compilation/startup, reset/protocol and
final cache teardown. Wider CPU includes reset/protocol. No fresh/CPython/heap/
pending-I/O/embedding-tail claim. Whole-script performance objective remains open.

Original staging headroom refusal preserved; stopped before mutation. Only exact
committed c7 image/raw working replicas retired after fresh lease/UID/path/hash/
all-alias proof. Baseline/current images, toolchains and254/252MiB floors preserved.
Raw archive persisted/verified before RAM teardown; no users remain. Terminal
cached.607frozen inputs include updated seven-case source. Publication held.

[Controls](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[source/checks](source-and-checks.tar.xz),[image/procedure/original refusal](procedure-and-image.tar.xz).
