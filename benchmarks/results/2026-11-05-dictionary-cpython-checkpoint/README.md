# CPython checkpoint after final list/activation and dictionary construction

2026-11-05. Retained source **e958c9b1**, frozen core **1f5919c0**, previous external
**a8e476bf/c6d71108, core402ae9f1**, independent identical-current control and
CPython3.13.16. Full e958 delivery42005719439 is GREEN: Windows/Ubuntu, actual
.NET11/10, original stress on both targets and installed Release consumers.
[Terminal receipt](../2026-11-05-native-dictionary-backing/delivery-ci-status.json)
is cached. Publication postponed; whole-script performance work remains open.

Quiet dedicated Standard_F4as_v7, four AMD EPYC9V45 vCPUs/16GiB/no swap,
Ubuntu24.04.4. SDK/runtime11.0.100-rc.3.26522.148/11.0.0-rc.3.26522.148.
CPython3.13.16/GCC13.3 PGO/LTO, ordinary GIL/GC, `-I -S -X utf8`, no JIT/debug.
Normal worker GC/tiering, separate processes for each engine/job. Existing
protocol2,5s warmup/engine/job,7 rotated approximately150ms samples, reversed role
order for later jobs. **466.52s/17jobs** within540s owned service. No new framework.

Seven complete scripts warm/fresh, CSV/report compilation and one cache-drain CSV
warm diagnostic. Invocation, mediated RAM-file I/O, complete stdout/files and
prior-result cleanup are timed. Fresh adds Compile/Dispose; compile-only excludes
execution/I/O. Reset, startup, transport, response hashing and final cache/process/
fixture teardown excluded. Final result remains funded through response delivery.
All Ready/core/runtime/catalog identities, independent full goldens, quiet
endpoints, Closed/exit0/joins, fixture removal and free lease pass.

Both Lython source images use identical **b94fa98c** worker bytes beside their
core, with identical auxiliary hashes. Tool-only95c10298 adds a final zero shared-
reservation assertion after all cached compilations/results are disposed; it
changes no clock boundary.124 protocol/file/supervisor cases per actual11/10 pass.
Each measured Lython worker's successful exit therefore also checks final zero.

| Fresh complete script | Lython ms | CPython ms | Lython / CPython | Current spread / candidate IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV/native helpers,4096records |4.764|3.476|1.37×|1.69% /1.62%|
| CSV/ordinary helper,4096records |5.296|3.375|1.57×|0.67% /1.15%|
| Text/helpers,3072lines |2.745–2.821|1.270|2.16–2.22×|2.71% /0.65%|
| JSON-lines/helpers,4096records |4.790–4.972|4.212|1.14–1.18×|3.67% /0.61%|
| JSON-to-Markdown,3072records |3.608–3.705|2.474|1.46–1.50×|2.67% /2.88%|
| Config edit,1024paths |0.266|0.253|1.05×|0.06% /0.88%|
| Long CSV,16records |0.396|2.439|0.16×|0.57% /1.61%|

Warm ratios: nativeCSV1.34×, ordinaryCSV1.60×, text2.20×, JSON1.13–1.16×,
report1.50–1.54×, config1.04×, longCSV0.11×. Warm nativeCSV CPythonIQR4.29%
limits ratio precision. Ranges show identical-source medians, not confidence
intervals. CSV has4096 logical records, including quoted newlines; JSON has4096
nonempty records plus1024 blank lines. Catalogs/goldens preserve exact input bytes.

Compile-only CSV69.62µs/54.61µs=1.27×, report50.30µs/37.79µs=1.33×;
candidateIQR1.61/2.04%, current spread.04/.55%. Do not subtract fresh/warm medians.
Ordinary-function CSV is a distinct script with ordinary Python helpers in both
runtimes, not a one-variable measurement of call cost.

## Coupled source effects, uncertainty and next work

| Complete job versus previous402 | Current / identical control elapsed delta | Current spread |
| --- | ---: | ---: |
| Native CSV warm |−4.40% /−3.10%|1.36%|
| Native CSV fresh |−3.92% /−2.30%|1.69%|
| Ordinary CSV warm |−5.11% /−6.62%|1.59%|
| Ordinary CSV fresh |−6.15% /−5.53%|0.67%|
| Text warm |−3.22% /−1.84%|1.43%|
| Text fresh |**+1.14% /−1.60%**|2.71%|
| JSON warm |−5.76% /−3.61%|2.29%|
| JSON fresh |−0.11% /−3.77%|3.67%|
| Report warm |−7.01% /−4.51%|2.69%|
| Report fresh |−8.02% /−5.56%|2.67%|
| Cache-drain CSV warm diagnostic |−9.55% /−9.32%|0.25%|

The diagnostic constructs8192 integer-key entries, drains them in reverse and
then executes the unchanged CSV script/full goldens. Current7.083/7.101ms,
previous7.830ms, CPython3.758ms (about1.89×); it is not a general large-map claim.
Independent processes remove shared CLR/JIT state between ALC images. This does
not prove shared state caused the original B2656.62/9.16% drift. Those conditions
and adverse initial report allocation remain preserved. Coupled gains do not
isolate D37 versus D40 or establish additive CPU improvements.

Config/long-field CSV/compilation are near unchanged. Fresh text is inconclusive;
do not select its favorable control. Report has favorable direction with2–3%
dispersion limiting magnitude. Warm JSON favors current, fresh magnitude remains
control-limited. D34's accepted roughly1% text component debt is not isolated or
proved fixed. Preserve its original adverse points and B260's conflicting JSON.
No automatic repeat is necessary to establish substantial remaining text/call
gaps. No startup, real suspended I/O, heap/RSS, embedding latency or causal CPU
claim. Next design must eliminate actual work/storage and preserve semantics,
effective cancellation, terminal refusal and explicit ownership.

[Pre-measurement contract](pre-measurement-plan.md), [identities](identity.json),
[all medians/distributions](summary.json), [exact source/catalog/raw/gates](observations.tgz),
[archive receipt](archive-receipt.json), [validation](validation.json),
[worker checks](tooling-validation.json), [available logs](worker-validation-logs.tgz).
Archive SHA256 `0cf830c7a8edaa6ad36b25f8e6ec74f15644135d7d7b171b455c78b4fddd37e0`.
No failed/timed-out/discarded job in this collection. Bounded immutable-core
hardlinks reclaimed22.2MB before staging, preserving every path/byte/mode and
executable placement; [maintenance receipt](hardlink-receipt.json).
