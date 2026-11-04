# Shared numeric layout and UTF-8 complex body — B190

2026-11-04. RETAINED334c7cc7; full delivery41748908560 pending. Measured readonly-layout prototype
`f967b9460a8b924780a5ba6be252923a8f7b84193be8fdbb198642157443beb2` is
b4de9833 plus the archived source patch. Qualified B188 source9ce21533/frozen
`cfdedd954ad1c040c3c4b1f17e40fb07816247fad5f045259466b26334b4d31e`
is both old-control roles. Actual frozen precommit bits retain their SourceLink
identity; later revision metadata may change a rebuilt hash without semantic change.

## Removed implementation and shared invariant

Legacy general/default/percentage float and Decimal renderers now return ungrouped
ASCII. They keep their existing notation/rounding algorithms. One scalar layout and
one byte writer serve both native byte digits and real legacy text. Encode text at
the end of the FINAL admitted output window: every final digit lies at or before
its staged source, so forward grouping/padding never overwrites a future input.
No proportional encoding buffer, padded/grouped CLR string or new owner/cache.
The immutable layout value holds no owned storage or caller token.

Nonempty complex formatting writes components directly through the same formatter
into a governed private UTF-8 body. It remains funded through final parent admission
and copy, and releases on all exits. Preserve empty-format Render(), imaginary-only,
negative zero, null-type .0 stripping, alternate/sign/grouping/Unicode alignment,
rounding, errors and real custom formatting suspension. More explicit temporary
capacity can require extra headroom; identical tiny-budget thresholds are not a
contract. Retained results/aliases and replacement overlap keep their existing owners.

Delete AppendInterpolatedFormatPadding's numeric/string assembly, GroupDigits,
GroupFloatingDigits, orphan FormatNumericSign, prefix-out plumbing and the now-orphan
FormatSimpleFloatingText bridge/finite F/E text arms. No second formatter, provider,
policy, cache tier, public option or memory-management mechanism. Native F/E span
algorithms remain. Opaque BCL conversion/encoding cannot interrupt mid-call;
managed scans/grouping/padding observe the current token, and first refusal remains
terminal. Final source diff is71 fewer production lines; no supported behavior removed.

## Focused validation

Independent CPython3.13.2 goldens reproduced seven legacy grouping failures before
source mutation; baseline16 passed/7 failed. Final136 internal/1,045 public tests
PASS per actual .NET11/.NET10. Three formatting entry points, both invocation APIs,
budget cleanup/reuse, general/percentage/default/Decimal and complex boundaries.
Large controls cover a2,048-significant-digit text body,3,000-scalar Unicode fill
and4,096-column grouped zero fill. Native width-denial tests assert the formatting
source site after value construction, first structured origin, sub-proportional
allocation, frame cleanup and allowance reuse. Existing protocol, suspension,
rounding, alias/cancellation and final-funding checks retained.

One initial large fixture exceeded Lython's documented fixed-precision Decimal
representation; it correctly failed before transport. Preserve fixture-correction.md;
replace that unsupported fixture with the independent large float/width goldens,
not a relaxed Decimal constraint or a larger timeout.

## Initial pair, including adverse controls

Initial image1cd2e26bc46fcd01521523bf11ab5a3f8fd56a7c5646330ea0d4e700cd69f963.
Same quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering, existing protocol2.
Report, legacy general/Decimal, complex and native F/E controls, both APIs. Three
old/new/old workers/job, three-second warmup, nine rotated~200ms batches, one-second
settle. Persistent evidence/workers; only guest files on tmpfs. All goldens/hashes/
quiet gates/balances/cleanup/fixture removal/worker joins pass. Owned171.210s.

Clocks include invocation, actual mediated I/O, complete output/file verification
and prior-result cleanup. Reset/staging/startup/final cached teardown excluded;
final users/cleanup joined. No CPython, compilation, suspended-I/O, equal-heap,
RSS or embedding-latency qualification. Systemd peak is not process RSS.

Positive means new is slower; old control is old2/old1.

| Lane | Workload | New/old1 | New/old2 | Old control |
| --- | --- | ---: | ---: | ---: |
|warm|report-render|+0.61%|+0.10%|+0.50%|
|warm|numeric-general-decimal|-4.01%|-3.76%|-0.25%|
|warm|numeric-complex|+5.52%|+4.11%|+1.36%|
|warm|numeric-floating|+3.43%|+2.48%|+0.92%|
|warm-async|report-render|+0.57%|+0.06%|+0.51%|
|warm-async|numeric-general-decimal|-2.33%|-3.00%|+0.68%|
|warm-async|numeric-complex|+1.02%|+0.96%|+0.06%|
|warm-async|numeric-floating|+0.92%|+0.31%|+0.61%|

