# Coupled source and CPython checkpoint — B189

2026-11-04. Current runtime9ce21533 (B188), frozen precommit
`cfdedd954ad1c040c3c4b1f17e40fb07816247fad5f045259466b26334b4d31e`.
Source files/patch are preserved in the adjacent admitted-numeric-formatting evidence;
exact-source9ce21533 full Windows/Ubuntu, both runtimes, original Stress and
installed consumers GREEN41742152264. [Cached receipt](../2026-11-04-admitted-numeric-formatting/delivery-ci-status.json); do not re-query.
The frozen build hasb09b259e SourceLink plus that patch; do not relabel its bits.

Direct pre-loop B166 baseline, qualified2323cceb:
`3bed411efcd6e008fc04bca8634d32be64bafc71742509ff0192e8eee86ab8bc`.
This single direct pair includes B171 loop operands, B174 terminal storage, B178
casing, B183 reader borrowing, B186 dictionary ownership and B188 numeric output.
Do not sum independent historical percentage gains or suppress earlier regressions.

Existing protocol2 on quiet Azure Standard_F4as_v7, Ubuntu24.04.4,4EPYC9V45 vCPU,
16GiB/no swap. .NET11 RC3 and CPython3.13.16 GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug. Normal managed worker GC/tiering. Persistent catalogs/workers/evidence;
only guest fixture files use shared tmpfs. Four complete file scripts, each warm
and fresh, plus separate CSV/report compilation. New/old/unchanged old/CPython
processes run sequentially; three-second warmup, nine rotating~200ms batches,
one-second settle. All identities, full goldens, allowance balances, before/after
quiet gates, fixture removal and joined cleanup pass. Owned218.403s.

Both execution clocks include setup/invocation, actual mediated file I/O, complete
stdout/output-file checks and previous-result cleanup. Fresh includes compilation
and per-invocation compilation cleanup. Fixture staging/reset, startup, response
hashing and final cached compilation/last-result teardown are excluded. All final
users/cleanup are joined. Compile-only excludes execution/I/O. No genuinely
suspended-I/O, equal-heap, embedding latency or RSS claim. Phase medians cannot be
subtracted. Systemd peak is not process RSS.

## CPython reference

Ratio is current Lython elapsed / CPython elapsed; above one is slower.
IQR is interquartile range / median. Input sizes are unchanged B184 fixtures.

| Lane / complete script | Lython ms | CPython ms | Ratio | Lython / CPython IQR | Old control |
| --- | ---: | ---: | ---: | ---: | ---: |
| warm / csv-summary-helpers |5.294342|3.332585|1.589×|0.62/0.48%|+1.28%|
| compile-run / csv-summary-helpers |5.371826|3.455928|1.554×|2.93/0.87%|+1.00%|
| warm / text-summary-helpers |3.286462|1.159418|2.835×|0.80/1.52%|+4.61%|
| compile-run / text-summary-helpers |3.443886|1.278366|2.694×|1.19/0.90%|+22.26%|
| warm / json-summary-helpers |5.685747|4.074449|1.395×|1.19/0.62%|-0.34%|
| compile-run / json-summary-helpers |5.774347|4.213899|1.370×|1.09/0.97%|+0.45%|
| warm / report-render |5.127181|2.338600|2.192×|2.84/3.24%|+2.48%|
| compile-run / report-render |5.412593|2.455231|2.205×|6.61/3.36%|-0.11%|
| compile / csv-summary-helpers |0.086706|0.055000|1.576×|0.63/0.22%|-1.03%|
| compile / report-render |0.059145|0.037571|1.574×|0.60/0.55%|+0.02%|

Text fresh unchanged controls differ22.26%, warm4.61%, despite within-worker IQR
approximately1%. Do not infer a reliable text source-pair gain or attribute the
cause. Report fresh IQR6.61%Lython/3.36%CPython remains variable. These are bounded
diagnostic points, not certified multipliers; text/report gaps remain substantial.
Earlier B184/external ratios retain their historical identities and conditions.

