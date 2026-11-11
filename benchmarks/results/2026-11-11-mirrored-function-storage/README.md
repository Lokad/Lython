# Compiled function storage — 2026-11-11

Retained `5f9059fa` versus fully qualified `b5849b04`. Every compiled function now
binds directly into its final compiler-sized workspace, including functions with
mirrored namespaces. This removes their second local array/copy and
`ArgumentsAreLocals` selection from the existing call/initializer runner.
Actual namespace copies, cells and lexical contexts remain; mirrors never borrow
reusable backing. No new owner, cache, option or execution route.

The affected complete CSV validator/report lazily defines its local record class
once per execution, then constructs ordinary records. Its helper has three
parameters and ten workspace slots. Five existing scripts remain unchanged
controls. This is a separate useful variant, not a replacement for the published
CPython scripts or evidence of improvement to captured text.

## Results and limits

CLR allocation falls **4.92% through both APIs**, about 532,600 bytes per complete
5,120-row script. Estimated charged peak falls only 112 bytes; compilation
allocation and unchanged controls are essentially flat. There is no major
retained-memory reduction claim.

| Quiet VM comparison | Elapsed change, all four process-median pairings | Limit |
| --- | ---: | --- |
| Local-record CSV / Run | −5.37..−0.83% | Coarse process CPU supports direction |
| Local-record CSV / RunAsync | −4.14..−0.95% | Coarse process CPU flat |
| Ordinary CSV control / Run | +0.97..+2.83% | Adverse; coarse CPU mixed |

Negative means faster; ranges are not confidence intervals. The original desktop
affected-sync **+7.98..+8.15%**, with 0.16% unchanged elapsed control, does not recur
on the quiet VM. Its cause remains unknown; all original results survive. Other
desktop controls and compilation also move adversely. Retain the simpler ownership
invariant and established allocation reduction with supporting affected-script
quiet direction. Accept ordinary-CSV control uncertainty as milestone debt to
reassess at a coherent checkpoint before publication. No universal CPU gain,
control-neutrality or genuinely pending-I/O performance claim.

## Measurement and acceptance

Windows source pair reuses the existing complete-job driver and fixed common
`9a4272de` worker/adapter images: three isolated core images sharing process GC,
256 warmups and five rotating rounds of sixteen complete jobs. Both APIs and
compilation are separate; desktop elapsed is diagnostic. Complete warm jobs
include mediated I/O, fixture reset, full output checks and result disposal.

B581 uses the existing quiet F4as_v7 Ubuntu/.NET 11 RC3 VM and fixed `fec79` worker:
four independent processes, normal GC/tiering, five-second warmups and seven
rotating 150 ms rounds. All three jobs finish in 87.498 seconds. Warm includes
mediated RAM I/O, full output checks and prior-result cleanup; compilation,
startup, reset, protocol and final caches are excluded. No clock merging or
CPython ratio. Both quiet gates pass; all users join and RAM fixtures disappear.
The persistent archive is verified under its lease and after retrieval. B581 is
terminal/cached. The qualified baseline is reused by a verified hardlink; no
remote files are deleted.

4,744 focused Release checks pass across actual .NET 11/10, plus an overlapping
initial 30 checks. Independent CPython 3.13.2 goldens cover the complete CSV
output/files and escaping class/closure values. Public regressions include
positional/variadic/keyword binding, compiled initializers, actual suspended reads,
cancellation, terminal refusal despite suppression/finally returns, retained
aliases after reuse and shared allowance returning to baseline after cleanup.
Full Windows/Ubuntu, stress and installed-package qualification is tracked
separately by delivery run 43042632656; focused checks do not establish it.

The contract deliberately admits the final capacity earlier. Larger idle
workspaces can stay charged in the existing execution cache until cleanup;
conservative earlier rejection is acceptable. Pending users and escaping values
remain funded through their actual cleanup.

## Preserved evidence

[Decision](decision.json), [local identity](diagnostic-identity.json),
[quiet summary](quiet/summary.json), [quiet raw archive](quiet/observations.tgz),
[exact measured source](sources-candidate.tar.xz) and both-target compressed cores
preserve the pair. All local phases, adverse data, loaded identities and controls
are retained. The measured binary has precommit `30b03` metadata; the retained
source differs only by a documented two-line comment correction after freezing.
[Delivery hashes](delivery-source-hashes.json) and the comment patch record it.

An initial metadata hash error is preserved: the identity gate refused before
launching measurement. The independently checked baseline image was already
correct; the metadata was corrected. The first CPython stdout comparison also
failed on Windows CRLF; normalized text then matched, while binary output files
were compared unchanged. Neither required a runtime or golden change.

This storage milestone does not complete the performance objective. CPython
tables remain explicitly tied to their earlier qualified source; regex work is
independent and publication remains postponed.
