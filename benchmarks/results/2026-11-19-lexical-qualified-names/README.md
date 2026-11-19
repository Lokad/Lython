# B1049 and B1050 lexical function qualified names

Prototype `cb5f812b7` records lexical paths during scope preparation.
Functions use the path for a lazily funded UTF-8 name, removing runtime
owner selection and the enclosing-frame walk. It fixes nested classes,
functions rebound as methods, explicitly global declarations, lambdas in
comprehensions, and lazy generic bounds/defaults. Both local targets passed
276 focused cases. The tested lexical paths and generic expressions agree
with CPython through both execution APIs.

The rule-registration report constructs 2,048 annotated closures and
counts their qualified names while executing the ordinary rule body. Its
allocation falls **5.56%**, from 3,245,040 to 3,064,696 bytes, with equal
accounted peak. The other five cases' allocation is flat. The report's
initial warm elapsed improves but CPU is mixed, and both span zero on
repeat. Forwarding initially slows and moves in both directions on repeat.
Annotated-rule warm timing remains adverse in some pairs, with a 6.37%
spread between identical candidate workers on repeat. The prototype stays
private. A further CPython probe reports that separate instances from one
declaration share the qualified-name object. This implementation caches
each UTF-8 name separately, leaving that case open.

| Collection and workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1049 Unannotated rules, warm | −4.33…+1.14% | −3.76…−0.31% | 4.38 / 1.28% |
| B1049 Annotated rules, warm | −0.64…+4.20% | −0.43…+3.94% | 2.61 / 2.20% |
| B1049 Qualified-name report, warm | −1.92…−0.84% | −2.62…+2.13% | 0.38 / 0.72% |
| B1049 Qualified-name report, compile-to-result | −2.63…+0.32% | −1.45…+0.69% | 2.78 / 0.24% |
| B1049 Expanded forwarding, compile-to-result | +1.23…+3.10% | +0.58…+2.53% | 0.18 / 1.67% |
| B1049 Positional calls, warm | −1.89…+1.24% | −2.17…−0.34% | 1.71 / 1.46% |
| B1049 Generated validator, compilation | −1.98…+0.88% | −1.01…+2.07% | 0.96 / 1.94% |
| B1049 Generated validator, compile-to-result | −1.10…+1.73% | −1.10…+0.80% | 0.48 / 2.38% |
| B1050 Expanded forwarding, compile-to-result | −1.73…+2.35% | −2.65…+2.07% | 1.97 / 2.13% |
| B1050 Qualified-name report, warm | −2.80…+1.84% | −3.36…+1.05% | 1.57 / 3.16% |
| B1050 Annotated rules, warm | −0.42…+7.26% | −1.59…+5.07% | 1.26 / 6.37% |

Both complete 32-file images verified against the same six CPython goldens.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
The source pair is `c8e749318` and `cb5f812b7`. The repeat used the same
images and cases with a different worker order. Ranges cover all four
pairings of process medians. Negative changes favor the candidate.

The eight initial jobs took 364.51 seconds. The three confirmation jobs
took 139.98 seconds. All 220 measured batches verified their goldens.
Workers joined, guest fixtures were removed, and raw archives were
verified under the quiet-VM lease. Warm includes mediated I/O, golden
checks and previous-result cleanup. Compile-to-result adds compilation
and compiled-owner cleanup. Fixture reset, protocol, startup and final
cache teardown fall outside guest elapsed. CPU covers the wider request.

[B1049 summary](summary.json) · [B1049 raw](observations.tgz) ·
[B1050 summary](confirmation/summary.json) · [B1050 raw](confirmation/observations.tgz) ·
[Exact core](measured-core.dll.gz) · [Sources, checks and procedures](checks-and-procedures.tar.xz).
