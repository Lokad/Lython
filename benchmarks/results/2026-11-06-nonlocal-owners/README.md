# Nonlocal ownership facts — 2026-11-06

Retain the source simplification: lexical scope facts and parent links already
identify nonlocal owners. Remove the per-invocation dictionary, shared empty table
and eager map construction. Keep eager validation with existing cancellation and
terminal-refusal checks, the same class/global barriers, direct executable cells,
dynamic locals/reads/writes/deletes and existing StoreName synchronization. No new
owner/cache/ledger, public API or Python behavior. A potentially unfunded variable
map was a source concern, not a demonstrated host-budget failure.

[Contract](contract.md), [validation](validation.json), [focused logs](focused-checks.tgz).
Release56 internal/1801 public checks pass per actual.NET11RC3/.NET10.0.8;
build zero warnings/errors. This source follows ae3a5a5b and is not qualified by
that earlier source's CI. Final platform/stress/package checks remain batched.

## Bounded whole-script comparison

Quiet F4as_v7, Ubuntu24.04.4, .NET11RC3, CPython3.13.16, frozen b94 worker and
unchanged protocol2. Core0050f188 candidate/control versus F03/core6cb31a1e.
Two complete4096-record CSV jobs, identical fixtures/report/output goldens; the
nested accumulator also has an independent CPython3.13.2 output/file control.
Normal GC/tiering, five guest warmup seconds/role, seven rotated150ms rounds.

| Warm script | Candidate / identical control | F03 reference | CPython | Candidate / control delta versus F03 |
| --- | ---: | ---: | ---: | ---: |
| Nested nonlocal accumulator |4.935 /4.900 ms|5.101 ms|3.010 ms|-3.25% /-3.94%|
| Unchanged ordinary-helper CSV |4.648 /4.729 ms|4.639 ms|3.229 ms|+0.19% /+1.94%|

B316 completed59.35s, all quiet/funding/golden/join/fixture gates pass. Nested
current-role spread0.71%, larger role IQR1.88%; ordinary control spread1.75%.
This supports a small affected-workload improvement; it does not establish broad
CSV, allocation, guest-only CPU, RSS or embedding-process latency improvements.
Warm clocks include invocation, mediated RAM-file I/O, full validation and
prior-result cleanup; compilation/startup/staging/reset/transport/final teardown
are excluded. Lython imports are execution-isolated; CPython retains modules.

Original B315 completed both jobs but failed the final quiet gate:13.57% busy CPU,
no steal/fault/swap. An agent SSH status query coincided with that window; its
contribution is plausible, not proven. All workers joined and fixtures were
removed. Preserve its [invalid samples](failed-observations.tgz) and
[summary](invalid-block-summary.json); do not qualify their timings. One unchanged
confirmation avoided SSH monitoring during the owned110s supervisor window.
[B316 full observations](observations.tgz) and [summary](summary.json) remain
separate. Original local Windows stdout-newline setup failure is preserved in
[setup history](golden-setup-history.txt); report-file bytes stayed exact.

Publication remains postponed; the whole-script performance objective stays open.