## Direct coupled source pair

Positive means current is slower. Initial old2/old1 is the independent control.

| Lane | Script | New/old1 | New/old2 | Old control |
| --- | --- | ---: | ---: | ---: |
| warm | csv-summary-helpers |+0.38%|-0.90%|+1.28%|
| compile-run | csv-summary-helpers |-0.23%|-1.22%|+1.00%|
| warm | text-summary-helpers |-4.93%|-9.12%|+4.61%|
| compile-run | text-summary-helpers |-2.10%|-19.93%|+22.26%|
| warm | json-summary-helpers |+2.04%|+2.39%|-0.34%|
| compile-run | json-summary-helpers |-0.19%|-0.63%|+0.45%|
| warm | report-render |-6.63%|-8.89%|+2.48%|
| compile-run | report-render |-5.51%|-5.41%|-0.11%|
| compile | csv-summary-helpers |-0.32%|+0.72%|-1.03%|
| compile | report-render |-1.55%|-1.57%|+0.02%|

Initial warm JSON+2.0–2.4% with stable~0.3% old control, fresh JSON−0.2–0.6%
with0.45% control. Fresh older IQR2.77% and prior confirmed fresh debt justified
one same-bit JSON-only confirmation. Keep original current/old/control data.

## Same-bit JSON confirmation

SOURCE ROLES SWAPPED: two current images, one pre-loop old image. Both warm and
fresh JSON, five-second warmup, nine rotating~250ms batches, one-second settle.
No new CPython or allocation run. Owned50.081s; all goldens, image identities,
allowance balances, quiet gates and joined fixture cleanup pass. Separate sampling;
never pool with initial profile.

| Lane | New1/old | New2/old | New control | New1 / old / new2 IQR |
| --- | ---: | ---: | ---: | ---: |
| warm |+0.29%|-0.30%|-0.59%|0.63/0.87/0.85%|
| compile-run |+4.94%|+2.13%|-2.68%|0.75/0.61/0.61%|

## Completion decision and remaining debt

Retain the coupled simplifications. Report remains below both old roles:
−6.6–8.9% warm /−5.4–5.5% fresh; initial original B171+1.35% remains historical.
This does not close any asynchronous debt or establish a CPU-only improvement.
CSV is near unchanged (−1.2 to+0.4%); initial historical+1–2% is not demonstrated
as a current regression here, but fresh~3% IQR does not exclude a small tradeoff.
Do not claim an exact CSV gain or erase its earlier source-pair observation.

JSON warm confirmation is near flat (−0.3 to+0.3%,~0.6% control), so its initial
+2.0–2.4% is not persistent evidence of a warm regression. Fresh confirmation is
adverse+2.1–4.9%, with2.7% current-control spread and~0.6–0.75% within-worker IQR.
It conflicts with the initial near-flat point; preserve BOTH. Earlier fresh
+1.8–1.9% debt stays OPEN/ACCEPTED, now provisional roughly2–5% under these
conditions, until the next coherent per-record/activation completion decision.
No causal attribution, silent debt closure or third confirmation campaign.
An inconclusive result is not a permanently rejected design. Future bounded
measurement correction remains allowed when new source evidence warrants it.
Text paired magnitude remains uncertain due identical-control spread.

Compilation is near unchanged CSV (−0.3 to+0.7%) and report approximately−1.6%;
no large compile improvement. Do not subtract compilation from fresh/warm medians.
The next design should remove a demonstrated call/frame or record-construction
representation, preserving semantic callbacks, cancellation/admission and cleanup.
This checkpoint is not completion of the whole-script performance objective.

observations.tgz SHA256:
`e7225426ddf4615a349455d4987b91683c5beb5248b4f4daf30b40c298b94c85`.
debt-observations.tgz SHA256:
`4d119394778a20ee9aebf724af42abdc2ce7799d1d5d5316c4dbd29033eda612`.
Raw catalogs, source/core identities, full samples, warmups, machine snapshots,
filesystem conditions, service exits and cleanup are preserved. Publication postponed.
