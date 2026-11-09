# Native helper direct inputs and fixed local prefix

R132, 2026-11-09. Qualified459813a3/core1be2 versus frozen final prototype95fc.
Consumed native-helper constants and bound locals use the existing executable
operand convention: negative constant references, local prefix, then operation
references. Removed their full Read nodes and recursive leaf dispatch. Real roots
and mutable names retain source metadata. Removed private NativeValueIndex /
NativeArgumentIndex plumbing; argument sources use the existing missing-source
sentinel. One immutable code-local prefix replaces repeated interface Count queries.
No new frame, scratch array, owner, cache, engine, option or eligibility restriction.

Complete proofs still precede effects. Actual mutable field reads remain distinct
from proofs after admission; only selected branches execute. Operation, member,
entry/root/return checks and bounded long-operation cancellation remain. Consumed
pure leaves no longer have individual cancellation checkpoints; exact checkpoint
frequency/source observation is not a fuel contract. Unbound reads use ordinary
executable diagnostics. Managed depth, admission/refusal and retained funding stay.
Public APIs are unchanged.

Initial4f7, refined3afe and final95fc each pass focused Release87internal/394public
cases peractual.NET11/.NET10 after real source changes. No tests, assertions or
timeouts changed. Native/conditional/formatting returns, globals/default rebinding,
real suspension, cancellation, first refusal, retained aliases and budget reuse pass.
Compile-only census confirms ordinary instructions/storage metadata and prefix
counts identical: wanted3→1native nodes, words_from_line2→1, charge9→4; all consumed
Read leaves disappear. The census is not CPU evidence.

Compilation allocation: search−0.107%, text−0.018%, numericCSV−1.179%, validatedCSV
−0.309%; report+0.014%small metadata increase. Coarse admission unchanged. Final
warm allocation within0.038%. Desktop elapsed remains diagnostic: initial text+57%/
search+34%, control drift43.63%, final text+9.8–10.2%/report+8.6–12.0% all preserved.

B492 compares intermediate3afe: six jobs169.786s, no consistent gain; report elapsed
+1.85..4.64%, textCPU+1.25..5.33%adverse. One source-backed refinement publishes
the fixed local prefix rather than querying IReadOnlyList.Count during input decoding.
Temporary GuardNativeInput/EvaluateNativeInput wrappers had already been removed;
initial source/binary/data stay separate.

B493 compares final95fc: six jobs169.776s, four independent processes/job, five-second
warmup/role, seven rotated150msrounds, normal GC/tiering on quiet F4as_v7/Ubuntu24.04.4/
.NET11RC3. Warm public Run/awaitedRunAsync includes mediated RAM-file I/O, full
goldens and prior-result cleanup. Compilation/startup/reset/protocol/final cache
teardown excluded. External process CPU includes wider reset/protocol work and coarse
ticks; it is not isolated guest CPU.

| Complete script / API | Elapsed change vs both references | Process CPU change | Reference / candidate spread |
| --- | ---: | ---: | ---: |
| File search / sync | −3.40..−2.45% | −0.46..+1.79% |0.58% /0.41% |
| Text helpers / sync | −0.05..+0.98% |0.00..+5.35% |0.33% /−0.69% |
| Report / sync control | −2.86..+0.95% | −1.79..+2.22% |0.26% /3.65% |
| Numeric CSV / sync | −3.47..+1.32% | −3.13..0.00% |1.28% /3.64% |
| Validated CSV / sync control | −5.65..+0.69% | −6.45..+0.00% | −3.07% /−3.32% |
| File search / async | −4.96..+0.87% | −3.81..+2.98% |0.58% /5.53% |

Ranges span both candidates against both references; signed spread compares the
second same-image process with the first. Sync search has a modest consistent
elapsed benefit. Other cases are mixed/control-limited, text CPU debt remains and
the original adverse report result is preserved. Retain the shared representation
and fixed invariant with those limits; no universal CPU gain/neutrality claim or
further unchanged retiming. This does not complete the whole-script objective.

Both collections pass identity/golden/budget/quiet/Closed/join/RAM-removal/archive/
local-verification gates. Final collection reuses an owned SHA-identical catalog;
no disk recovery or floor reduction. Terminal cached, no remaining users/fixtures.
Full candidate platform/stress/installed-core qualification is pending; reference
CI42545629905 qualifies459/core1be2 only. Publication postponed.

[Identity](identity.json), [focused checks](focused-validation.json), [census](census-assessment.json),
[compilation](compile-assessment.json), [desktop](assessment-local.json),
[B492 results](before-prefix-assessment.json), [B492 receipt](before-prefix-archive-receipt.json),
[B492 raw](before-prefix-observations.tgz), [B493 results](assessment.json),
[B493 receipt](archive-receipt.json), [B493 raw](observations.tgz),
[all source variants/binaries/diagnostics](source-and-diagnostics.tar.xz).
