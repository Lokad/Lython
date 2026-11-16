# Generated configuration/report CPython checkpoint

R444/B847,2026-11-16UTC,qualified312f5911/frozen0443b5cf versus stockCPython3.13.16.
Runtime source is unchanged throughc18babe9. Configuration edits a7,177BJSON file
with1,024paths and writes it back. Reporting reads189,441BJSON/1,024records,calls
a small formatting helper and writes Markdown. Existing catalog scripts and CLR
goldens; frozen-worker/CPython protocol verification passes before VM staging.

| Script | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | compile | 0.036–0.036 ms | 0.035–0.035 ms | 1.04–1.05× | Diagnostic; controls L/C 0.23/0.07%; IQR≤1.01% |
| Configuration edit | warm | 0.179–0.182 ms | 0.185–0.185 ms | 0.96–0.98× | Diagnostic; controls L/C 2.10/0.01%; IQR≤1.37% |
| Configuration edit | compile-run | 0.240–0.245 ms | 0.255–0.255 ms | 0.94–0.96× | Diagnostic; controls L/C 1.92/0.10%; IQR≤0.67% |
| Report rendering | compile | 0.040–0.040 ms | 0.038–0.038 ms | 1.06–1.06× | Diagnostic; controls L/C 0.27/0.32%; IQR≤0.53% |
| Report rendering | warm | 2.774–2.790 ms | 2.327–2.361 ms | 1.18–1.20× | Diagnostic; controls L/C 0.57/1.46%; IQR≤2.04% |
| Report rendering | compile-run | 2.974–2.994 ms | 2.456–2.475 ms | 1.20–1.22× | Diagnostic; controls L/C 0.66/0.77%; IQR≤4.04% |

Above one means Lython is slower. All four independent process-median ratio
pairings are retained; ranges are not confidence intervals. Configuration is
near CPython with1.92–2.10%Lython control movement. Report execution remains about
18–22%slower; fresh-report IQR4.04%stays visible. Compilation is around1.04–1.06×,
so a heavier compiler is not justified by these observations. Prepared f-string
layouts and receiver arguments already exist in current source; do not add
duplicate metadata caches or call packets from historical review assumptions.
These are diagnostic external ratios,not causal earlier-source gains.

One identical six-case catalog36c0a528in all processes: original R437four cases
plus configuration/report. Only these two new cases are timed,not four unchanged
retests. Catalog initialization differs from R437and stays explicit. QuietF4as_v7/
Ubuntu24.04.4,fourEPYC9V45vCPUs,16GiB/no swap,.NET11RC3normal tiering/workstation
GC,CPython GIL/GC. Six phases200.960s,5s full-request warmups,five rotated roughly
0.5s batches. Full-image/golden/quiet/Closed/join/archive gates pass.

Warm includes Run,mediated RAM I/O,stdout/file goldens and prior-result cleanup;
excludes compilation/startup,fixture reset/protocol and final cache teardown.
Compilation includes compiled-owner disposal; compile-to-result includes compilation,
one execution and compiled-owner disposal. Independently warmed medians are not
additive. Wider process CPU includes reset/protocol. CPython native buffered files
do not provide equivalent capability/budget containment. No startup,pending-I/O,
heap/RSS,contention or embedding-tail measurement. All users joined and RAM fixtures
removed; archive SHAverified under lease,terminal cached. No retirement/floor change.

Current guide adds these six phases without changing R437observations. Original
historical guide is preserved below. The initial local catalog-generation attempt
used a stripped frozen image without RepresentativeScripts and failed before
producing any catalog or observations; generation uses the actual built tool/data,
then the frozen measured image verifies the concrete selected catalog. No timing
from that setup is discarded. User-facing runtime code is unchanged; publication
held and remaining text/search execution gaps stay open.

[Raw](observations.tgz),[phases/controls](summary.json),[identities](identity.json),
[procedure/catalog/preflight](procedure.tar.xz),[preceding guide](prior-BENCHMARKS.md).
