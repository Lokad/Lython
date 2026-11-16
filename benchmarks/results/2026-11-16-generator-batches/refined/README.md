# Buffered yields inside the existing engine: correction pending

R458 refined4fcfcc71 versus qualified9e8d1cf5/e92d9598,2026-11-16UTC.
**Private, not promoted.** Single-item Advance/AdvanceAsync are restored; the existing
instruction loop consumes private yields into admitted list capacity. One borrowed
destination/limit ends before observation, growth or cleanup. Pending disposal
returns through the existing awaited path. No second interpreter/owner/policy.

| Complete workload | Phase | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Normalization,4,096lines/52,224B | Warm | −14.83..−11.57% | −14.60..−10.68% |
| Same normalization | Compile and run once | −10.47..−7.71% | −10.92..−8.01% |
| Ordinary generator/list/render control,16,384values | Warm | −2.98..+2.52% | −3.08..+2.47% |
| Numeric report,4,096records | Warm | +5.34..+13.33% | +6.84..+14.13% |
| CSV invoice,4,096rows | Warm | +0.30..+1.71% | +0.56..+2.62% |

All four process-median pairings retained; negative means faster. Numeric reference
moves5.82%, but all elapsed/CPU pairings are adverse. Generator candidate moves4.59%;
normalization reference/candidate0.07/3.75%. Allocation adds only32B per affected
invocation (<0.006%), accounted peaks and compiled allowance unchanged. Neither
absence of regression nor a universal CPU gain is established.

Source still calls TryBufferYield for ordinary yields with an inactive destination.
A bounded correction will guard that cold helper at its callers, preserving the
single-item engine and borrowed bulk destination. No further timing or production
promotion is claimed here. Original99e placement and its longer-warmup control
adversity remain in [the parent](../README.md). No fixed-percentage veto or permanent
ban; a useful final design must decide its remaining representative regression debt.

B864 five jobs193.464s: quietF4as_v7 Ubuntu24.04.4,.NET11RC3 normal tiering/natural
workstation GC,two processes/source,6s full-request warmup and five rotated~0.5s
samples/process. Warm includes Run, mediated RAM I/O, full goldens and prior-result
cleanup; excludes compile/startup/reset/protocol/final caches. Fresh includes compile
and compiled-owner disposal; CPU clocks are wider. All identity/quiet/Closed/exit/
join/archive gates pass. Persistent archive SHA-verified before RAM teardown; no
users remain and terminal cached. No fresh CPython/RSS/embedding-tail claim.

Original B863 launcher had a stale B860 root and exited before benchmark workers,
producing no timing. Its trap changed B860's completion-marker file; that marker
was restored to zero from the immutable cached original terminal. Original scripts,
journal, failure and correction remain archived. B864 reuses the same verified
working image/root with the corrected launcher; source and measurement protocol
remain frozen. Only exactly proved committed replicas were retired; selected images,
toolchains and canonical evidence preserved. Staging254MiB/final252MiB floors hold.

Focused actual Windows.NET11/10 each166public/59internal pass, plus seven final
semantic/refusal rows each and183additional public.NET11 cases (overlap not summed).
Seven frozen worker/CPython goldens and five independent semantic programs pass.
The diagnostic extractor initially included the separate refusal fixture and made
an incorrect CPython return assertion; the scoped extractor correction and original
failure are preserved. No runtime test failed or assertion/budget was relaxed.
Full platform/stress/installed-package qualification waits for a retained design.

[Results](summary.json),[allocation](allocation-summary.json),[validation](validation.json),
[frozen inputs/images](identity.json),[source](candidate-sources.tar.xz),
[raw evidence](observations.tgz),[focused checks](focused-checks.tgz),
[original failure/correction/procedures](procedures.tgz),
[exact measured prototype cores](measured-cores.tar.xz).
The core archive preserves original477e, initial99e and refined4fc bytes against their
recorded SHA256 values; these are unpublished benchmark snapshots, not release packages.
