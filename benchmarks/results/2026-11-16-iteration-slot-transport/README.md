# Common iteration-slot transport: original prototype

R436/B833,2026-11-16UTC; isolated **c7e25f12** versus fully qualified
**6581454d /1c8e1be5**. Prototype not retained or fully qualified. One bounded
refinement selected; original source, measurements and adverse controls remain.

Existing exact-range cursor advancement now carries ExecutableSlot values into
ordinary/persistent and async ForNext as well as native helpers. Builtin sequences
retain canonical references; unknown protocols/large bounds retain the general
path. Remove the native-only integer-publication helper. No new owner, controller,
view, cache, representation policy or complete-kernel rewrite.

| Complete warm job | Elapsed candidate/reference | Wider CPU | Ref/candidate process-median spreads |
| --- | ---: | ---: | ---: |
| Integer module /Run | −24.69..−24.32% | −26.65..−23.82% | 0.34/0.15% |
| Integer module /RunAsync | −29.92..−27.23% | −31.38..−24.85% | 1.31/2.50% |
| Generator draining | −9.77..−6.59% | −10.34..−6.65% | 0.02/3.51% |
| Native helper control | +0.71..+4.72% | +1.10..+6.85% | 0.63/3.33% |
| Complete CSV validation control | +0.53..+2.93% | −1.11..+4.00% | 0.45/1.92% |

Four process pairings, not confidence intervals. Two processes/source,5s full-request
warmup/five rotated approximately0.5s rounds;171.119s/all image/golden/quiet/Closed/
exit/join/archive gates, IQR≤2.93%. Adverse native-helper CPU and CSV/control movement
prevent a broad neutrality/improvement claim. Source adds a helper call to native
range advancement and probes every ordinary cursor. Refine these boundaries once;
do not discard the original conditions or presume exact causal attribution.

Local whole-process natural-GC allocation: integer module−97.8%both APIs, generator
drain−13.7%, native helper/positional call/complete CSV/JSON effectively flat.
Accounting peaks unchanged. Later operand observation can still require a box;
this does not establish substantial gains for file scripts or a complete call/frame
replacement. Basic module-loop source/golden already existed. The explicitly named
native-helper control wraps the same checksum, using the independent CLR golden/N;
it is a diagnostic variant, not a new representative file workload.

446internal/1,990public focused checks pass eachactual.NET11/10; two additional
call-alias cases subsequently pass as part of all7new semantic cases each target,
without runtime/image changes. The original607-input freeze includes the first5
cases; later source/checks are preserved separately and qualification must use the
updated test source. Coverage includes identities, machine/large bounds, generator
yield, real I/O, cancellation/refusal, funded owners and joined cleanup. A private
allocation-driver host compile failure was corrected by reusing the existing
comparison host; no runtime change or data collected by the failed driver.

Same F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural
workstation GC/normal tiering. Warm includes full stdout/file goldens, prior-result
cleanup and mediated RAM I/O where used; excludes compilation/startup, reset/protocol
and final cache teardown. Wider CPU includes reset/protocol. No CPython/fresh/heap/
pending-I/O/embedding-tail measurement or across-source additive claim.

Evidence persists and was verified before RAM unmount; all users joined, terminal
cached. Only exact committed superseded ecd9 core/catalog and completed working
replicas retired after fresh lease/UID/path/hash/all-alias checks. New selected
catalog replaces old working catalogs; all originals remain in committed archives.
Current1c8e/c7images/toolchains and254/252MiB floors kept. Publication held.

[All controls](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[source/checks](source-and-checks.tar.xz),[image/procedures/added alias cases](procedure-and-image.tar.xz).
