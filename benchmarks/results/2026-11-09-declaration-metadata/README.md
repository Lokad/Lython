# Shared function-declaration metadata

2026-11-09, R127: qualified 9f2e1f51/core85e versus frozen prototype coreac6b.
Analysis, lowering and runtime now share a declaration-owned flat signature and
generator fact. Removed redundant positional/keyword-only parameter arrays,
frontend keyword-shape dictionaries, repeated inferred-call generator-body scans
and synthetic lowered-parameter wrappers for lexical name collection. Captured
abstract states and mutable defaults/annotations remain separate. General dynamic
binding, explicit admission, cancellation and cleanup are unchanged. No public API,
registry, dispatch guard, owner, carrier or option was added.

Focused Release actual .NET11/.NET10 each: 41 internal/360 public cases plus 23
internal/149 public additional cases pass, including scope/default/generator/order,
provider/static analysis, cancellation, refused admission, suspended argument/default
ownership and generated definitions. Four test fixture constructor calls changed;
no assertions, timeouts or semantic coverage were removed. Full Windows/Ubuntu Release actual11/10, original stress both and installed-core
qualification passes [cached CI42543394041](delivery-ci.json), observed Nov9
01:37:39UTC. It qualifies847/coreac6b; later459/core1be2 is separate.

Five existing complete scripts, identical fixtures/worker/optional0.4/normal GC and
tiering. Desktop allocation checks isolate images sharing CoreLib/process GC;
warm allocation is neutral within 0.066%. Compilation is separate: actual allocated
bytes fall 0.10–0.32% (144–448B per compile); coarse admitted compilation/generated
signature estimates remain unchanged. Desktop compile elapsed is inconclusive.
Original desktop sync validation CSV +13.09–15.11% and text +21.59–21.98% adverse
elapsed results remain in the raw evidence, alongside drifting controls in other
cases. No CPU or admitted-allowance reduction follows from allocation reduction.

B487, quiet Azure F4as_v7/Ubuntu24.04.4/.NET11RC3: six jobs in 168.433s. Four
independent processes/job, five-second warmup/role, seven rotated 150ms rounds.
Warm public Run/awaited RunAsync includes mediated RAM-file I/O, complete output
and file goldens, validation and prior-result cleanup. Compilation/startup,
fixture reset, protocol and final cache teardown are outside this timed boundary.

| Complete script/API | Elapsed change against both references | Process CPU change | Reference / candidate spread |
| --- | ---: | ---: | ---: |
| Validated CSV, sync | +0.36..+1.06% | −6.25..+6.67% | 0.20% / 0.50% |
| CSV summary, sync | −0.25..+1.61% | 0.00..+2.13% | −1.24% / −0.60% |
| Text helpers, sync | −3.50..−0.91% | −3.61..0.00% | −0.92% / 1.73% |
| JSON-lines helpers, sync | −1.79..+1.43% | −2.50..+2.56% | 2.16% / 1.09% |
| Report rendering, sync | −3.02..−0.05% | −5.88..+2.22% | −0.19% / −2.78% |
| Text helpers, async | −3.22..−1.00% | −2.70% | 1.34% / −0.93% |

Ranges include both candidate processes against both references. Signed spread
compares the second same-image process with the first. CPU ticks are coarse.
The large desktop adverse timings do not reproduce; small CSV elapsed debt remains
possible. Retain the metadata consolidation for removed work/storage and passing
semantics, with that debt explicit. This does not establish a general CPU gain or
universal neutrality. No further unchanged timing selected; objective remains open.

All core/auxiliary hashes, goldens, budget balances, quiet-before/after, Closed,
joined-worker, RAM removal, archive-under-lease and local archive checks pass.
A fresh idle proof and persisted plan preceded exact-byte hardlink consolidation
of eight singleton compiler-library aliases: 56,131,584B recovered, every historical
path/byte/metadata preserved. Original Windows argument-length launch failure and
corrected stdin transport are preserved. B487 terminal cached, no users/fixtures.

[Identity](identity.json), [focused checks](focused-validation.json),
[compile diagnostics](compile-assessment.json), [warm diagnostics](assessment-local.json),
[quiet comparison](assessment.json), [archive receipt](archive-receipt.json),
[raw quiet evidence](observations.tgz), [source/binaries/conditions/diagnostics](source-and-diagnostics.tar.xz).
