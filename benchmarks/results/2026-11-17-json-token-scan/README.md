# Bulk JSON string scan: withdrawn

R469/B875, 2026-11-17 UTC. Qualified ccf20ba3 / frozen 7a599347 versus private
c882e206 + one production-file prototype / frozen 06797383. **Withdraw this shape;
production scanning remains unchanged.** Six useful public boundary cases survive.

The prototype replaces ordinary per-byte token classification with bounded bulk
delimiter/control searches in the existing decoder. It preserves escape handling,
UTF-8 validation, strict/non-strict behavior, scalar errors, ownership/preflights
and the existing 1,024-byte cancellation boundary. No second parser, memory credits,
policy, cache or option. It does not establish a whole-script improvement:

| Complete workload | Phase | Candidate elapsed change | Wider process CPU change | Reference / candidate control spread |
| --- | --- | ---: | ---: | ---: |
| Helper-list report, 3,072 records | Warm | −1.70..−0.39% | −4.51..+0.32% | 1.18 / 0.16% |
| Same report | Compile and execute once | −2.20..+4.54% | −0.56..+7.25% | 0.91 / 5.93% |
| JSON-lines helpers, 4,096 records | Warm | +0.07..+1.04% | −2.14..0.00% | 0.60 / 0.37% |
| Same JSON-lines script | Compile and execute once | +1.20..+3.16% | +1.31..+3.77% | 0.83 / 1.10% |
| Helper-list normalization, unchanged | Warm control | −1.89..+0.41% | −2.41..+0.09% | 0.54 / 1.79% |

All four independent-process median pairings are retained, not confidence
intervals. Report warm has a small favorable elapsed effect and mixed CPU; fresh
is inconclusive with a moving candidate. JSON fresh consistently regresses with
supporting CPU and close controls. Allocation is essentially flat; all accounted
peaks are unchanged. The small report effect does not justify the JSON debt.
Possible short-token search overhead is a hypothesis, not an isolated cause.
No permanent design ban or fixed-percentage veto; a different coherent design
would need different eliminated work. No preferred-sign confirmation or ad hoc
short-string route is introduced. Broader whole-script work continues.

Initial actual Windows net11: 969 public / 213 internal; final 28 escaped/boundary
cases pass. Actual net10: 972 public / 213 internal. Filters overlap and are not
summed. After withdrawing production, the 28 boundary cases pass again on each
actual target. New cases cover escaped Unicode around long-token boundaries and
strict control errors/non-strict values with non-BMP scalar positions; retained
results stay charged and cleanup restores baseline. Local CPython 3.13.2
independently confirms new expected values/positions. No assertion or timeout
relaxed. Full platform/stress/package checks are not replayed for this withdrawal.

Both frozen 32-file images differ only in core; worker and normal optional 0.4
adapter assets are fixed. A nonincremental Release build precedes freezing.
All twelve cases pass actual frozen-worker/local CPython golden verification.
The fixed catalog is the prior eleven cases plus the unchanged existing R461
JSON-lines helper case. Initial eleven-case verification/allocation diagnostics
are preserved separately; they are not this final catalog or quiet comparison.
Natural-GC allocation diagnostics return to the compiled baseline after every run,
and zero after final disposal. Local first-use timings remain diagnostic only.

Five jobs / 153.70 s, quiet existing Azure F4as_v7 / Ubuntu 24.04.4, four EPYC
9V45 vCPUs, 16 GiB/no swap, .NET 11 RC3 normal tiering/workstation GC. Two
independent processes per source, 4 s full-request warmups and five rotated
approximately 0.5 s samples. Warm includes mediated RAM I/O, full stdout/files
and prior-result cleanup; excludes compilation, startup, fixture reset/protocol
and final cache teardown. Fresh adds compilation and compiled-owner disposal.
External CPU includes reset/protocol. No CPython performance refresh, async,
RSS, pending-I/O or embedding-tail claim.

All identities/goldens/quiet/Closed/exit/join/archive gates pass. All users joined
and RAM fixtures removed after persistent evidence verification. Six exact old
committed raw JSON replicas (9,597,649 B) retired after fresh owner/path/SHA,
link-count and all-inode-alias proof. Current images/toolchains/canonicals and
254/252 MiB staging/final floors preserved. Terminal cached permanently; no
unchanged requery, profiling or qualification. Publication/tagging held.

[Summary](summary.json), [allocation](allocation-summary.json),
[validation](validation.json), [raw observations](observations.tgz),
[sources](candidate-sources.tar.xz), [prototype patch](prototype.patch.gz),
[frozen image identities](identity.json), [procedures/checks/original diagnostics](procedures-and-checks.tar.xz),
[independent CPython boundary results](boundary-cpython.json),
[reference core](reference-core.dll.gz), [withdrawn core](candidate-core.dll.gz),
[custody](archive-receipt.json), [terminal](terminal-status.json).
