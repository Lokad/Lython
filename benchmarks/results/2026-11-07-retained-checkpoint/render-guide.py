from pathlib import Path
import json
p=Path(__file__).parent
data=json.loads((p/'summary.json').read_text(encoding='utf-8'));assert data['complete']and len(data['timing'])==23
rows={(r['case'],r['lane']):r for r in data['timing']}
ordinary=[('scripts.csv-summary-functions.large','CSV summary/ordinary helper','4,096 records / 57,361 B'),('scripts.text-summary-helpers.large','Text summary/helpers','3,072 lines / 35,840 B'),('scripts.json-summary-helpers.large','JSON-lines/helpers','4,096 records / 110,592 B'),('scripts.report-render.large','JSON-to-Markdown report','3,072 records / 189,441 B'),('scripts.config-edit.small','Configuration read/edit/write','16 paths / 181 B'),('scripts.file-search.large','Protected file search','2,049 lines; exits after 1,025 / 42,010 B'),('scripts.file-manifest-helpers.large','Two-file copy/checksum/manifest','2 files / 4,107 B')]
affected=[('scripts.csv-summary-functions.large-padded-integers','CSV with padded numeric fields','4,096 records / 191,505 B'),('scripts.json-summary-helpers.large-integer-strings','JSON with numeric strings','4,096 records / 278,528 B')]
def table(lane,cases,inputs=False):
 text='| Workload | '+('Input | 'if inputs else'')+'Lython | CPython | Lython / CPython | Status; control / Lython IQR |\n'
 text+='| --- | '+('--- | 'if inputs else'')+'---: | ---: | ---: | --- |\n'
 for case,name,size in cases:
  r=rows[(case,lane)];low,high=r['timeRangeMilliseconds'];scale=1000 if high<1 else 1;unit='µs'if scale==1000 else'ms'
  ratios=r['ratioRange'];spread=max(r['iqrPercent'][k]for k in ['candidate','control'])
  text+=f"| {name} | "+(f'{size} | 'if inputs else'')+f"{low*scale:.3f}–{high*scale:.3f} {unit} | {r['medianMilliseconds']['CPython']*scale:.3f} {unit} | {ratios[0]:.2f}–{ratios[1]:.2f}× | {r['status']}; {r['controlPercent']:.2f}% / {spread:.2f}% |\n"
 return text
