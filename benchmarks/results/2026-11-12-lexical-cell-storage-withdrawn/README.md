# Lexical-cell storage prototype withdrawn — R284

2026-11-12. Qualified production **9bca1337**, frozen core **e4cc65b2**;
prototype over documentation commit632a867c, frozen core **c8bca168**. Not a
measurement of subsequent HEAD. No retained production change.

The prototype replaces each cell's value/owner/name with value-or-namespace/name,
explicitly promoting live cells when namespace mirroring starts. Cell construction
measures48→40B, but complete-script allocation is effectively flat (≤0.04%). It
adds promotion and live-storage checks, including suspended-frame handling.

**Withdraw it.** CSV Run is slower with supporting CPU; captured text supplies no
reliable whole-script benefit. The smaller cell does not justify the added
promotion machinery and measured cost. This is a source/measurement decision,
not a fixed percentage veto, proof of one CPU cause, or permanent design ban.
The broader whole-script performance objective remains open.

| B641 complete script/API | Elapsed change | Process CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| Captured text Run | −2.39..+4.19% | −2.51..+3.94% | 5.11/1.29% |
| Captured text RunAsync | −0.29..+4.15% | +0.30..+6.87% | 2.74/1.56% |
| CSV Run | +1.28..+3.46% | +1.70..+4.98% | 0.28/1.83% |
| UTF8 report Run | −1.40..+2.01% | −3.18..+0.69% | 0.92/2.51% |
| UTF8 report RunAsync | −2.62..+4.49% | −1.22..+4.43% | 3.59/3.45% |

Negative means faster than baseline. Ranges include all four candidate/reference
process-median combinations, rather than selecting one favorable pair. These are
within-Lython comparisons, not CPython ratios. Existing quiet Azure F4as_v7 VM,
Ubuntu/.NET11RC3, normal tiering/GC, fixed comparison worker and unchanged input
catalog. Five jobs finish in275.929s under a360s owner plus30s stop/30s margin.
Existing matching/validation, mediated RAM-file I/O and previous-result cleanup
remain inside the warm boundary; startup, fixture reset, protocol and final cache
cleanup are outside. Full identity/golden/balance/quiet/worker-exit/join/unmount/
archive gates pass. SHA-verified retrieval and terminal state are cached.

All original desktop data survives: favorable captured Run elapsed−7.76..−11.16%
with3.83% unchanged-control spread, adverse CSV Run+4.93..+5.02% with0.09% spread,
variable async/report and adverse compilation despite unchanged compiler source.
Those observations were diagnostic; B641 does not erase them. No unchanged retime
is required for this withdrawal. All failed setup conditions, source patches,
frozen source/core, constructor allocation observations and raw data are archived.

Candidate focused checks cover1128 internal/6030 public per actual.NET11/10
(14316 unique target checks, including separately added alias cases). Seven
complete scripts pass both APIs plus compilation/fresh execution and ownership
goldens. A new independent CPython-backed class/nonlocal/generator-close alias
regression remains:2public cases. All five production and seven fixture files
are restored;568 production sources match qualified code with explicit newline
normalization. A forced nonincremental Release solution build passes, then158
internal/61public focused checks per target pass, including the retained alias
regression. No dormant cell-promotion hooks remain. No repeated full qualification
of the unchanged production runtime is claimed or needed.

Before staging, a verified idle-lease hard-link deduplication replaces both links
to an identical e4cc65b2 core inode with the existing canonical inode. Every old
path and byte survives; all aliases, ownership, device and hashes were checked.
Free space rises274518016→280207360B without changing OS/evidence/staging floors.
Preparation and proofs survive in diagnostics. B641 ends with268345344B free,
no live owned workers and no guest RAM mount. No archive or image was deleted.

[Raw summary](summary.json), [VM observations](observations.tgz),
[source/restoration and desktop diagnostics](diagnostics.tar.xz),
[restoration proof](restoration.json). The exact withdrawn patch is in diagnostics.
The current runtime guide continues to describe qualified R281. Publication held.
