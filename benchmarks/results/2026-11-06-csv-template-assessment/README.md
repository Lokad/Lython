# CSV header construction assessment — 2026-11-06

**Withdraw the production prototype; retain four independent Python regressions.**
The measured changes do not justify new cache/index/replacement-lifetime machinery.
This is an engineering tradeoff, not a fixed improvement threshold or permanent
ban. Existing B150 bulk field admission stays delivered and unchanged.

[Contract](contract.md), [source/validation history](source-and-check-history.tgz),
[independent CPython controls](independent-goldens.json), [assessment](assessment.json).
Source based on ebaa5850 (production1134d6e8); candidate12096662 versus F05/core520b3eb0.
A separate shared-key format was rejected before coding: native reads, reentrant
protocol queries and iterators across promotion would require parallel machinery.
The smaller prototype kept canonical tables, cloned stored header metadata and
filled independent row values. Identity validation preserved mutable headers,
duplicate positions, retained rows, rest fields and reentrant callbacks. Template
and snapshot were admitted through existing batch ownership; no parser/EOF change.

Initial cache growth could inflate later normal-row capacity. Before measurement,
the template became immutable; each row retained its original admitted capacity,
with stored-hash appends for larger rest-field rows. Both source variants and all
checks remain preserved. No failed/adverse samples were dropped.

## B321 complete scripts

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3/CPython3.13.16, unchanged b94 worker/protocol2,
normal GC/tiering. Two identical candidate roles, F05 reference, full output/file
goldens, five guest warmup seconds/role, seven rotated150ms rounds.

| Script/boundary | Candidate / identical control | F05 reference | Changes versus F05 |
| --- | ---: | ---: | ---: |
| Ordinary CSV warm |4.665 /4.660 ms|4.678 ms|-0.27% /-0.38%|
| Reused variable-width CSV warm |6.604 /6.607 ms|6.732 ms|-1.89% /-1.86%|
| Ordinary CSV compile-to-result |4.771 /4.739 ms|4.864 ms|-1.91% /-2.56%|
| Unchanged JSON warm control |4.541 /4.601 ms|4.529 ms|+0.26% /+1.60%|

113.39s/four jobs, all quiet/golden/funding/join/fixture gates pass. Variable-width
reference IQR2.93%; unchanged JSON current-role spread1.33%. Warm ordinary CSV is
essentially flat; the small variable/fresh benefit is diagnostic. No allocation,
guest-only CPU, working-set, RSS or embedding-latency improvement established.
[Raw observations](observations.tgz), [summary](summary.json), [identity](identity.json).
The variable-width source/fixtures are reused unchanged from B150, with provenance
inside the archive. Execution clocks include mediated RAM I/O, complete validation
and prior-result cleanup; fresh adds compilation/disposal. Startup/staging/reset/
transport/final teardown excluded. CPython imports persist; Lython isolates imports.

Prototype Release125 internal/415 public checks pass per actual.NET11RC3/.NET10.0.8,
including four new Python controls. After withdrawal, force a nonincremental build:
zero warnings/errors and44 canonical CSV cases pass on both actual runtimes. All
prototype fields/helpers are gone; production tree matches fully qualified1134d6e8.
New regressions have semantic names, covering equal header-key identity, duplicate/
rest collision, irregular independent rows and reentrant header replacement.

The complete-script objective remains active. Next work needs evidence of a
dominant removable phase, not more construction caches for small source-level
opportunities. Regex development remains independent; publication postponed.
