# Closed unary builtin result transport

R477/B889,2026-11-17UTC. Qualified abcbd9fd/frozenca1a8239 versus private
seven-file prototype/frozen0315de0a. **Retain for simplification and allocation; full delivery pending.**
One existing ExecutableSlot result convention removes the Delegate/two-signature
bridge, generic invocation switch, native-only integer repacking and fresh scalar
boxing at ordinary int/len/float boundaries. No new owner, cache, enum registry,
backend or tagged-value storage. Parsing/grammar and managed admission unchanged;
abs/str/sum(bytes) retain existing object/alias behavior. Net31added/34removed.

The new complete numeric CSV report parses4096records/127031B through module
and helper float conversions, writes an exact summary and prints count. Input is
the unchanged integer-invoice fixture. Isolated CPython3.13.2generates independent
stdout/file goldens; the existing comparison protocol runs all three cases.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate control spread |
| --- | --- | ---: | ---: | ---: |
| Numeric CSV quality report | Warm | −1.58..+1.92% | −2.79..+0.84% | 2.64 / 0.89% |
| Same | Compile and execute once | −11.02..−4.89% | −12.07..−5.64% | **6.04** / 0.80% |
| Original integer CSV | Warm | −2.52..−0.28% | −3.34..−1.96% | 1.41 / 0.87% |
| Helper text control | Warm | −0.22..+1.51% | −0.62..+2.43% | 0.47 / 1.26% |
| Numeric CSV report | Compilation | −0.27..+0.99% | −2.85..+0.83% | 1.12 / 0.14% |

Natural-GC allocation3739038→3542430B/run, **−196608B/−5.26%**; accounted
peak6284280B unchanged. Original integer CSV allocation/peak flat; text essentially
flat (56Bdiagnostic noise). All shared budgets restore baseline. Warm CPU remains
inconclusive; moving fresh reference makes magnitude uncertain. Preserve the small
adverse text result. One bounded unchanged-image warm/fresh confirmation follows below. The original
favorable fresh magnitude is not confirmed; do not select only that observation.

Both actual Windows.NET11/10 pass433public/107internal focused checks, plus
211public boundary/cancellation checks and6actual async protocol checks (counts
may overlap). Five new observable alias/retained/pending-I/O cases pass. Full
585production snapshot and copied32file images preserved after a nonincremental
build. Both actual images pass3case worker/CPython goldens before staging.

Five jobs191.46s, quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, natural workstation
GC/normal tiering. Four independent processes/job, interleaved6s full-request
warmups and five rotated approximately0.5s samples. Ranges pair process medians,
not confidence intervals. Warm includes synchronous Run, mediated RAM I/O,
stdout/file goldens and prior-result cleanup; excludes compilation/startup/reset/
protocol/final cache teardown. Fresh adds compile/compiled-owner disposal; external
CPU is wider. No async/CPython/RSS/embedding-tail gain inferred.

All image/golden/quiet/Closed/exit/join/archive checks pass. RAM removed/terminal
cached. Fresh UID/path/hash/link/all-alias proofs retire7004853Bof superseded5ec
core8aliases and exact committed working evidence replicas. Qualified currentca1
images, toolchains/canonicals and254/252MiBstaging/final floors preserved.
No staging or timing failure in this collection. Retained source and five observable cases promoted; full delivery pending;
publication held; broader performance goal remains active.

[Summary](summary.json),[allocation](allocation-summary.json),[catalog](catalog.json),
[independent golden](golden.json),[raw](first-pair/observations.tgz),
[exact source](candidate-sources.tar.xz),[prototype](prototype.patch.gz),
[core](candidate-core.dll.gz),[identity](identity.json),
[checks/procedures](procedures-and-checks.tar.xz).

## B890 unchanged-image confirmation and decision

Two jobs95.71s; same source/images/catalog/rotation, interleaved8s full-request
warmups instead of6s, because the first fresh reference moved6.04% and warm was
inconclusive. No source tuning, forced GC or timing repair.

| Numeric CSV report | Elapsed change | Wider CPU change | Reference / candidate controls |
| --- | ---: | ---: | ---: |
| Warm | −1.57..−0.18% | −2.79..0.00% | 0.24 / 1.17% |
| Compile and execute once | **−1.39..+0.73%** | **−0.89..+1.80%** | 1.36 / 0.79% |

Retain for the shared smaller result boundary and demonstrated5.26%allocation
reduction, not a substantial CPU claim. Original fresh advantage is not reproduced;
its cause is unisolated. Small warm benefit, essentially flat fresh elapsed and
uncertain/slightly adverse fresh wider CPU remain separate. Preserve the first
small adverse text control; no prior integer/async debt is declared resolved.
No further unchanged-source timing is needed to make this retention decision.

Both collections pass all gates/users joined/RAM removed; raw/terminals cached.
Confirmation retires only465772Bof exact committed first working evidence
replicas under fresh UID/path/hash/link/all-alias proof, keeping both measured
cores/toolchains/canonicals and254/252MiBfloors. Three independent CPython
semantic programs and all13previous catalog goldens also pass the candidate.
No public API or contract break; full delivery is a separate checkpoint.

[Confirmation](confirmation-summary.json),[raw](confirmation/observations.tgz),
[decision](decision.json),[validation](validation.json),
[additional checks/procedures](confirmation-procedures-and-checks.tar.xz).
