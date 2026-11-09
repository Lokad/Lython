# Representative CPython checkpoint

R146/B503,2026-11-09. Measured commit65ed6cf7, fresh nonincremental Release core082aaa7c;
unchanged R136 worker22331/optional0.4 adapter, external catalog and protocol.
Four unchanged complete scripts: CSV validation, text counting, JSON-to-Markdown
report and protected file search. Same source/fixtures/stdout/files in both engines.
This comparison does not attribute a gain to R145 or compare class-callable shapes.

Twelve short jobs/250.847s on reference Azure F4as_v7, Ubuntu24.04.4, four AMD
EPYC9V45vCPUs,16GiB/no swap; actual.NET11RC3 and isolated CPython3.13.16. Three
independent roles: CPython and two current Lython processes. Ordinary GC/tiering,
five-second warmup each, seven rotated150ms rounds. Original observations, process
CPU, identities and variable controls remain. Ranges span two Lython medians,
not confidence intervals; descriptive status percentages are not acceptance vetoes.

| Script | Compile plus run, Lython / CPython | Warm, Lython / CPython |
| --- | ---: | ---: |
| CSV validation | 1.13–1.17×, variable | 1.15–1.17× |
| Text helpers | 1.49–1.51× | 1.56–1.60×, variable |
| JSON report | 1.35×, variable | 1.37–1.38× |
| Protected search | 1.34–1.35×, variable | 1.61–1.63× |

Compilation alone is roughly1.05–1.09× for CSV/text/report and0.95× for search.
The [guide](../../../docs/BENCHMARKS.md) contains absolute times, inputs, controls
and all three boundaries. Search has2,049 physical lines and exits at1,025; the
old input label counted visited lines. Script/input bytes are unchanged. Physical
CSV lines also include quoted record newlines; record counts remain distinct.

Warm includes mediated RAM-file I/O, full stdout/file digest checks and prior-result
cleanup. Compile includes construction/disposal under the supplied allowance;
compile-to-result combines those phases. Startup, fixture staging/reset/protocol
and final cache teardown are excluded. CPython retains imports; Lython isolates
execution imports and mediates filesystem capability/path checks. Pending I/O,
embedding tails and large-file/tree throughput remain unmeasured.

Source65ed's full delivery42586293361 found one real lazy-type/module-super
classification regression and109 raw-module fixture cases per OS; later10/Stress/
package gates were skipped. Corrected58d3ec0f passes200 internal/752 public focused
checks on each actual framework; full corrected delivery42588611410 is pending.
The changed compiler fact and fixture setup do not relabel these recorded timings
as58d3. [Correction evidence](../2026-11-09-callable-consolidation/README.md).

All quiet/identity/output/ownership/Closed/join/RAM-unmount/archive-under-lease/local-
SHA gates pass. Archive5b542be40238fe30744f0b4ba889aeed184680e45a46fbbe5b07024a8dbfc655.
Terminal cached, no users/fixtures. No floor/headroom relaxation or space recovery.
Initial catalog-link setup refusal and two continuation-script errors occurred
before worker launch/timing; cleanup unmounted their fixture, their logs survive,
and successful setup reverified the idle lease, exact catalog/core/all auxiliaries.
No failed observation or earlier adverse result is discarded.

[Summary](summary.json), [raw observations and exact core/tooling](observations.tgz),
[identity](identity.json), [source proof](core-source.json), [terminal](terminal.json),
[prior guide/all displaced rows](prior-BENCHMARKS.md), [preparation and original setup failures](preparation.tar.xz).
