# Casing context trial: inconclusive, removed (B56)

2026-11-01. **Prototype removed.** Text original/control medians differ4.00%,
exceeding the predeclared3% gate. Candidate text elapsed improves5.94%/9.56%
against the two originals, missing the required7.5% against both. No accepted
speedup, retry, tuning or new CPython ratio.

## Design and controls

[Criteria frozen before code](pre-code-plan.md): remove four eager preceding-case
variables and per-rune property updates; scan the original UTF-8 prefix only when
lowering capital Sigma. Keep forward context, property data, Unicode expansions,
title's word state, per-rune cancellation and existing admitted builders. No
conversion/copy/cache, new ownership mechanism, unsafe code or public change.
[Exact candidate](source.patch.gz).

This explicitly permitted new-source trial follows B54/B55's comparison-method
evidence. B36's stale-binary comparison remains invalid and unchanged; none of its
numbers is reused. Original source95b5955c, frozen at20e208c0; candidate62eac173 +
patch. [Fresh build identities](candidate-identity.json) prove equal worker/test/
probe copies and byte-identical auxiliary hosts. Informational revisions differ.

One owned process on dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs /
16GiB, .NET11RC3. Original/candidate/identical-original images share CoreLib and
actual dictionary-key/word hashes; independent core images are verified. Normal
GC/tiering,128 interleaved warmups/image/case,2s settle/case, seven rotating32-job
batches. All outliers retained. Complete warm public jobs include fixture reset,
mediated file work, full goldens and result disposal; compile/startup excluded.
Coexisting images share process GC/JIT pressure; no context-unload guarantee.

## Complete observations

| Job | Original ms | Candidate ms | Original control ms | Candidate vs both |
| --- | ---: | ---: | ---: | --- |
| CSV helpers, unaffected | 8.27327 | 8.31243 | 8.18814 | +0.47% /+1.52% |
| Text helpers | 6.40299 | 6.02278 | 6.65927 | -5.94% /-9.56% |

CSV controls differ-1.03%; text+4.00%. Relative IQR1.07–1.92% in all series;
every batch>100ms. Low within-image dispersion does not repair failed controls.
Both quiet gates, full outputs, hash witnesses, zero final balances and owned
fixture removal pass. Collection exits0 between12:25:10–12:25:33UTC, within45s.
Service is inactive/dead/success, MainPID0, empty group; exclusive lease available.

## Validation and restoration

Fresh candidate Release build has zero warnings/errors. Existing independent
Greek/overlap/supplementary/long-context and all-method casing controls, terminal
refusal and shared-budget checks pass:95 native/105 public per actual.NET11RC3 /
10.0.8. No architecture-only tests added.

Original three files restored with fresh timestamps and a nonincremental solution
build. Production diff empty; matching fresh core/worker/test/probe copies and
the same95/105 checks pass per target. [Restored identity](restored-identity.json)
records the result; no baseline-binary reproduction claim. Existing terminal
production qualification95b5955c/41296168049 is not repeated.

[Every observation and gate](summary.json), [validation logs](validation-logs.tar.gz),
readable [driver](Program.cs)/[collector](collect.sh), criteria, patch and
[raw records](raw-results.tar.gz), SHA256
`016b52ac1b65753fa13297ebe836ed0a16fad77ad247b8497e952ef6b4a476fc`,
preserve this failed acceptance decision. Stop this prototype; no favorable
retiming or threshold change. Independent development continues.
