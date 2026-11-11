# R228 — dispatch completion prototype withdrawn

2026-11-11. Exact qualified4aa/9ee7 baseline, evidence HEADd7279fcb; candidate548a9edc,
one production file. No retained runtime change or current CPython ratio. [Decision](decision.json).

The compiler already emits explicit completion/return/jump/throw terminators. The
prototype removes the additional array-end successful completion and per-instruction
jumped flag/test, routing taken control/exception paths directly back to dispatch.
Managed bounds checks and per-instruction cancellation/terminal-refusal checks stay;
pending async cleanup retains its instruction and advancement outcome.

**Withdraw the whole design.** Direct routes add22net source lines, route exits and
duplicate cleanup guards while retaining the necessary disposal protocol. No extra
argument/frame/storage phase disappears. Allocation is flat. Mixed desktop clocks
do not establish CPU gain/equivalence or an isolated regression. The added routing
is not justified as a simplification; no quiet campaign or partial branch rescue
is selected. This is not a fixed-percentage veto or permanent rejection of a larger
dispatch redesign. R227's method samples include useful inlined scanning.

## Bounded complete-script observations

Existing six scripts, fixed common worker/adapter/auxiliary images; only core DLL
differs.256warmups/image,5rotated16-invocation rounds, two identical baseline ALCs
and one candidate in the same Windows/.NET11RC3 process, ordinary GC/tiering.
These are **diagnostic desktop elapsed ranges**, relative to both baseline roles:

| Complete script | Sync envelope | Async envelope |
| --- | ---: | ---: |
| csv-validated-records.nonlocal-counter | -2.00..+1.27% | +10.53..+11.42% |
| text-summary-helpers.nonlocal-counter | +3.24..+10.49% | -6.05..-3.20% |
| report-render | -10.60..+7.48% | -1.37..+0.91% |
| text-summary-helpers | -4.73..-1.12% | +12.43..+19.34% |
| csv-validated-records | +5.28..+10.71% | -3.32..+0.59% |
| json-summary-helpers | -3.57..+2.56% | -13.93..-8.35% |

Envelope includes mediated file I/O, fixture reset, full output checks and result
disposal; excludes startup/compilation/final cache disposal. Separate Run-only clocks
exclude reset, post-return verification and disposal. Both clocks and every original
sample survive in [desktop](desktop/warm-sync/measurements.json). Shared JIT/GC and
variable controls prevent CPU conclusions. Compile construction/disposal is separate;
its source is unchanged, allocation flat, clocks variable/adverse up to+31.58%.
Warm compilation/retained capacity are not combined into a universal speed claim.

Candidate passes **1046internal/3483public per actual.NET11/.NET10**, including
protected/generator/import/control semantics, nonallocating cancellation loops,
terminal refusal, held async cleanup, retained values and shared-budget reuse.
All measurement goldens, stable/final-zero balances, image isolation and fixture
cleanup pass. No new test freezes the discarded architecture.

Guarded restoration verifies the owned candidate before writing the exact original;
all production bytes equal qualified4aa. Forced nonincremental solution rebuild
22.54s/0warnings/errors and **62internal/119public per actualtarget** pass. Rebuilt
cores have separate recorded identities, no timing claim. Cached full delivery
42953725339 remains authoritative for unchanged production; no full replay/pin or
publication. [Restoration](restoration.json),[focused](focused-checks.json),
[restored checks](restored-checks.json),[all source/cores/conditions/logs](preparation.tar.xz),
[archive identity](archive-identity.json).

Next assess the missing complete patch-generation shape using existing tooling.
Current difflib already reuses native match-length dictionaries; do not assume a
quadratic guest grid or select another narrow shortcut without workload evidence.
The broader performance objective remains active.
