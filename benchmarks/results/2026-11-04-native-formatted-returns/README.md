# Native formatted helper returns

Measured 2026-11-04; retained for substantially less whole-report allocation and
elapsed time. This extends the existing native-return instruction proof to static
builtin formatting, removing the full invocation/context/interpreter/operand
construction for qualifying helpers. It reuses the formatter and its working
builder; custom protocols and genuinely suspending callbacks use the ordinary
executable route. No second parser, backend, owner layer or public option.

Source is `48d4bf14` plus B201, frozen core SHA256
`718796567fc8e36c597eddd1d92c87b93573b833f3cf69a85ee345aa05686c14`.
`source-snapshot.tgz` contains all four production files, including the new
NativeFormatting file which was untracked when `source.patch` was captured.
The snapshot and identity hashes, rather than that partial patch, define the source.
The unchanged old roles are B197 (`ee2b4529`, core `1b9b3230`); the current role is
B200 (`48d4bf14`, core `68899ba8`). No CPython ratio follows from this comparison.

## Controlled complete scripts

Quiet Azure F4as_v7, Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap,
.NET11 RC3 SDK/runtime11.0.100-rc.3.26522.148/11.0.0-rc.3.26522.148,
normal GC/tiering. Four isolated images per job,6s warmup each, nine rotating
approximately200ms samples. Goldens, Ready/source identities, quiet gates,
ownership balances, normal cleanup and worker joins all pass. Capture288.705s;
complete owned service289.316s, below420s bound. No guest fixtures remain.

Warm execution includes invocation, actual mediated RAM-file I/O, full checks and
prior-result disposal. Compilation/startup, fixture reset/staging, response hashing
and final cached teardown are excluded; all users are joined. Compile-run includes
fresh compilation/disposal; compile-only excludes execution/I/O. Independent phase
medians must not be subtracted.

Elapsed change, percent; negative means faster. Two old roles have identical code.

| Complete job | Candidate vs old first / last | Candidate vs B200 | Old-role spread |
| --- | ---: | ---: | ---: |
| Report warm Run | -7.45 / -7.13 | -8.07 | -0.34 |
| Report warm RunAsync | -11.62 / -9.28 | -10.21 | -2.57 |
| Report compilation | +2.20 / +1.93 | +2.21 | +0.26 |
| Report compile-run | -9.57 / -8.35 | -10.09 | -1.34 |
| Text helpers RunAsync | -4.84 / -2.02 | -5.69 | -2.88 |
| CSV helpers RunAsync | -4.08 / -1.95 | -4.20 | -2.17 |
| JSON helpers Run | -4.23 / -2.05 | -0.44 | -2.23 |

Report candidate IQR is3.69% warm,3.11% async,1.39% compile,3.08% compile-run.
The report benefit exceeds its controls; the approximately2% compilation cost is
accepted with improved fresh execution. Small helper deltas are control-limited;
do not attribute unrelated text improvements to formatting work. Process CPU is a
separate, quantized Linux100Hz diagnostic including reset/transport/JIT overhead;
the established results here are elapsed time and allocation, not an isolated CPU
percentage or embedding-process latency improvement.

The existing three-isolated-image eight-case allocation driver, both APIs, compares
B197/candidate/B197. Report Run falls from6,022,478 to4,449,496.5 bytes/job;
RunAsync from6,023,882 to4,450,631.5: **26.12% less allocation**. Seven controls are
essentially flat (-0.01..+0.03%). Report admitted execution/root peaks remain
4,706,207/4,788,639 bytes. Allocation reduction is not a live-budget/RSS claim.

## Retention and remaining timing debt

Retain B201 and the [shared JSON grammar](../2026-11-04-shared-json-container-grammar/README.md)
B200, which removes95 net lines. B200's earlier async-report+3.2-4.0% is not
reproduced here: -1.56..+1.04% versus old roles that differ2.57%. Its earlier
async-text+2.7-3.0% remains **accepted uncertainty**, with B200 now+0.90..+3.90%
and2.88% old-role spread. The combined image is favorable versus both old roles
in measured helper jobs; this does not explain an unrelated text point. Earlier
B194 possible async-text1-2% cost remains separate. Preserve every original adverse
sample; do not add deltas from different baselines or repeat unchanged retiming.

## Semantic, cancellation and ownership checks

Nonincremental Release solution build, zero warnings/errors. Focused250 internal
and408 public cases per actual.NET11/10 pass, then seven helper cases per target
(six overlap the preceding selection,409 unique public cases total). Coverage
includes formatting order/Unicode/identity, bad-format-before-later-missing-key
and protocol equality, real suspended custom formatting, retained funding,
terminal admission refusal, cancellation during substantial builder growth,
baseline reuse, caller exception state and source frames. An isolated managed-stack
scenario rejects deep formatted-helper recursion three times after genuine host
suspension and successfully calls the helper after each rejection.

The original new low-level cancellation test expected a runtime wrapper; the
existing formatter correctly throws OperationCanceledException. Its failed log
and corrected expectation are preserved; no runtime change, timeout relaxation
or weakened containment assertion followed. CPython3.13.2 isolated semantic
goldens are separate from performance evidence.

Full Windows/Ubuntu, original stress and installed-consumer delivery is pending;
focused checks do not imply full qualification. Publication/tagging stay postponed.

Raw archive SHA256:
`973a342dccc1c668328710ccff6d88eed241baae0471949bb33e01ed13cdd8ed`.
`observations.tgz` retains all responses, controls, identities, source snapshot,
manifest and conditions. `focused-qualification.tgz` includes preliminary/final
logs and TRX, including the original failed expectation. Summaries do not replace
raw evidence.
