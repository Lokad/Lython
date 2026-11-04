# Native dictionary ownership operations

2026-11-04. Qualified baseline **eff471b3 / 9588b3c8**. Final refinement based on
documentation1519dee3, frozen **38c03e5e**; exact [source identities](candidate-identity.json)
and [snapshot](source-snapshot.tgz). Quiet Azure F4as_v7/Ubuntu24.04.4/.NET11RC3,
normalGC/tiering. The [full self-dispatch prototype](../2026-11-04-dictionary-dispatch-withdrawn/README.md)
remains withdrawn with all adverse evidence. Its timings do not describe this code.

## Retained design and correctness

Remove capacity/release operations from the raw mapping-storage interface, including
three no-op view growth hooks and three throwing view-release methods. Native
PyDict owners already perform admission and private rollback; callers use those
operations directly. Preserve baseline ordinary mapping dispatch, actual live view
behavior, native table/capacity estimates, stored hashes, protocol keys, copy/attach
rollback, retained aliases, current-token cancellation and first refusal. No new
wrapper/backend/owner/ledger/policy. Production net **16lines removed**.

A pre-existing private-rollback bug refunded a live namespace's backing BEFORE
rejecting that rollback. Validate native/private ownership before returning any
allowance. Two attribute/module view budget-and-alias regressions fail on exact
committed baseline9588b3c8 and the original prototype; they pass after correction.
No tests/assertions removed or relaxed. No public API or Python contract change.

Nonincremental Release39.35s, zero warnings/errors. **770internal/2258public per
actual.NET11 and.NET10** pass: mappings/order/protocol mutation, namespaces/globals/
locals/environment, CSV/JSON, retained views/results, copy/attachment failure,
admission/cancellation and actual async callback cleanup. [Validation](validation.json),
[red/green/build logs](validation-logs.tgz). Runtime qualification is independent
of the measured baseline's already-cached green delivery.

## Final-source controlled comparison

Four complete cases **111seconds**, below180s owned limit. Three isolated images/
two old-source roles, six-second guest warmup/image, alternating preparation,
two-second settle/nine rotated200ms batches. All loaded core/aux/CoreLib/hash/
CPython complete goldens/quiet gates/ownership baseline/finalzero/fixtures/joins pass.
No failed final-source capture or timing-policy correction.

| Complete script | Phase/API | Candidate | Elapsed vs old / old control | Old-role spread / candidate IQR |
| --- | --- | ---: | ---: | ---: |
| text-summary-helpers | warm Run | 3.0052 ms | -0.51% / -0.16% | 0.35% / 0.35% |
| json-summary-helpers | warm RunAsync | 5.7272 ms | +0.08% / -0.13% | 0.21% / 0.48% |
| csv-summary-helpers | warm Run | 4.9792 ms | -4.99% / -5.08% | 0.10% / 0.17% |
| config-edit | fresh Run | 0.2974 ms | +1.85% / +0.00% | 1.85% / 0.55% |

Text and asyncJSON are near unchanged; the full prototype's clear adverse rows do
not persist in this narrower code. CSV elapsed−5.0–5.1% against0.10%old spread;
no attributed CPU explanation or asyncCSV gain is claimed. Fresh config matches one
old role while the other differs1.85%; small-change direction is control-limited.
Process allocation stays flat (fresh config <=0.01%noise). Admitted setup peaks
match exactly; the reused driver's initial warm witness does NOT separately
measure fresh config compilation/execution overlap or process/RSS peaks.

Retain for corrected ownership, removal of view memory-control stubs and the useful
CSV source-pair result. This is not a broad CPU/embedding-latency improvement or
closure of whole-script performance gaps. No unchanged retiming for prettier controls.

Invocation/mediated RAMIO/full checks/result cleanup included; fresh adds compilation/
compiled disposal. Reset/staging/startup/final cached teardown excluded, all users
joined. RunAsync IO completes; real suspension separately tested. No new external
CPython performance comparison. [All metrics](assessment.json), [original bounded plan](pre-measurement-plan.md),
[raw observations](observations.tgz), SHA256
`9f284f266b97af10ee746703fde037cb571eb60ce7501000d65e37c6437a0bda`.

Full Windows/Ubuntu actual11/10/original Stress/installed Release consumers for this
final source remain pending. Publication stays postponed.
