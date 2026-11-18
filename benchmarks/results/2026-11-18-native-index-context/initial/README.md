# Explicit native indexing: initial source pair

R523/B953,2026-11-18UTC. **Private, unpromoted b6769b32** versus fully qualified
3f873227 / a5038a28. Existing8-second warmups/five rotated rounds, four jobs187.39s;
same32-file workers/catalog/goldens/default GC/tiering. Two production files;
context follows native string count/cache/copy instead of a per-read ambient scope.
Genuine context-free/reversed delegate calls remain. Bothactual11/10 pass59internal/
144public focused cases, including four cold/warm native admission/cancellation
regressions under a different cancelled outer context.13goldens/allocation/reuse
and four actual timed-case goldens pass; no full qualification inherited.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate spread |
| --- | --- | ---: | ---: | ---: |
| Floating JSON report | Warm | −0.93..+1.01% | −0.95..−0.08% | 0.14 / 1.82% |
| Same | Compile-to-result | −2.24..−0.90% | −4.43..−2.48% | 0.91 / 0.46% |
| Helper text | Warm | +1.42..+4.03% | +0.97..+3.21% | 0.42 / 2.15% |
| Positional calls | Warm | −1.76..+0.51% | −1.65..−1.15% | 2.07 / 0.24% |

Warm includes Run, mediated RAM I/O, goldens and previous-result cleanup; fresh
adds compilation/compiled-owner disposal. Startup/reset/protocol/final caches are
outside guest clocks; process CPU wider. Ranges combine process medians, not
confidence intervals. All within-process IQR<1%; control movement limits precision.
Allocation effectively flat, accounted peaks unchanged, all13budget baselines
restore. No CPython ratio or general speedup is established.

Keep the prototype unpromoted. Source review identifies a broader effect than
intended: ordinary CopyFromUtf8 now snapshots ambient context and passes it to a
long-copy checkpoint that baseline ordinary copies did not use. One changed-source
refinement removes that implicit snapshot for ordinary copies, keeping explicit
current context for native cache/character users and the existing ordinary inline
ambient checkpoint. It is a justified scope correction, not proven attribution
of the whole text slowdown. Repeat focused checks and one short changed-source
pair; retain every original condition and decide debt/retention/refinement from
usable evidence. No causal subtraction, rigid threshold or permanent design ban.

All workers/quiet/Closed/exit/join/SHAarchive gates pass before RAM teardown. Only
exact archived22e5historical core/all10aliases retired under fresh idle/path/UID/
hash/link proof; current2ede/a71/c036/84/a503images/catalogs/toolchains/unmatched
evidence and254/252MiBfloors remain. Publication held; broader objective active.

[Source](candidate-sources.tar.xz),[summary](summary.json),[raw](observations.tgz),
[identity](identity.json),[checks](checks.json),[procedures](procedures-and-checks.tar.xz).
