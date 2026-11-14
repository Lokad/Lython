# Captured scalar storage and publication

2026-11-14, R349: qualified `009d236a` / frozen `8ae1ae5f` versus
`71ba421c`. Retain a representation that removes per-update integer boxes and
three-instruction object transport for prepared captured arithmetic. An existing
authoritative cell keeps an exact machine integer; observation creates one stable
immutable identity. Older aliases remain unchanged and funded. General values,
mirrored namespaces, unbound cells and overflow retain ordinary operations.

The existing compiler preparation walk now handles dictionary and cell segments;
the dictionary-only file/method and proposed extra pass disappear. An unused
instruction field records the fact; instruction size and public API stay unchanged.
Both execution forms share the existing checked arithmetic kernel. Exception
boundaries, clean operand extents and cancellation-before-store stay explicit.
Three48Bcell creation/retention estimates consolidate into96Bfunding for metadata
and an observed current scalar. Earlier tiny-budget denial is accepted. This adds
no identity accounting, ownership policy, tracer, pool, arena or execution engine.

| Complete script | Allocation change | Warm elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: |
| Captured text counter,3,072lines /35,840B | −18.00% | −5.20..−7.60% | −4.81..−6.89% |
| Direct text, unchanged control | 0% | −2.60..−8.43% | −3.47..−7.91% |
| CSV fulfillment,4,096records /126,821B, control | effectively0% | −2.11..+0.57% | −1.68..+0.89% |

**Allocation reduction is established; CPU attribution is uncertain.** Captured
allocation falls545,989.75→447,725.75B across four fresh allocation processes.
Unchanged direct text also runs faster and has5.58%candidate process spread.
Captured process spreads are0.48%candidate/2.11%reference. Ranges compare every
pair of fresh process medians, not confidence intervals. Do not subtract control
percentages or claim that this change explains the entire elapsed difference.

An exposure diagnostic runs the same complete captured script with an assertion
reading the counter after every update. Both sources execute the identical changed
script and preserve output/files. Allocation is546,677.75→546,685.75B, just+8B.
Frequent observation necessarily retains per-update boxing; CPU cost for this
variant is unmeasured. This control has no local elapsed claim.

B720 uses the quiet F4as_v7 Ubuntu24.04.4/.NET11RC3 VM, normal GC/tiering,
unchanged worker/optional0.4adapter and identical source-pair scripts. Four fresh
processes/job,12s complete-request warmups, five1s batches. Warm clocks include
mediated RAM I/O, output/file validation and prior-result cleanup; exclude
compilation/startup, fixture reset/protocol and final cache teardown. Wider CPU
includes reset/protocol. No CPython timing, physical heap peak or embedding-tail
claim.225.65s remains inside the300s owner. All image/auxiliary/output/budget/
quiet/join/archive gates pass; no worker or RAM fixture remains. Raw SHA256:
`2ad6b17caabbdb2b0fc16850e6891125caa389922fdfdf92ec744926e0f9e68f`.

Before staging, only withdrawn4311's three working aliases and a duplicate failed
B718archive were retired after canonicalfc11817c Git/SHA/path/UID/all-alias proof.
Current8ae images, catalogs, toolchains and unchanged254/252MiBfloors survived.
All withdrawn evidence remains immutable in its canonical result.

167internal/746public focused cases per actual .NET11/10 pass (1,826total).
Twenty-nine isolated Python controls cover values, identity, namespaces, aliases,
promotion, errors, callbacks and genuine I/O under both APIs. Admission observers,
cooperative loops and retained results preserve first refusal, current values,
funding through cleanup and shared-budget reuse. Frozen581inputs include8changed
and one retired old file. Full platform/stress/installed-consumer qualification is
pending; later delivery is recorded separately. The previous CPython checkpoint
still measures009d, not this candidate. Publication/tagging stay held.

[Decision](decision.json),[timing](timing-summary.json),
[all sources/cores/tools/checks/raw observations](sources-checks-and-observations.tar.xz).
