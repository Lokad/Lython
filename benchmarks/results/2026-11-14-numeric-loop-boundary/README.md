# Numeric loop execution boundary

2026-11-14UTC, R356. Exact fully qualifiedf5817a15/frozen9cfb7d5b on the
quiet AzureF4as_v7 VM, .NET11RC3 and stockCPython3.13.16. A helper sums the same
100,000range values, constructing range inside or outside the helper. No file I/O.
Warm guest clocks include stdout validation and result cleanup; compilation,
startup, fixture reset/protocol and final cache disposal are excluded. Wider
process CPU is retained separately. These **source variants are diagnostics**,
not a runtime source-pair speedup or representative-script replacement.

| Range construction | Lython warm ms | CPython warm ms | Lython/CPython |
| --- | ---: | ---: | ---: |
| Inside helper |5.162–5.194|1.698–1.715|3.01–3.06×|
| Outside helper |4.741–4.748|1.670–1.683|2.82–2.84×|

Two fresh processes per engine/case,5s full-request warmups,five0.5s rotating
batches. Lython process spread0.61%inside/0.15%outside, CPython0.95%/0.76%;
maximum sample IQR3.13%. B729completes71.399s with every identity, output, budget,
quietness and joined-user/archive gate. Local warmed allocation is~7.52MB/run
for either variant; cleanup restores the compilation budget baseline. This does
not establish allocation's CPU share or embedding tail latency.

Moving range construction outside saves roughly8–9%in Lython; the source confirms
range is outside closed native calls and ordinary calls can end native execution.
That boundary does not explain most of the remaining gap. General native reentry
is a secondary opportunity, not the selected redesign and not permanently banned.

One additional5s199Hzcpu-clock/fp owning profile of the outside variant, after
8guest-second warmup, yields1,033guest leaves/65unknown/zeroambiguous. Exact
addition owns505nearest leaves;185leaves are in BigInteger.Add,55in its limb
constructor,240in AddExactIntegers. NativeRunCore has191leaves; same-process JIT
map/disassembly uniquely match its6,619-byteTier1body. Counts include useful work
and are **not removable CPU shares**. Other-thread CPU/raw samples remain.
Every RAM byte was archived/verified before unmount; all workers/profiler joined.

Select the existing checked machine-integer kernel as the next coherent step:
share it with individual exact arithmetic before considering new value storage.
Keep exact overflow promotion, Python signed division/modulo/protocol behavior,
published aliases/identity and existing major-storage admission. No predicted
speedup or allocation reduction. The broader scalar/whole-script roadmap remains.

Preserved fine print: initial staging named local allocation-tool dependencies
absent from the comparison image and failed before mutation/launch. The corrected
identity retains the same core/data/collector. B730's raw receipt has a stale
R354fulfillment source label; actual catalog, Ready, request, record and identity
all identify the numeric outside-helper case. No failed condition is discarded.

[Decision](decision.json),[normal timings](summary.json),[owning CPU](cpu-attribution.json),
[exact native groups](native-instruction-groups.json),[original tools/raw archives](tools-catalogs-and-observations.tar.xz).
