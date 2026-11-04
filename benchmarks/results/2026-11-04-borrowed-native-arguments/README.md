# Borrowed native arguments — B192

RETAINED development source: `0ca0944a` plus the exact archived patch; refined core `659fa32aade2cfcafecfa2f42f4e3dad5812e9240f6608abdfa989b280fefbcc`. Baseline is final B190 `80e9a32d`, matching runtime `334c7cc7`, already fully qualified. Full delivery of this new source is pending; do not reuse B190’s receipt. Publication remains postponed.

Fully supplied positional calls without a receiver now consume their admitted caller operands synchronously, before binding rents/copies/presence/clears/refunds an argument array. The existing native guard/evaluator takes one readonly span rather than passing ownership-bearing BoundCallArguments recursively. General/default/keyword/star/receiver/lexical/suspending calls retain owned binding and real frames. Every guard precedes effects; refused admission is terminal, never a retry. Returned objects retain their own funding; spans do not escape. A failed borrowed guard is not repeated after a fully supplied, non-callback binding. No new IR, metadata cache, pool, option or backend. Ordinary bound-local/frame elimination was delivered earlier and is not claimed again.

## Controlled observations

Measured 2026-11-04 on the existing quiet Azure F4as_v7 Ubuntu VM, ordinary .NET11 RC3 GC/tiering. Negative deltas mean faster than each unchanged old worker. `RunAsync` here has synchronously completed host I/O; genuine suspension is tested separately. Each final block has three isolated workers, five seconds warmup each, one second settle and eleven rotating approximately250ms batches. The initial five complete scripts plus helper control used three-second warmups/nine200ms batches. Compilation is unchanged and excluded. Startup, fixture reset/staging and final cached teardown are excluded; actual mediated tmpfs I/O, full output/file checks and prior-result disposal are included. All work and cleanup is awaited and joined. No new CPython timing or fresh-compilation claim.

| Refined workload | API | Elapsed versus old roles | Old-control difference | Candidate IQR |
| --- | --- | ---: | ---: | ---: |
| text-summary-helpers | warm | -2.84% / -1.00% | -1.86% | 2.76% |
| report-render | warm | +0.63% / +0.47% | +0.16% | 3.41% |
| helper-normalize | warm | -6.56% / -7.55% | +1.08% | 1.91% |
| text-summary-helpers | warm-async | -7.34% / -6.94% | -0.42% | 0.53% |
| report-render | warm-async | +1.18% / -0.26% | +1.44% | 1.31% |
| helper-normalize | warm-async | -8.83% / -9.98% | +1.28% | 1.07% |

Initial helper elapsed improved5.9–7.7%, but report was adverse2.3–4.2% with4–5%IQR; text unchanged controls differed4.5–5.1%. CSV/JSON/config were approximately within2% of old roles. Retain every initial value. Source inspection identified a repeated failed native guard newly added before/after binding. One refinement removes that duplicated attempt. Refined report is within control/IQR variation; source explanation alone does not prove why elapsed changed. Async text improves6.9–7.3% with0.4%old-control difference and~0.5%IQR; synchronous text is uncertain (1.0–2.8% gain,1.9%old-control difference,2.8%candidate IQR). Helper improves6.6–10.0% with0.6–1.9%IQR. CPU ticks are coarse diagnostics and support the helper/async-text direction, not exact CPU percentages. No third unchanged retiming now; future bounded correction remains allowed when new evidence matters.

Allocation across all eight cases is effectively flat (largest complete-job difference+0.02%); the small argument arrays were already pooled. This removes rent/copy/presence/clear work, not a demonstrated heap-allocation reduction for these cases. Admitted whole-execution/root peaks are unchanged in these observations; this is not proof of identical tiny-budget thresholds. Large unpooled helper signatures can avoid argument storage, but were not separately timed.

## Validation, debt and evidence

Focused Release27 internal/148 public checks PASS per actual.NET11 and.NET10. Covers existing guards/forwarding, defaults/dynamic binding, class/closure/generators, evaluation effects, cancellation/refusal origin, genuinely suspended ownership and cleanup. New public retained-argument coverage survives operand reuse, a second execution and compiled-code disposal with allowance returning to baseline. Nonincremental Release solution build passes both targets. Full Windows/Ubuntu, original Stress and installed consumers are queued at this retained boundary, not implied by these focused tests.

Both owned captures completed (initial236.020s/refined197.266s), all goldens/balances/identity/bootstrap/quiet gates pass. All workers close normally/exit0 and join; no guest fixture directories or background matching/execution remain. Same-process isolated-ALC allocation diagnostics are not source-pair CPU proof. Source hashes and exact precommit patches identify measured images, not subsequent receipt commits.

Fresh-JSON provisional2–5% debt versus pre-loop B166 remains OPEN; this adjacent warm B190 pair cannot close it. Numeric-layout prototype costs remain distinct. Whole-script performance remains OPEN. Continue complete activation/operand and per-record work using actual source evidence; do not infer that merging pooled local storage into larger per-call arrays reduces allocation.

- Initial raw archive SHA256: `beb089b1289d2dc59d1e23577ebc5963facf40452acaea02d67859ef9a640750`.
- Refined raw archive SHA256: `95393e20b45f72ce636acf8eeaa3594b448e76bb449cf59a349014b98137feaa`.
- Each archive contains exact catalog, protocol/collector, source patch, identities, initial/final gates, raw warmups/samples, allocation observations and close checks. Summary/identity/decision files alongside are conveniences.
