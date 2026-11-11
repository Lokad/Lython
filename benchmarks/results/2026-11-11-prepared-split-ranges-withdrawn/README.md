# R230 — prepared split ranges withdrawn

2026-11-11. Qualified4aa/frozen9ee7 baseline, evidence HEAD2f6fa99e; candidate
identities in [source pair](diagnostic-identity.json). One production file/+20 net
lines; **whole prototype withdrawn**, exact qualified production bytes restored.
No retained runtime change, new option, pool, cache or architecture-specific test.

The current R227 profile attributes72 leaves to private split preparation and
additional useful scanning inside iteration/dispatch. These samples motivated a
bounded tradeoff, not a speed forecast: reuse the existing range collector to
retain immutable boundaries after sizing rather than scan the line again. The
existing cursor writes independently owned words from prepared ranges; every word
is still prepaid before the first guest loop effect. Scratch and final range-array
overlap are admitted before allocation. Existing Unicode writer/collector, current
cancellation, terminal refusal and ownership mechanisms remain unchanged.

**The second scan disappears, but a range-array allocation/copy and its lifetime
appear.** Text CLR allocation rises8.48–8.49% for the captured-helper shape and9.11%
for ordinary text through both APIs. Estimated execution-allowance peaks increase
163,840B/about9.85%, not RSS. That gives back more allocation than R225 saved. No
compensating whole-script benefit is demonstrated; adding inline tiers or a cache
would move away from simplification. Withdraw the complete design without a quiet
campaign. This is a construction/storage tradeoff, not a fixed-percentage veto or
permanent closure of a different coherent text pipeline. [Decision](decision.json).

## Bounded observations

Existing six complete scripts, fixedfec79 worker/optional0.4/auxiliary images,
only core differs. Same Windows/.NET11RC3 process, ordinary GC/tiering,256warmups
per image,5rotated16-invocation rounds and two identical baseline ALCs. All goldens,
stable/final-zero shared-budget balances, image isolation and fixture cleanup pass.
Envelope includes mediated file I/O, fixture reset, full checks and result disposal;
excludes compilation/startup/final cache disposal. Run-only clocks exclude reset,
post-return validation/disposal and survive separately. Compilation includes
construction/disposal. Shared JIT/GC and moving controls prevent CPU conclusions.

These are diagnostic **envelope** ranges against both identical baseline roles:

| Complete script | Sync change | Async change |
| --- | ---: | ---: |
| csv-validated-records.nonlocal-counter | +5.97..+11.78% | +8.94..+9.19% |
| text-summary-helpers.nonlocal-counter | +5.98..+7.17% | +1.19..+4.75% |
| report-render | -16.03..-9.12% | -1.14..+4.78% |
| text-summary-helpers | -2.56..+0.64% | -0.14..+2.75% |
| csv-validated-records | +0.26..+3.34% | -4.64..-2.59% |
| json-summary-helpers | -3.91..+4.95% | -2.59..+2.78% |

Captured text's sync control spread is1.11%, async3.41%; ordinary text3.18%/2.81%.
Other unchanged controls vary up to9.23%. All adverse samples and wider coarse CPU
remain in [raw sync](desktop/warm-sync/measurements.json),
[async](desktop/warm-async/measurements.json) and [compile](desktop/compile/measurements.json).
Compilation source is unchanged, allocation flat, elapsed variable/adverse up to
+9.68%. No elapsed improvement/equivalence/causal CPU regression is claimed.

Candidate **45internal/263public checks per actual.NET11/.NET10** pass, preserving
Unicode/order/aliases/defaults/keywords/dynamic targets, terminal all-word refusal
before body effects, cancellation, genuine suspended protocols, retained values,
awaited cleanup and budget reuse. No test added to enforce this private design.
Guarded restoration verifies the owned candidate before writing original physical
bytes. Forced nonincremental solution rebuild20.05s/zero warnings/errors and the
same focused checks pass for restored source. Rebuilt binary identities are
separate, not timed. [Restoration](restoration.json), [checks](restored-checks.json).
Full platform/stress/installed-consumer4aa qualification is already cached GREEN;
unchanged source does not require replay. Broader whole-script work remains active.

The first private-driver launch omitted its unchanged hash-witness input and
failed before image construction, warmup or timing. The corrected run supplies
that same R228 input, changing no source, images or settings. Original empty output,
error and explanation survive with all source/cores/builds/tests/commands in
[preparation](preparation.tar.xz), verified by [archive identity](archive-identity.json).
No VM collection, producer change or publication occurred.
