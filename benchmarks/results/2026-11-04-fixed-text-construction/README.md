# Fixed immutable UTF-8 construction — B194

RETAINED source: `9a4af031` plus the exact archived complete patch. Measured complete core `ba48576af397ef62ce62a493b7ab4eb2ff845874fe44c512e6e5ceadf8e63783`; baseline B192 `659fa32a`, matching fully qualified runtime9a4af031. Full delivery of this new runtime is pending. Publication/tagging postponed. Receipt commits are not measured binaries.

## Removed work and ownership

Known-capacity UTF-8 copies, casing, padding and terminal string metadata use the existing governor reserve/commit/refund balances directly. Their temporary heap reservation carriers disappear. One internal FromReservedUtf8 consumes a fixed private admission only after creating its immutable result; empty output refunds that admission. Cancellation before allocation/construction refunds only local reserved bytes; existing final cancellation checks refund a fresh unpublished result after ownership transfer. Inputs and returned aliases stay independently funded.

Delete CopyUtf8Slice and its orphan FromUtf8 wrapper: they preflighted a fresh byte copy and then admitted the SAME storage again. The existing central copy constructor now performs one preflight. Real source/replacement overlap remains funded. Growing/shared/batched/asynchronous reservations still require their existing object/lifetime and remain; no new budget, scope, pool, ledger, value-type lease, watchdog or configuration. No public API change, compiler rewrite, backend dependency or unsafe code. Unicode/context, scalar widths, errors and ownership behavior remain supported; removing redundant admission can improve tiny thresholds.

B193 activation source assessment ends in precise deferral: existing bound locals already reuse storage/pools; an inheritance merge keeps most fields and needs retained-closure retirement. The full17.84% context/interpreter allocation share is not removable header bytes or dominant CPU. This repeats neither that qualification nor a failed arena prototype. See the source decision alongside.

## Measurements and decision

2026-11-04, existing quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3. NormalGC/tiering, physical worker bootstrap/verified core+aux hashes, sequential old/current/old roles. Five complete CSV/text/JSON/report/config jobs plus casing control, both APIs;3s warmup per image,9 rotating~200ms batches,1s settle. Only guest fixture files use tmpfs; workers/raw evidence stay persistent. Execution includes actual mediated I/O, full stdout/file checks and prior-result disposal. Compilation/startup/reset/staging/final cached teardown excluded, all work/cleanup joined. RunAsync host I/O completes synchronously here; genuinely suspended behavior is correctness coverage, not timing.

| Complete source allocation | Run | RunAsync |
| --- | ---: | ---: |
| csv-summary-helpers | -3.05% | -2.88% |
| text-summary-helpers | -7.31% | -7.31% |
| json-summary-helpers | -10.68% | -10.67% |
| report-render | -4.18% | -4.18% |
| config-edit | -12.46% | -12.42% |
| fixed-text-casing | -12.72% | -11.60% |
| fixed-text-padding | -8.01% | -8.01% |
| fixed-text-slices | -12.69% | -12.68% |

Separate isolated-ALC allocation diagnostics share process GC and are not CPU proof. Both partial and complete captures establish allocation direction. Whole-execution/root admitted peaks remain recorded independently; removing untracked control objects is not a claimed proportional accounting reduction or equal-heap/RSS/host-latency guarantee.

| Complete source elapsed | API | Versus old roles | Old-role difference |
| --- | --- | ---: | ---: |
| csv-summary-helpers | warm | -0.02% / -6.34% | +6.75% |
| text-summary-helpers | warm | -1.03% / -5.70% | +4.95% |
| json-summary-helpers | warm | -7.01% / -6.13% | -0.93% |
| report-render | warm | -4.19% / -4.01% | -0.19% |
| config-edit | warm | -3.13% / -4.75% | +1.70% |
| fixed-text-casing | warm | -10.46% / -6.45% | -4.29% |
| csv-summary-helpers | warm-async | -1.38% / -0.38% | -1.01% |
| text-summary-helpers | warm-async | +10.60% / +3.58% | +6.78% |
| json-summary-helpers | warm-async | +1.17% / +1.36% | -0.19% |
| report-render | warm-async | -2.67% / -0.29% | -2.38% |
| config-edit | warm-async | -8.94% / -7.18% | -1.89% |
| fixed-text-casing | warm-async | -4.59% / -5.81% | +1.29% |

Partial0c456e67 does not include the last terminal-metadata constructor. Its completion was selected from source BEFORE reading partial results; keep both exact patches. Partial allocation-2.6..12.4%; asyncJSON elapsed adverse+3.8% despite stable old roles, while text old-role spread5.3..9.6% limits its favorable point. Complete-source CSV/text old-role spread4.9..6.8% also limits conclusions. No favorite-control selection and no attribution to GC/JIT from source speculation.

One justified SAME-bit ba48576a confirmation uses swapped old-role preparation,6s warmup/13~300ms rounds/1s settle, only text both APIs and asyncJSON; no allocation replay.

| Same-bit confirmation | API | Versus old roles | Old-role difference | Candidate IQR |
| --- | --- | ---: | ---: | ---: |
| text-summary-helpers | warm | -7.81% / -7.09% | -0.77% | 1.06% |
| text-summary-helpers | warm-async | +1.68% / +1.18% | +0.50% | 1.38% |
| json-summary-helpers | warm-async | -2.75% / -5.91% | +3.36% | 1.08% |

**Retention/debt decision:** retain the completed constructor family for coherent removal of fixed heap carriers/duplicate admission and robust3–12% whole-script allocation savings. Controlled synchronous text improves7.1–7.8% (old spread0.8%, candidateIQR1.1%); synchronous report complete pair improves4.0–4.2% (old spread0.2%). These are affected-workload elapsed observations, not a general CPU claim. Config is favorable; CSV remains inconclusive.

**Warm async text remains accepted possible1–2% elapsed debt**, with candidate1.4%IQR, old roles0.5% apart and one old1.2/2.8%IQR. Original complete+3.6..10.6% stays visible. AsyncJSON changes direction in confirmation with3.4%old spread; preserve initial/final adverse points and mark its current direction uncertain. Do not claim the warm confirmation closes earlier **fresh-JSON2–5%** debt against pre-loopB166. No third unchanged retiming now; materially new evidence may justify bounded correction. Next coupled CPython checkpoint must decide fresh debt. Whole-script performance remains OPEN.

## Validation and artifacts

Complete source passes213 internal/557 public focused Release checks per actual.NET11/.NET10, including terminal transfer, Unicode expansions/Sigma/context, formatting, refusal origin, admission-triggered cancellation, suspended ownership and returned aliases. New cross-owner split regression proves output funding survives source-owner disposal and final scope disposal restores allowance. Release solution builds both targets. Full platform/originalStress/installed-consumer checks follow retained delivery; prior B19241754575737 is already terminal all-eight green and is not this source’s qualification.

All captures complete: partial234.964s, complete234.995s, same-bit98.199s. Identity/goldens/balances/quietbefore+after/normal-close+exit0/joins pass; no remaining owned workers or guest fixture directories. Raw archives include catalogs, workers/protocol/collectors, patches, warmups/samples and separate allocation observations; exact identities/decisions/summaries alongside.

- Partial archive SHA256: `9a0a3a6f80a29d782366919f50c5c96d98425881426ce141f5adec7e1cb2c12e`.
- Complete archive SHA256: `25f6c3a296f7754929793411bcd87ea08b34f749afc0b130679b33b92d4a2525`.
- Same-bit confirmation SHA256: `d0040fcd0b42281f0cbf6b4f461132644d93280bd3b2f96d96535d004d81f2bc`.