old=Path('docs/BENCHMARKS.md').read_text(encoding='utf-8')
tail=old[old.index('## Improvements within Lython'):]
tail=tail.replace('It is not included in the CPython revision measured above.','It is included in this checkpoint; the source pair does not isolate its contribution.')
history=Path('benchmarks/results/2026-11-07-retained-checkpoint/earlier-source-pairs.md')
assert history.parent.is_dir() and not history.exists()
history.write_text('# Earlier source-pair account\n\nSnapshot of the previous benchmark guide at documentation revision f22d0346,\npreserved as historical evidence. Its primary table measured production8c9fd0d7.\nEach source pair has its own revision and measurement limits; none is a new\nmeasurement of the current checkout.\n\n'+tail.replace('../benchmarks/results/','../').replace('../benchmarks/EXPERIMENTS.md','../../EXPERIMENTS.md').replace('../benchmarks/HISTORY.md','../../HISTORY.md').replace('../benchmarks/README.md','../../README.md').replace('../tests/README.md','../../../tests/README.md'),encoding='utf-8',newline='\n')
tail='''## Improvements within Lython

CPython is the primary external baseline above. These separate source pairs
measure affected workloads; percentages are not additive or universal CPU gains.

| Change / affected work | Established result | Limits / evidence |
| --- | --- | --- |
| Native function returns / CSV, JSON | Allocation −19.92%/−19.31% | Elapsed remains diagnostic. [Source pair](../benchmarks/results/2026-11-02-native-function-returns/README.md) |
| Direct UTF-8 formatting | Allocation −8.67% on original source; controlled elapsed/CPU −5–8% | Original adverse elapsed result remains unexplained. [Confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md) |
| Completed CSV records | Allocation −7.31%/−6.55%; native-helper warm elapsed −7.72–8.04% | Controls mixed; independently mutable rows remain funded. [R25](../benchmarks/results/2026-11-07-csv-record-values/README.md) |
| Canonical formatted instructions / dynamic report | Allocation about −15.35%; warm elapsed −10–13% | CSV +0.13–2.36% possible debt. [R35](../benchmarks/results/2026-11-07-canonical-format-instructions/README.md) |
| Shared receiver methods / regex summary | Allocation about −29.47%; warm elapsed −8.64–10.80% | CSV/text/JSON controls variable; original adverse CSV retained. [R41](../benchmarks/results/2026-11-07-receiver-methods/README.md) |
| General UTF-8 integer parsing | Padded CSV warm −12.5–13.4%; numeric-string JSON −41.4–42.0% | Explicit affected inputs; ordinary controls mixed. [R45](../benchmarks/results/2026-11-07-integer-parsing/README.md) |
| Native integer width / numeric-string JSON | Allocation about −12.5%; warm elapsed −10.7–12.5% | Padded CSV +1.0–1.2% accepted debt. [R50](../benchmarks/results/2026-11-07-native-integer-width/README.md) |
| Native UTF-8 wildcard reports | Allocation −25%/−32%; fnmatch warm −12–13%, glob −16–17% | Ordinary JSON control +4–7% accepted unresolved debt; no matching-backend claim. [R51](../benchmarks/results/2026-11-07-wildcard-translation/README.md) |

Shared compiler-fact correction, annotation demand pruning and smaller executable
transport are also retained; their compilation/source benefits do not prove broad
warm CPU gains. [Earlier source pairs](../benchmarks/results/2026-11-07-retained-checkpoint/earlier-source-pairs.md)
preserve detailed revisions, regressions and completed qualification. The original
failed B406 refresh and its partial data remain beside the corrected checkpoint;
it was not silently discarded or promoted.

[Experimental history](../benchmarks/EXPERIMENTS.md) retains rejected designs,
adverse timings and allocation evidence. [Benchmark tooling](../benchmarks/README.md)
describes qualification procedures; [tests](../tests/README.md) cover semantics,
ownership, cancellation and installed consumers. Timing does not prove containment.
'''
text='# Performance against CPython\n\n'+table('compile-run',ordinary,True)+'''
**2026-11-07; 0.11 development source `55e510ff`; CPython 3.13.16; .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio = Lython elapsed / CPython elapsed;
above one means slower.** Ranges span two independent identical-build process
medians, not confidence intervals. Control is their absolute difference/candidate;
IQR is the larger Lython-role interquartile range/median. Diagnostic/Variable label
short observations, not design acceptance thresholds. Inconclusive cases stay visible.

Frozen core `83af401e` matches the recorded 561-file production inventory for this
commit; informational SourceLink predates the commit. Qualification status is
recorded with the [source and evidence](../benchmarks/results/2026-11-07-retained-checkpoint/README.md).
The normal optional 0.4 adapter and worker are unchanged; this is not new producer
certification. Earlier tables measured `8c9fd0d7`, preserved with their
[historical observations](../benchmarks/results/2026-11-07-integer-cpython/README.md).

## Warm execution, compilation and startup

Warm execution reuses the compiled script. The last two inputs explicitly exercise
general integer conversion and do not replace ordinary CSV/JSON. Their compilation
and compile-to-result times were not refreshed in this checkpoint.

'''+table('warm',ordinary+affected)+'''
Compilation alone includes construction and disposal under the shared allowance:

'''+table('compile',ordinary)+'''
**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** A combined checkpoint does not isolate each change's CPU benefit,
repay historical regression debt or explain earlier adverse controls. It describes
the recorded source, not future HEAD. Search exits after 1,025 of 2,049 lines; the
manifest copies only two files/4,107 B. These are not large-file throughput tests.

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 x86-64, four AMD EPYC 9V45 vCPUs,
one thread/core, 16 GiB/no swap. CPython GCC 13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime 11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal GC/tiering,
quiet endpoints; five-second warmup per role/job, seven rotated 150 ms rounds.
'''+f"The 23-job CPython checkpoint completed in {data['elapsedSeconds']:.2f} seconds.\n"+'''Wildcard source-pair jobs are reported separately. Every worker joined and fixture
was removed; persistent archives were verified under the collection lease.

Both engines run identical scripts/fixtures and check complete stdout/file digests
on every invocation. Efficient host overrides mediate Lython file access. RAM
fixtures include guest reads/writes; staging/reset are outside elapsed clocks.
Warm includes invocation and prior-result cleanup; compile includes construction/
disposal. Protocol transport and final cached teardown are excluded. CPython
retains imported modules; Lython isolates execution imports. This is an embedding
comparison. Tables use synchronous Run; historical async observations use completed
mediated I/O and do not establish pending-I/O latency.

'''+tail
Path('docs/BENCHMARKS.md').write_text(text,encoding='utf-8',newline='\n')
print('Rendered seven fresh/compile and nine warm CPython rows from one coherent checkpoint.')
