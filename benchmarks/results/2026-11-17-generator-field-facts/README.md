# Generator field facts

Private R503 on7584bcaf/core5ae019db versus qualifiedea8production/corecf847936.
Remove one compiler exclusion: function-like code, including generator expressions,
uses existing field-cache sites. No new runtime cache, owner, token, policy or
execution path. PyGenerator already creates a work state with no function lookup
owner, funds its private cache through owned workspace and detaches after awaited
cleanup. Entries retain layout/key/index, never row values; hits read live slots.
The existing conservative frame estimate includes lookup metadata; actual cache
backing is separately admitted. New sites may reject tighter allowances earlier.

B928,2026-11-17 quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering,
fourjobs155.24s/six-second warmups/five rotated rounds/two processes per source:

| Complete script/phase | Elapsed change | Process CPU change | Reference/candidate spread |
| --- | --- | --- | --- |
| Floating numeric report warm | −10.03..−9.16% | −9.61..−8.61% |0.15/0.82%|
| Floating compile-to-result | −13.56..−11.93% | −12.61..−11.75% |0.60/1.28%|
| Text normalization warm | −0.08..+4.00% |0..+1.63%|0.78/3.28%|
| Positional calls warm | −4.96..−0.19% |−6.67..+0.79%|4.67/0.33%|

Retain the ownership-consistent correction and named floating benefit. Preserve
text's possible adverse CPU/elapsed movement and candidate spread; call reference
maxIQR5.10%limits precision. No universal gain or recovery claim, no new CPython
ratio inferred. Allocation/peaks effectively flat; floating+0.12%/same906,686Bpeak,
all six budget baselines restore. Both actual11/10 each407internal/570public
focused checks and six exact-worker goldens pass. Four added public cases preserve
mutation/rebinding, interleaved generators, actual delayed host I/O, protocol-key
fallback, errors/close, first terminal refusal/no guest continuation and reuse.

Full Windows/Ubuntu/originalstress/installed Release package qualification is
[complete at62786960](delivery/README.md). Frozen images copy and verify all32recorded
files; auxiliary worker/optional adapter remain reference files. Exact cfreference
diagnostics are reused only after full hashes/catalog comparison. The unchanged
six-case catalog was verified with actual images before staging. Sources and
test snapshots identify2386inputs/585production. Rebuilt-binary equality is not
inferred. This consistency fix does not close the larger activation/operand goal.

Warm includes Run, mediated RAM I/O, goldens and prior-result cleanup; fresh adds
compile/compiled-owner disposal. Startup/reset/protocol/final cache teardown
excluded; processCPU wider. Quiet gates, joined users, persistent archive/localSHA
and fixture teardown pass. Only exact committed raw/archive replicas and the old
five-case catalog retire; currentcf/six-case catalog/toolchains preserved.254/252MiB
guards unchanged. A reused preparation kept six-job validators; the local guard
caught that before staging. Actual four-job files/caps were generated directly
from verified templates, originals retained; no failed collection or retiming.

[Decision](decision.json),[summary](summary.json),[allocation](allocation-summary.json),
[raw](observations.tgz),[identity](identity.json),[source](candidate-sources.tar.xz),
[procedures/checks](procedures-and-checks.tar.xz).
Publication remains postponed; the broader runtime roadmap continues.
