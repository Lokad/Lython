# Source-to-string-literal construction

R129/B489, 2026-11-09: committed8472935f/coreac6b versus frozen prototype core1be2.
The [current compiler profile](../2026-11-09-compile-phase/README.md) identifies
parsing as material work; it does not make all parsing overhead removable.

Ordinary literals borrow token spans, materialize unescaped bodies directly and
allocate concatenation storage only for adjacent nonempty pieces. Formatted parts
are allocated only for actual f-strings. One canonical body decoder replaces
synthetic triple delimiters around already-parsed f-string text. Removed ordinary
token copies, unconditional decoding/concatenation builders, intermediate decoded
copies and unused parts lists. AST/static-analysis strings and runtime UTF-8 storage,
coarse compilation admission and ownership remain unchanged. No new representation,
cache, public option or evaluator. Escaped pieces retain the existing decoder.

Focused Release actual .NET11/.NET10 each: 42 internal and 529 public cases pass.
Coverage includes raw/triple/escaped/adjacent/u/f/bytes pieces, scalar rejection,
source diagnostics, annotation/pattern constants, active parser cancellation and
first refusal, retained literal aliases and budget reuse/cleanup. No tests, assertions
or timeouts changed. Full Windows/Ubuntu Release actual11/10, original stress both and installed-core
consumers pass [cached CI42545629905](delivery-ci.json). The receipt records its
terminal observation; it qualifies459/core1be2, separately from laterR132.

Five existing complete scripts/config, unchanged worker/auxiliaries/normal GC/tiering.
Desktop isolated images share CoreLib/process GC. Actual compilation allocation:

| Script | Reference bytes | Candidate bytes | Reduction |
| --- | ---: | ---: | ---: |
| Validated CSV/helpers | 184,778 | 178,450 | 3.42% |
| Text/helpers | 135,314 | 131,338 | 2.94% |
| JSON-lines/helpers | 146,666 | 140,458 | 4.23% |
| Report rendering | 122,538 | 115,714 | 5.57% |
| Config edit | 115,298 | 103,890 | 9.89% |

These are CLR allocations, not lower admission estimates. Warm allocation is neutral
within 0.023%. Original adverse desktop compile text +13.35–15.03% and JSON
+3.13–6.93%, plus warm/control drift up to13.32%, remain in raw evidence.

B489 quiet F4as_v7/Ubuntu24.04.4/.NET11RC3: six jobs167.303s, four independent
processes/job, five-second warmup/role, seven rotated150msrounds. Compile-only includes
construction/prior-script disposal; complete config includes compilation, mediated
RAM-file I/O, full output/file validation and prior-result cleanup. Startup, fixture
reset, protocol and final cache teardown are excluded. Warm text excludes compilation.

| Script / lane | Elapsed change vs both references | Process CPU change | Reference / candidate spread |
| --- | ---: | ---: | ---: |
| Config / compile | −4.79..−3.66% | −5.54..−5.00% | −0.56% /0.62% |
| Text / compile | −1.37..+0.03% | −1.28..−0.03% | −0.12% /1.30% |
| JSON / compile | −3.03..−0.15% | −2.62..−0.24% | −1.37% /−1.54% |
| Report / compile | −4.24..−2.60% | −4.14..−0.03% | 1.39% /−0.32% |
| Config / compile-to-result | −3.35..−1.08% | −8.66..−1.74% | 0.80% /−1.50% |
| Text / warm control | +0.36..+1.01% |0.00..+1.23% | −0.15% /−0.49% |

Ranges include both candidate processes; signed spread compares second/first
same-image processes. CPU ticks are coarse. Config compilation gains have supporting
CPU; full config also improves in this bounded run. Large adverse desktop compile
timings do not reproduce. Small warm text debt remains possible; this parser change
does not establish a warm-execution gain or universal neutrality. Retain the coherent
staging removal with that debt explicit. No unchanged retiming selected.

All source/core/auxiliary hashes, goldens, budget balances, quiet-before/after,
Closed, joined-users, RAM removal, archive-under-lease and local verification pass.
No disk recovery needed. Terminal cached. Broader complete-script objective remains
active; publication postponed.

[Identity](identity.json), [focused validation](focused-validation.json),
[compile allocation/desktop timing](compile-assessment.json), [warm diagnostics](assessment-local.json),
[quiet results](assessment.json), [receipt](archive-receipt.json),
[raw quiet evidence](observations.tgz), [source/conditions/binary/diagnostics](source-and-diagnostics.tar.xz).
