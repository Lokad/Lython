# Typed regex analysis and legacy-pass deletion (B59)

2026-11-01. Retained for correctness and removal of duplicated analysis. A user
object/parameter named `re` was rejected as the regex module. The [original probe](original-probe.json)
disagrees with isolated Python. Ten of twelve new public controls fail on the
original library; all pass after consolidation. No qualified speedup is claimed.

[Plan before code](pre-code-plan.md), [exact production patch](source.patch.gz):
remove the267-line StaticRegexDiagnostics pass, string/locale binding tables,
branch clones, recursive walks and eager argument packing. Existing typed flow
handles actual regex calls/imported aliases. Exact small integer bitwise facts
preserve masks; unknown protocols remain dynamic. Delete the duplicate flag
resolver and syntactic DEBUG/truthiness tests. No new ledger/cache/enum framework,
configuration, owner or backend dependency.

Intentional diagnostic change: remove LA3044's textual named-escape ban. Raw pattern
syntax belongs to the provider, including escaped backslashes and verbose comments.
Provider pattern rejection during inference remains unknown and reports catchable
PatternError on execution. Capability/cancellation/terminal refusal stay distinct.
Non-raw Python string/f-string named escapes remain unsupported; no Unicode database.

Twelve new controls cover shadowing, imported aliases, masked flags, escaped/comment
text, original raw pattern/current tokens, provider-defined named escapes and
catchable rejection. Eight [independent CPython3.13.2 goldens](reference-validation.json)
run through both public APIs with budget cleanup. The test provider maps exactly
one named character to a literal OUTSIDE core; it does not qualify backend named
escapes. Useful existing flag/no-side-effect checks remain; only LA3044 assertions
disappear. Zero-warning Release build;130 native/756 public focused cases pass per
actual.NET11RC3/10.0.8, including provider/adapter cancellation/refusal/lifetime/cache
families. Original new-controls:10 failed/2 passed; final12 all pass.

## One fixed short comparison

Original source95b5955c, frozen20e208c0; candidate7050bf5b +patch. [Fresh identities](candidate-identity.json)
prove equal nonincremental core/worker/test/probe copies and unchanged auxiliary
hosts/adapters. Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB,
.NET11RC3. Separate images share CoreLib/actual hashes, ordinary GC/tiering.

| Complete phase/job | Original ms | Candidate ms | Original control ms | Candidate vs both |
| --- | ---: | ---: | ---: | --- |
| Fresh compile CSV helpers | 0.087272 | 0.085483 | 0.088539 | -2.05% /-3.45% |
| Fresh compile text helpers | 0.067899 | 0.065453 | 0.067722 | -3.60% /-3.35% |
| Warm CSV, unaffected | 8.34478 | 8.33112 | 8.31646 | -0.16% /+0.18% |

Compile includes fresh public Compile/validity/script-owner disposal, excludes
execution/startup. Warm includes reset/mediated I/O/full goldens/result disposal,
excludes compile/startup. No phase subtraction or external ratio.1024 compile/
128 warm interleaved warmups per image,2s settle/case, seven rotating4096-compile/
32-run batches. Every outlier retained. Controls+1.45%/-0.26%/-0.34%, within3%;
IQR0.37–2.47%, all batches>100ms. All quiet/hash/golden/balance/cleanup gates pass.
Service exits0 in36.752s within45s; inactive/dead/success, MainPID0/empty group,
lease free. Coexisting images change shared GC/JIT pressure; modest compilation
observations are diagnostic, not qualified embedding latency. [All samples/gates](summary.json).

## Limits and next correction

Full platform/original stress/package checks are batched with B60; no historical
CI polling or publication. Core stays decoupled from unchanged optional adapter0.4.0.
A new test exposes a PREEXISTING gap: provider Position is discarded and Python
PatternError attributes are absent. [Probe](error-metadata-probe.json). B60 records
that correction; current tests claim catchability/message, not metadata support.
A temporary enum-fact detour assumed attributes absent from existing integer-valued
flags. It and two unsupported-behavior expectations were removed before collection.
The preliminary frozen image failed focused checks and was discarded without any
comparison. [Logs](validation-logs.tar.gz) preserve failures/corrections. Final
source has no enum detour or legacy pass; no favorable retiming.

Readable [driver](Program.cs)/[collector](collect.sh), criteria, identities and
[raw results](raw-results.tar.gz), SHA256
`9d2c0d589277ea50207f591c46163807555da072a4454cb22ceae1dd5cb11f8c`,
preserve complete evidence. Correctness/deletion justify retention; the small
compilation observation does not solve the warm CPython gap.
