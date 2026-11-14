# Native primitive working storage: active prototype

2026-11-14 UTC, R359. Initial frozen `6c35b043` versus fully qualified
`47df7e19` / frozen `cbb254be`; four changed production inputs. **Development
prototype, not yet retained or fully qualified.** Publication/tagging remain held.

Private machine integers live beside existing object slots until observation.
This removes repeated boxes without R358's private heap cells or small-call
reservations. The first form uses a fixed 128-byte managed buffer; larger bound
helpers admit temporary capacity before allocation and keep it reserved through
cleanup. Stable immutable values are cached before aliases, argument spans,
collections, returns and ordinary-frame transfer. No primitive marker or borrowed
working span crosses suspension; ordinary frames/call ABI remain unchanged.

## Initial complete-script comparison

Quiet Azure F4as_v7, Ubuntu 24.04.4, EPYC 9V45, .NET 11 RC3, normal GC/tiering,
unchanged workers/auxiliaries/catalog. Four fresh processes per job, 5-second
full-request warmups (12 seconds for captured text), five rotating 0.5-second
batches. B734 completes all eight jobs in 303.156 seconds with image, output,
files, budget, quiet, joined-user and archived-evidence gates. No remaining RAM
fixtures or workers. Raw SHA256:
`a9f5c8e3083fb0b53a7c6abad1d3d97b52cd441a53fd586c1887178a4c3bf5cf`.

| Complete script | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| 100,000-add inside-range counter | −2.88..−0.51% | −4.63..+0.33% |
| 4,096-row CSV while validator, warm | −24.13..−22.03% | −23.85..−21.50% |
| Same CSV, compile-to-result | −24.39..−22.02% | −22.90..−20.56% |
| 4,096-row CSV fulfillment | −1.88..−0.27% | −1.96..0.00% |
| 3,072-line captured text | −5.61..+2.40% | −5.56..+3.39% |
| JSON helper summary | +2.10..+2.96% | +2.71..+4.87% |
| Repeated short/zero helpers | +6.40..+9.63% | +8.22..+11.21% |
| Helpers exposing each intermediate | +0.80..+5.96% | +1.01..+7.85% |

Ranges span all candidate/reference process-median pairings, not confidence
intervals. Warm clocks include mediated RAM-file I/O, validation and result
cleanup; exclude compilation, startup, fixture reset, protocol and final cache
disposal. Compile-to-result adds compilation/compiled-owner disposal. Wider CPU
includes reset/protocol. No CPython, startup, contention or tail-latency claim.
Text's 4.44% reference / 3.89% candidate spread remains uncertainty. Numeric
control movement is not attributed to this storage design: `range(...)` inside
that helper already promotes before its loop.

Four separate allocation processes, reversed role order, all goldens/budgets and
ordinary GC: CSV allocation falls approximately 40.87%
(5,131,350.625 to 3,034,111.625–3,034,115.125 B/run). Counter allocation is flat.
No CPU inference from allocation. The CSV while algorithm is the diagnostic
variant of the maintained text-digit validator; source/data are identical across
each runtime, and its algorithm change is not claimed as a runtime speedup.

2,416 focused checks pass across actual .NET 11/10, including stack/recursion,
integer alias/overflow/protocol behavior, actual I/O and cancellation, terminal
refusal, retained results and shared-budget cleanup. Thirty-two isolated CPython
3.13.2 controls match both execution APIs. Eight new public observation/wide-call
cases remain independent of the internal representation. The initial white-box
constructor compile failure and local verification-script path correction are
preserved; no semantic or containment assertion was weakened.

## Bounded refinement and remaining debt

CSV's substantial CPU/allocation gain justifies completing the representation;
the short/observed-helper and JSON regressions remain explicit debt. Do not add
cell pools or adaptive hotness thresholds. Source inspection finds unnecessary
storage in non-arithmetic `record_amount(record)`, and duplicated publication
retirement/store-checkpoint/PC logic. One bounded refinement will share the
authoritative publication path and compute storage need during the existing
eligibility scan, eliminating storage that cannot be written. This adds no
analysis pass and never skips allocation following refusal. Confirm affected
whole scripts/controls before a coherent full delivery checkpoint.

No unchanged general retiming or fixed percentage veto. The original results,
adverse samples, sources and failed conditions remain immutable. A publication-only
intermediate has focused/allocation evidence but no elapsed/CPU comparison; it
must not be described as measured faster. The broader representative performance
roadmap and independent regex track remain active.

[Initial timing](initial/timing-summary.json), [allocation](initial/allocation-summary.json),
[identity](initial/candidate-identity.json), [checks](initial/focused-checks.json),
[additional checks](initial/extra-checks.json), [sources/binary/raw/failures](initial/sources-checks-and-observations.tar.xz).
