# Representative CPython checkpoint after CSV input delivery

2026-11-05. Retained production9c1d7e74, documented74de3388, frozen core6cb05f8e.
Includes executable target instructions, owned native CSV input and quoted-EOF
correction. The slower D46 call-operand prototype is absent. Qualified7dcc3b48/
a4ce2b48 is the historical source reference; identical-current control is a
separate process. Primary external reference: CPython3.13.16.

440.59s/16jobs, all quiet, source/fixture/golden/library identity and lifecycle
gates pass. Every worker returns Closed, all final Lython reservations are zero,
all processes join successfully and fixture directories are removed. The exact
archive hash is in archive-receipt.json; observations.tgz contains every raw
sample, warmup, machine gate, frozen catalog, identities and source snapshot.

## Current comparison

Two independent identical-build medians, ranges rather than confidence intervals.
Ratio=current elapsed/CPython elapsed. Ordinary helper CSV uses ordinary functions
in both runtimes. Long-field CSV has16records/524717B; ordinary CSV4096/57361B.

| Complete script / phase | Lython ms | CPython ms | Ratio |
| --- | ---: | ---: | ---: |
| Native CSV/fresh |4.583–4.616|3.479|1.32–1.33×|
| Ordinary helper CSV/fresh |5.128–5.281|3.410|1.50–1.55×|
| Text/fresh |2.583–2.605|1.292|2.00–2.02×|
| JSON-lines/fresh |4.728–4.833|4.212|1.12–1.15×|
| Report/fresh |3.652–3.670|2.492|1.47×|
| Config/fresh |0.264–0.266|0.255|1.03–1.04×|
| Long-field CSV/fresh |0.346–0.348|2.464|0.14×|

Fresh includes compilation and one execution in an already-running host. Warm
reuses compiled code: nativeCSV1.29–1.32×,ordinaryCSV1.53–1.54×,text2.18–2.27×,
JSON1.11–1.13×,report1.53–1.55×,config1.02×,longCSV.09×.
Compile-only CSV69.959–71.040µs versus54.995µs (1.27–1.29×), report49.997–52.087µs
versus37.831µs (1.32–1.38×). Compilation is measured independently, not inferred
by subtracting fresh/warm medians. Startup is unmeasured.

Fresh ordinaryCSV current spread2.89%,JSON2.19%; warmtext4.45%,warmJSON2.12%,
reportcompile4.18%. Warmreport current IQR reaches3.37%,freshreport2.47%, compile
controls3.11/3.15%. Preserve these uncertainties rather than advertising precise
small gains or a universal multiplier. Fresh text remains about2×CPython; the
long-field result is not representative of ordinary records.

## Coupled changes against earlier Lython

Against qualified7dcc/a4ce, ordinaryCSVwarm favors current by2.6–3.4%; nativeCSV
fresh2.0–2.7%. Fresh ordinaryCSV3.2–6.0% has control-limited magnitude. LongCSVwarm
17.1–18.4% lower, fresh13.2–13.8% lower. Textwarm−2.28/+2.08% and freshJSON+1.66/
−.56% are inconclusive; retain adverse roles. Report/compilation dispersion limits
small claims. These do not isolate each component or close earlier~1% text debt.
Previous checkpoints, source pairs and rejected designs retain their own sources
and measurements; percentages are not additive. No allocation/causalCPU/RSS or
genuinely suspended-I/O latency claim.

## Boundary, machine and qualification

Same frozen comparison workerb94fa98c and auxiliary libraries beside each source.
Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4, four EPYC9V45 vCPUs/16GiB/no swap,
.NET11RC3 SDK/runtime11.0.100-rc.3.26522.148/11.0.0-rc.3.26522.148. CPython3.13.16
PGO/LTO, normal GIL/GC, no JIT/debug, -I -S -X utf8. Normal worker GC/tiering.
Seven scripts each warm/fresh plus nativeCSV/report compile-only,5s warmup/engine/
job and seven rotated~150ms samples. Existing540s process bound,<=480s target.

Clocks include invocation, actual mediated guest I/O on /dev/shm, complete stdout/
file validation and prior-result cleanup. Fresh also compiles/disposes each
invocation. Fixture staging/reset, startup, transport/response hashing and final
cached-owner/process/fixture teardown are excluded. Final result remains funded
through response delivery; final owners clean up outside that batch timer. No
allocation retention, admission retry, forced GC or relaxed containment controls.

D44 source723b9638 full42025698023 is GREEN/all8/cached. D45 correction has focused
107internal/366public CSV per actual.NET11/10 and18 final escaping-result checks;
retained-source rebuild31.87s/zero warnings/errors,7internal/142public post-restore
checks per target pass. Full retained-source74de3388/42029990429 is GREEN: all eight
Windows/Ubuntu, actual.NET11/10, original stress and installed-consumer gates
verified in delivery-ci-status.json. Terminal cached; never query again.
Publication is postponed. No new benchmark framework or producer qualification.