Native sync F/E and complex sync are adverse with~0.4–1% IQR; do not label these
unstable. This justified one source refinement: pass the immutable layout by
readonly reference, eliminating a source-level28-byte value-copy boundary. The JIT
may optimize copies already; no automatic causal CPU attribution is justified.
Initial exact source/image/samples remain preserved, not pooled or overwritten.

## Readonly-layout refinement and completion decision

Measuredf967b946, same unchanged baseline/catalog/profile. Repeat the bounded eight
jobs and allocation diagnostic; owned172.884s. All identity/golden/quiet/ownership/
cleanup/fixture/join checks pass. No further refinement campaign for percentages.

| Lane | Workload | New/old1 | New/old2 | Old control |
| --- | --- | ---: | ---: | ---: |
|warm|report-render|+1.24%|+3.44%|-2.12%|
|warm|numeric-general-decimal|-4.81%|-3.99%|-0.86%|
|warm|numeric-complex|+5.39%|+3.56%|+1.77%|
|warm|numeric-floating|+1.74%|+2.17%|-0.42%|
|warm-async|report-render|+0.79%|+3.03%|-2.18%|
|warm-async|numeric-general-decimal|-5.15%|-3.07%|-2.15%|
|warm-async|numeric-complex|+2.52%|+2.29%|+0.23%|
|warm-async|numeric-floating|+2.76%|+2.30%|+0.45%|

Separate allocation/peak diagnostic uses three isolated images sharing process GC/
CoreLib/hashing,32 warmups and five16-job batches, five complete scripts plus three
controls, both APIs. Readonly prototype general/Decimal−11.34/−11.33%, complex−16.90% both APIs;
other complete jobs and native F/E near flat. Its elapsed samples are diagnostic,
not source-pair CPU proof. Recorded aggregate peaks are unchanged in these workloads;
that does not guarantee identical small-budget admission or every temporary peak.

RETAIN the corrected single layout/writer, direct complex body and71-line production
net deletion. General/Decimal elapsed improves approximately3–5%. Native F/E remains
adverse roughly1.7–2.8%; complex roughly2.3–5.4%. Accept these measured control costs
for correctness, consolidation and allocation reduction. Do not promise their later
removal or present this as a broad speedup. Report refinement+0.8–3.4% versus two old
roles with~2.1% control spread and its IQR is variable/slightly adverse; initial was
near flat. Preserve all points as provisional report uncertainty through the next
coherent whole-script completion decision. Fresh-JSON debt remains unrelated/open.
No CPython ratio update, fresh debt closure or full-performance-objective completion.
Return to substantial calls/frame or bulk-storage work after delivery.

## Machine/evidence limitations

Initial VM root filesystem approached full; guest fixtures were tmpfs and all
writes/goldens/gates/cleanup still completed. Before refinement, checked inactive
old build obj caches were removed; source/evidence/frozen binaries preserved, older
0f143a6c restored byte-for-byte from its verified compressed copy. Free space then
approximately27MB. Do not attribute a timing difference to disk space or readonly
passing from this comparison. Exact local/remote SHA256s matched after retrieval.

Initial observations.tgz SHA256:
`f2d6745fd30c7a9010447c2ceed0e599109bbfd0ba9a12eca7f7ad32d2372fb9`.
Refined observations.tgz SHA256:
`3358802abcfd7405911a33294fe788f0a04244d0de5f01b84eaef4c2cae52031`.
Both archives retain actual catalogs/source/core identities, warmups/samples,
allocation diagnostics, machine conditions, service exits and joined cleanup.
Publication postponed.

## Final correctness correction, not separately retimed

After the measured prototype, boundary review found a short-width defect inherited
from B188: grouped zero fill with fewer than four integral digits could use the
bulk-copy branch and put padding after the value (`1`/`03,d` → `100`). Six new
independent CPython cases fail on the measured prototype, then pass after the writer
uses bulk copy only when no grouped zeros are inserted. Covers integers, fixed and
general floats, Decimal, signed fields and all three entry points/both APIs.
Unused using directives were also removed. These final corrections have no separate
performance timing: the tables describe their recorded prototype hashes, not the
final commit. Exact final source/binary identity is recorded in final-source-state.json;
full delivery qualifies the actual committed source. No claim that a prototype's
elapsed delta measures today's HEAD. Preserve short-width-before-correction.log.
