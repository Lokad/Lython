# Native UTF-8 tab expansion without repeated column sizing

R463/B870,2026-11-17UTC. Fully qualified production46343804 / aaef443f versus
private ae389c19,now promoted with identical production text. One production file
changes; new edge tests are separate from the immutable measured snapshot. Prior
[full delivery43989114633](../2026-11-17-sequence-construction/delivery/README.md)
qualifies the reference. [Full qualification passes as integrated into R464](../2026-11-17-receiver-calls/delivery/README.md);
this does not claim a separate full run of standaloneae389.
Publication remains held.

Tab bytes determine conservative final capacity; Unicode column counting and
space generation run once instead of being repeated for sizing and writing.
Existing final storage/preflights/rollback/cleanup remain; unchanged input can
reuse its funded alias. An upper bound exceeding Array.MaxLength triggers exact
sizing BEFORE admission,so valid representation decisions remain exact. Admission
refusal is never retried with a smaller charge. Existing exact sizing remains only
for this representation case,not as a silent execution fallback.

| Complete script / boundary | Elapsed change | Wider CPU change | Reference/candidate control movement |
| --- | ---: | ---: | ---: |
| Normalization / warm Run | −7.71..−7.17% | −9.31..−7.09% | 0.13 / 0.46% |
| Normalization / compile and Run once | −11.40..−6.79% | −12.10..−5.64% | 1.64 / 3.49% |
| Ordinary calls / warm Run | −1.03..+0.46% | −1.61..+1.60% | 1.02 / 0.48% |
| CSV invoice / warm Run | −1.04..+0.53% | −1.39..0.00% | 1.31 / 0.27% |

Four pairings of two process medians/source,ranges not confidence intervals.
Warm affected-script improvement has close controls and supporting CPU. Fresh
also favors the candidate,but its3.49%candidate control movement widens magnitude
uncertainty. Unchanged call/CSV pairings straddle zero. Retain the complete pass
removal with this measured gain and explicit storage tradeoff; no universal gain,
new CPython ratio or cross-collection causal conclusion. Earlier rejected native
blocks and shared kernels remain withdrawn; this is a different displaced stage.

Normalization physical allocation +0.27%,accounted peak2,324,834→2,328,930B(+4KiB).
The whole conservative capacity,including its spare tail,stays funded while the
result is retained. Supplied allowances can deny earlier; no hidden default cap,
new policy,owner,ledger or reclamation mechanism. All nine allocation diagnostics
and their original local first-use/compile observations remain preserved; those
local elapsed clocks are diagnostic and do not qualify CPU gains. Compiled-budget
baseline is restored after each run and zero after disposal.

Nonincremental measured build clean; both immutable worker images pass all nine
supervised local CPython goldens. Initial actualWindows Release.NET11/10 each
136internal/215public pass. Promoted source each145internal/215public pass,including
new width-one,empty/all-tab,negative-width and unchanged non-BMP/large-width cases.
Existing source-chunk boundaries,Unicode columns,CR/LF,active cancellation,terminal
refusal before proportional construction,retained results and budget reuse stay
covered. All585production source texts match the frozen candidate; no rebuilt
binary equality is inferred. Focused checks do not claim full platform coverage.

Quiet F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural
workstation GC/normal tiering. Four jobs123.695s;four-second full-request warmups,
five rotated approximately0.5s samples/process. Existing fixed nine-case catalog
and worker/protocol; images differ only in the core. Warm includes Run,mediated
RAM I/O,all stdout/file goldens and prior-result cleanup; excludes compilation/
startup,reset/protocol and final cache teardown. Fresh adds compilation and
compiled-owner disposal; process CPU is wider. No pending-I/O,RSS or embedding-
tail claim. All identities/goldens/quiet/Closed/exit/join/archive gates pass.

Every user joined; evidence SHA-verified persistently under lease before RAM
teardown. Only the exact committed superseded8ddcore and B866working raw JSON
replica were retired after fresh UID/path/hash/nlink/all-inode-alias proof.
Selected images/toolchains/canonicals and254/252MiB stage/final floors preserved.
Terminal cached permanently; no unchanged retiming pending. Continue the broader
runtime roadmap after this retained milestone.

[Timing](summary.json),[allocation](allocation-summary.json),[validation](validation.json),
[source/image identity](identity.json),[source](candidate-sources.tar.xz),
[measured core](measured-core.tar.xz),[raw observations](observations.tgz),
[focused checks](focused-checks.tgz),[procedures/custody](procedures.tar.xz).
