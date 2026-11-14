# Native primitive working storage: retained, full delivery pending

2026-11-14 UTC, R359. Refined frozen `a4bfbfd8` versus fully qualified
`47df7e19` / frozen `cbb254be`; five changed production inputs. **Runtime retained;
full Windows/Ubuntu, stress and installed-consumer qualification is pending.**
Publication/tagging remain postponed. The complete
[initial comparison](initial-README.md), including every adverse result, remains.

The final object-array call interface and ordinary frame representation stay intact.
Native prefixes keep private machine values beside those slots, materializing and
caching a stable immutable identity before aliases, calls, collections, returns or
ordinary-frame transfer. No marker or working span survives suspension. Small
prefixes borrow at most 128 bytes of managed working storage; larger bound helpers
preflight temporary capacity and keep it reserved until all users/cleanup finish.
The existing eligibility scan computes storage need, so helpers that cannot write
this representation allocate none. Primitive/object publication share the same
retirement, store checkpoint and instruction advancement. No private heap cells,
cell pools, adaptive thresholds, argument repackaging or second evaluator.

## Bounded completion comparison

Same quiet F4as_v7, Ubuntu 24.04.4, EPYC 9V45, .NET 11 RC3, unchanged workers/
auxiliaries/catalog and normal GC/tiering as B734. Four fresh processes/job,
5-second full-request warmups, five rotating 0.5-second batches. B735 finishes
all five affected-workload jobs in 175.171 seconds, with all image/output/file/
budget/quiet/join/archive gates. No remaining workers or RAM fixtures. Raw SHA256
`6884b92cf22a9a2d20dd5699423d53bcbbcea1a059fa0b5a1a5cd47a76ce6d7b`.

| Complete script | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| 4,096-row CSV while validator, warm | −22.99..−21.07% | −22.43..−21.50% |
| Same CSV, compile-to-result | −21.90..−21.21% | −21.00..−19.22% |
| JSON helper summary | −0.03..+1.93% | +0.69..+4.13% |
| Repeated short/zero helpers | +3.70..+10.19% | +5.69..+11.36% |
| Helpers exposing each intermediate | +2.29..+3.97% | +1.87..+4.51% |

Ranges span all process-median pairings, not confidence intervals. Short-helper
process spread is 3.75% candidate / 2.37% reference; uncertainty does not erase its
adverse direction. JSON elapsed is near flat to adverse, with adverse wider CPU.
Text remains uncertain from B734 and was not remeasured. No universal gain,
resolved call overhead, cross-algorithm or refreshed CPython-ratio claim.

Warm clocks include mediated RAM-file I/O, full validation and result cleanup;
exclude compile/startup, fixture reset/protocol and final cache disposal.
Compile-to-result adds compilation/compiled-owner disposal; wider CPU includes
reset/protocol. Startup, pending I/O, contention, heap peaks and embedding tail
latency remain unmeasured. CSV uses the identical diagnostic while algorithm in
both runtimes, not a comparison against the different maintained for-loop source.

Four fresh allocation processes/reversed order show CSV allocation down 40.868%
(5,131,710.625 to 3,034,475.125–3,034,479 B/run), counter allocation flat. The
inside-range counter promotes before its loop; ordinary frames still box values.
6,086 focused actual .NET 11/10 checks and 32 independent CPython 3.13.2 controls
pass, including aliases/overflow/protocols, real asynchronous I/O/cancellation,
stack/recursion, terminal refusal, retained funding and budget cleanup. All 14
frozen worker goldens match. No timeout/assertion weakened. The intermediate
`ca82b162` publication-only variant is preserved with focused/allocation checks,
but has no CPU/elapsed comparison. A local pre-SSH preparation error used the
wrong member path for old canonical evidence; inventory correction preceded any
remote launch or measurement.

**Decision:** accept the recorded short/observed-helper and small JSON CPU debt
for this coherent representation milestone and substantial CSV gain. Do not hide
the costs behind pools, hotness settings or unchanged retiming. Full qualification
and a representative external checkpoint follow; the complete-call/frame/operand
roadmap remains active and final 0.11 regression debt needs an explicit decision.
Earlier denial from genuinely required larger temporary storage is documented;
refusal remains terminal and cleanup returns working allowance independently of GC.

[Decision/debt](refined/decision.json), [timing](refined/timing-summary.json),
[allocation](refined/allocation-summary.json), [exact sources/identity](refined/candidate-identity.json),
[public checks](refined/focused-checks.json), [internal checks](refined/internal-checks.json),
[sources/binaries/variants/tools/raw/failures](refined/sources-checks-and-observations.tar.xz).
