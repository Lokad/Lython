# Shared formatted-string construction

2026-11-05. Retained source: parent 3a2a9d5b plus the exact patch/snapshot below,
core **68a9f3f8**. Baseline fully qualified **8475a8dd**, core **81deb3be**.
Focused checks pass; full platform/Stress/installed-consumer delivery pending.
Publication postponed; the broader whole-script objective remains open.

Static literal byte lengths and already-parsed format widths plan modest initial
backing for the existing multi-part f-string builder, on both ordinary and native
helper paths. Remove intermediate growth buffers/copies/admission transitions;
no extra formatter, owner, value packet, cache or governor policy. Metadata lives
in the existing start operation; its compiler placeholder is replaced before code
publication. Actual values are not cached or moved across admission or effects.

Only the initial hint is bounded to 256 B; output and widths have no new ceiling.
Larger output follows ordinary governed growth. Conservative initial capacity
can retain more storage or refuse before a field callback that formerly ran.
Every actual capacity/replacement remains admitted; refusal never retries a
smaller allocation. Constructor failure/cancellation after admission now refunds
unpublished storage before allocation; retained strings keep transferred capacity.

## Complete-script comparison

Quiet Azure F4as_v7/Ubuntu 24.04.4/.NET 11 RC3, ordinary GC/tiering. Existing
complete-job driver, three isolated images/two unchanged baseline roles, identical
auxiliaries/CoreLib/string hashes. Six seconds guest warmup/image, two seconds
settle, nine rotated ~200 ms batches. Complete owned collection **165 s**.

Invocation, mediated RAM I/O, full output checks and result cleanup included.
Fresh adds public Compile/Dispose; compile-only excludes execution/I/O. Fixture
reset/staging/startup/hash/final cached teardown excluded; all users joined.
Process allocation includes reset/background work. All exact binary identities,
independent CPython 3.13.16 stdout/file goldens, quiet endpoints, live accounting
baselines/final zero, normal closes, joined users and fixture removal pass.

| Complete job | Elapsed vs baseline / unchanged control | Baseline spread | Candidate IQR | Allocation vs both |
| --- | ---: | ---: | ---: | ---: |
| Report warm | −6.96% / −8.92% | 2.15% | 0.64% | −10.33% |
| Report async/completed I/O | −7.81% / −5.76% | 2.17% | 4.53% | −10.32% |
| Report compile-to-result | −8.55% / −8.96% | 0.45% | 0.78% | −9.99% |
| Report compilation | +0.84% / −1.44% | 2.32% | 0.98% | about flat |
| Integer-report warm control | +0.19% / −0.32% | 0.50% | 1.47% | +0.02% |
| Text warm control | −3.07% / −6.07% | 3.19% | 1.02% | flat |

Retain: affected complete reports improve allocation and elapsed against both
roles, numeric control is near flat, compilation is inconclusive. Async dispersion
limits its precise magnitude. Text control has no attributed improvement; retain
its full observations. No CPU, RSS, startup, pending-I/O, universal speedup or
updated CPython-ratio claim. Source-pair percentages are not additive.
Initial untimed admitted report peak decreases 33,552 B; this is accounting under
these fixtures, not physical-heap evidence or a guarantee for every format.

## Validation and evidence

Nonincremental Release build: 44.27 s, zero warnings/errors. **275 internal and
1,167 public tests per actual .NET 11/.NET 10**, including native/ordinary format,
cancellation, refusal, escaping result, generator and real pending-host cases.
New constructor controls preserve admitted-cancellation refunds, first refusal
when cancellation coincides, no retry, and budget reuse without collection.
Public controls preserve callback/error order for a huge invalid width, growth
past the hint, nested fields and control-flow expressions. No timeouts/assertions
or original stress scales relaxed.

[Pre-code contract](plan.md), [metrics](assessment.json),
[raw/source/identities/gates](observations.tgz), [validation](validation.json),
[logs](validation-logs.tgz). An initial SSH launch command had a quoting error
before the service or any benchmark started; the corrected file-based launch and
observed live service completed normally. Raw conditions/results are unchanged.
