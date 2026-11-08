# Current integer-loop diagnosis

2026-11-08, committed `05790317` / frozen core `13107e6b`; the old numeric control
is `7265538c` / core `1aa68ce9`. R110 record capacity does not predict a numeric-loop
gain. Source0579 is fully qualified in [cached CI42502610937](../2026-11-08-json-record-capacity/delivery-ci.json).
Same maintained `loops.integer.large`,16384iterations, complete stdout134209536;
independent CPython golden unchanged. No new fixture or framework.

Existing short warm micro: one-second warmup, seven rotated25msrounds, normal GC/
tiering, external30second deadline. .NET11 RC3 / CPython3.13.16 on quiet dedicated
F4as_v7/Ubuntu24.04.4/four EPYC9V45vCPUs/16GiB/no swap, same worker7cf/optional0.4.

| Engine | Median per complete invocation | IQR / median |
| --- | ---: | ---: |
| Old Lython control |713.390µs|0.92%|
| Current Lython |700.968µs|1.65%|
| CPython |596.612µs|0.14%|

Current Lython/CPython≈1.17x in this short diagnostic. It does not show the old large
basic-loop gap or establish a R110 numeric speedup, confidence interval or general
multiplier. Quiet-before passed; quiet-after was not recorded in the failed combined
collection. Keep that limitation. Warm reuses code, includes invocation/output
validation/prior-result cleanup, excludes compile/startup/protocol/staging. No file I/O.

The first CPU capture failed before warmup because the collector required
actualFilesSha256, which a no-file WorkerResponse legitimately omits. Completed
micro was preserved and never repeated. The stopped worker, raw/maps/IPC metadata
and failure journal were archived under lease and independently verified; owned
RAM was unmounted after users ended. One corrected **profile-only** capture compares
optional response/case file digests, retaining checks for actual file fixtures.
The original stage-helper name also refused before any new worker; sources remain.

Corrected capture: six-second guest warmup/two-second settle, six-second wall
CPU-clock/FP199Hz,64completeinvocations/request. PerfMap/JIT diagnostics enabled;
not normal-settings elapsed.6.012guest-clock seconds/5.92guest CPU seconds,
1170samples/63unknown leaves(5.38%). Both quiet endpoints/full stdout/Ready/core/
aux/Closed/exit/join/maps/fixture/archive/local gates pass; no users or RAM remain.

| Sampled work | Leaf samples | Interpretation |
| --- | ---: | --- |
| CompleteLocalResult |287(24.53%)|Publication, cleanup/checks and binding work; not all removable overhead |
| Instruction prefix |234(20.00%)|Includes useful inlined work/checks |
| Range iterator |146(12.48%)|Native iteration/value construction |
| AddExactIntegers |118(10.09%)|Exact arithmetic/result construction |
| Input instruction handling |108(9.23%)|Operand/protocol routing |
| Augmented assignment |48(4.10%)|Arithmetic dispatch |
| BigInteger(long) constructor |35(2.99%)|Value construction |

Inclusive groups overlap and inlining/unwind loss limits attribution. No standalone
cancellation sample is not proof that inlined checks cost nothing. The first
offline memory classifier incorrectly matched a governor type in numeric method
signatures; corrected to actual owner methods and retained the original classifier.
This profile does not justify universal tagged slots or a new frame/arena engine.

Source follow-up identifies bounded removable work: StoreLocalValue unconditionally
looks up and classifies its compiler name before discovering no namespace/mirroring
requires it. Moving that pure metadata work into the real namespace branches may
reduce the publication cost. Preserve cells, synthetic names, dynamic globals/
locals, cleanup order, lifetime fences, current cancellation and memory refusal.
Select and test that change before predicting a speedup; this capture alone does
not establish a particular saved percentage.

[Micro receipt](micro.json), [profile attribution](attribution.json), [identity](identity.json),
[profile receipt](profile-receipt.json), [all raw, sources, failures and corrections](observations.tar.xz).
