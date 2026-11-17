# Constant-key record construction

Private R499 on4b975a44/corecf847936 versus committed4b975a44/core5d9633a8.
Eight production files remove repeated constant key operands and per-record key
tables/hash insertion. Existing RecordKeys and execution/function lookup owners
retain immutable keys; final value storage preserves duplicates, first-key
identity, mutation, value order, suspension, aliases and admitted overlap. Dynamic
keys/protocols/unpacking use the general path. No new owner, allowance or ledger.

B924,2026-11-17 quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normal tiering/GC:
six jobs229.03s, six-second full-request warmups, five rotated rounds, two processes
per source. Complete4,096record/110,592B JSON record transform: warm elapsed
12.03–13.53%lower/CPU13.03–15.20%lower; compile-to-result8.80–11.99%lower/
CPU8.55–10.83%lower. Same-source spreads warm1.17/0.55%,fresh0.86/2.74%.
Allocation14.67%lower,2,905,888→2,479,712B/run; accounted peak6,576,387→5,462,675B.
Six allocation/budget-reuse checks and actual-image workload goldens pass.

Text warm−0.43..+0.49%elapsed/CPU−0.59..+0.59%, JSON-helper−2.41..+0.47%/
CPU−2.13..+1.44%, positional calls−1.30..−0.03%/CPU−1.57..+1.60%: no material
control gain claimed. Floating warm−6.82..+6.17%/CPU−9.34..+5.67% with12.91%
candidate same-source spread remains uncertain. Retain the coherent record design
on named semantic/storage/whole-script evidence; carry this uncertainty rather
than declaring recovery or a general CPU improvement. Do not add independent
source-pair medians or infer new CPython ratios. Prior adverse R498 conditions remain.

Focused actual11/10 each401internal/58public plus90dictionary internal/
506public dictionary/admission checks pass(overlapping). Extended suspended-value
and cold/warmed-cache terminal-refusal cases pass11class cases per target. Frozen
source snapshot contains the9case version; the later additions change tests only.
All585production inputs match integrated source, not a claim of binary equality
after rebuilding elsewhere. Full cross-platform/stress/installed Release package
qualification is batched independently at the next coherent delivery boundary.

Frozen images are copies of all32recorded files, only the core replaced; exact
unchanged5dreference diagnostics are reused after full hashes/catalog comparison.
Auxiliary worker/optional adapter remain the reference files. Existing verify
checks the six-case catalog against actual images/CPython goldens before staging.
Warm includes mediated RAM I/O, goldens and prior-result cleanup; fresh adds
compilation/compiled-owner disposal. Startup/reset/protocol/final cache teardown
excluded; process CPU wider. No hidden quota, cancellation bypass or publication.

Quiet gates, joined users, persistent archive/localSHA and fixture teardown pass.
Retirement affects only exact committed old8dcore aliases/raw replicas; current5d
andcf images/catalog/toolchains stay. Staging/archive254/252MiB floors unchanged.
The first local build named the wrong solution extension and a freeze script had
a syntax error: both are retained; neither built a timed image. A final preparation
assertion had a misspelled search string after all six-job scripts were generated;
the following shell command launched despite that local false negative. Actual
collector/archive/retriever counts were six and passed. The corrected validator
is retained; no worker/source change or retiming was needed.

[Summary](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[identity](identity.json),[sources](candidate-sources.tar.xz),[decision](integration-decision.json),
[procedures/checks](procedures-and-checks.tar.xz).
The broader performance objective continues; publication remains postponed.

Subsequent [full delivery](delivery/README.md) GREEN44192474928 at ea8e9f91 verifies all16summaries/2,385inputs/585production against measuredcf847936 source; no rebuilt-binary equality inferred. Original source/control conditions remain.
