# Current coupled source / CPython checkpoint — B195

2026-11-04. Measured production **2d46dc8e**, frozen precommit `ba48576af397ef62ce62a493b7ab4eb2ff845874fe44c512e6e5ceadf8e63783` built from9a4af031 plus the complete archived B194 patch. Source hashes match committed production; actual SourceLink/binary identity is retained, never relabeled. B194 runtime source is unchanged by the subsequent writer-test correction. Full delivery41760386280 has a Windows writer-fixture timeout; do not claim it green. New exact-source delivery follows the finite fixture repair.

Baseline pre-loop B166, qualified2323cceb, frozen `3bed411efcd6e008fc04bca8634d32be64bafc71742509ff0192e8eee86ab8bc`. Two unchanged baseline images establish one DIRECT coupled comparison spanning B171/B174/B178/B183/B186/B188/B190/B192/B194; do not sum isolated percentages. CPython3.13.16 GCC13.3 PGO/LTO, ordinary GIL/GC, no JIT/debug, -I -S -X utf8. .NET11RC3 normalGC/tiering, quiet AzureF4as_v7/Ubuntu24.04.4, fourEPYC9V45vCPU/16GiB/no swap.

## Original condition and bounded correction

Initial twelve blocks completed all semantic/hash/normal-close/exit0/join checks in253.323s owned, but final idle CPU6.4677% over0.5028s failed unchanged median3%/max5% thresholds. Original data stays UNQUALIFIED. No later quiet pass certifies that first run. Its raw samples/controls and failed machine gate are retained. Root free space was5.7MiB; only7,829,335bytes of exact-SHA-verified uploaded copies with matching local copies were removed. All frozen sources/images/evidence remain. No VM/filesystem/performance-policy tuning.

One same-bit bounded confirmation reverses preparation roles, warms each image5s (initial3s), uses the same9 rotating~200ms batches/1s settle, then waits5s after ALL worker/user cleanup before the existing ending gate. Thresholds unchanged. Confirmation completes all12 blocks, quietbefore/after, identities/goldens/balances/normal closes/exit0/joins/empty guest fixtures. Capture366.565s/owned service367.180s (above the~350s estimate; below planned420s hard bound), not a10-minute lane. No third unchanged campaign now; new evidence can justify later bounded correction.

## Included work

Existing protocol2, five complete file scripts synchronous warm and fresh compile-run, CSV/report compilation separately. Persistent worker/catalog/raw evidence; only guest fixtures on tmpfs. Clocks include invocation/setup, mediated real I/O, full stdout/file checks and previous-result disposal; fresh includes per-invocation compilation/disposal. Fixture reset/staging, startup, response hashing, final cached compilation/last-result/process/fixture teardown outside engine clocks, inside owned wall bound; all cleanup/users joined. Compile-only excludes execution/I/O. No phase subtraction, equal-heap/RSS/embedding-latency or genuinely suspended-I/O claim.

Inputs are unchanged versus B189 (source/fixture/output/file SHA identities checked).
CSV/JSON each contain**4,096 records** plus blank lines;3,072records are accepted.
Text/report each have3,072lines/records, config1,024paths. Counts/bytes are verified
from decoded fixture bytes using actual CSV/JSON parsing; physical newlines and
BASE64-encoded JSON lengths are not record/file sizes. See input-validation.json.


## CPython and direct coupled observations

Ratio=current Lython elapsed/CPython elapsed (>1 slower). IQR=interquartile range/median. Quiet endpoint gates and controls support diagnostic observations, not a guaranteed multiplier or all-sampling performance certificate.

| Case / phase | Lython ms | CPython ms | Ratio | Delta versus old roles | Old-role spread | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| csv-summary-helpers / warm | 5.162352 | 3.347038 | 1.542× | -1.63% / -5.18% | +3.74% | 0.98% / 0.88% |
| csv-summary-helpers / compile-run | 5.298590 | 3.527750 | 1.502× | -0.51% / -3.90% | +3.53% | 1.03% / 0.71% |
| text-summary-helpers / warm | 2.978601 | 1.165775 | 2.555× | -17.05% / -14.86% | -2.57% | 0.34% / 0.43% |
| text-summary-helpers / compile-run | 3.339031 | 1.312753 | 2.544× | -9.37% / -6.97% | -2.58% | 0.78% / 0.39% |
| json-summary-helpers / warm | 5.334882 | 4.075102 | 1.309× | -5.70% / -3.13% | -2.65% | 0.41% / 0.46% |
| json-summary-helpers / compile-run | 5.489652 | 4.210677 | 1.304× | -7.30% / -5.60% | -1.79% | 0.77% / 1.14% |
| report-render / warm | 5.091107 | 2.572805 | 1.979× | -8.64% / -9.52% | +0.98% | 2.34% / 1.43% |
| report-render / compile-run | 5.268658 | 2.742419 | 1.921× | -6.53% / -8.12% | +1.73% | 3.82% / 1.66% |
| config-edit / warm | 0.195833 | 0.184637 | 1.061× | -3.57% / -8.43% | +5.30% | 0.64% / 0.44% |
| config-edit / compile-run | 0.287078 | 0.259220 | 1.107× | -4.16% / -3.04% | -1.16% | 1.84% / 0.46% |
| csv-summary-helpers / compile | 0.086175 | 0.054604 | 1.578× | -0.52% / +1.10% | -1.60% | 1.33% / 0.25% |
| report-render / compile | 0.059952 | 0.037998 | 1.578× | +0.40% / +1.13% | -0.72% | 1.12% / 0.26% |

**Debt decision:** earlier provisional fresh-JSON+2–5% is CLOSED under this current coupled condition: fresh−5.6..7.3% versus both old roles,1.8%old-role spread,0.8%current/1.2–1.4%oldIQR. Keep every earlier B189 adverse/uncertain point; this observation does not explain its original cause or promise every deployment. WarmJSON−3.1..5.7% with2.7%old spread limits magnitude.

Text improves14.9..17.1%warm /7.0..9.4%fresh versus both roles,2.6%old spread. Report improves8.6..9.5%warm /6.5..8.1%fresh, with1.0/1.7%old spread and2.3/3.8%currentIQR. CSV remains near unchanged/uncertain (old controls3.5–3.7% apart, old historical+1–2% cost not established as current or universally excluded). Config fresh−3.0..4.2% with1.2%old spread; warm magnitude limited by5.3%old spread. Compilation near unchanged relative to old controls. Do not attribute all coupled gains to B194 alone or add earlier separate gains.

Primary CPython gaps remain: fresh CSV1.50×,JSON1.30×,text2.54×,report1.92×,config1.11×; compile1.58×. B194 possible warm async-text1–2% and asyncJSON uncertainty remain ACCEPTED/OPEN; synchronous CPython data cannot close them. Whole-script performance remains OPEN; continue justified substantive runtime work, not an unchanged qualification campaign.

- Initial UNQUALIFIED archive SHA256: `7c3e399dec3cd0367b2a1d55b8aa3d338e292d8ddd0c4b9f1318edd561a35023`.
- Confirmed archive SHA256: `c9a3f580197da77b6d4b1fb73738170f665f74888a79b2a70ed308a5f3773a8f`.
- Exact catalogs/source identities/collectors/pre-plans, warmup/samples, both endpoint gates and cleanup outcomes remain in raw archives and summaries. Publication/tagging postponed.
