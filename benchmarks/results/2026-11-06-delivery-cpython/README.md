# CPython comparison — coherent b6650f09 delivery

2026-11-06. Fully qualified runtime b6650f09 (R13C/R14/R17/P01); current-source
core54b53c7f matches the explicitly packed/installed-consumer package. Auxiliary
workerb94fa98c and normal0.4 adapter stay frozen/unchanged; private dev44 work is
outside these images. Exact identities, source snapshots and patches are in the
archives. This measures the combined delivery, not each constituent's CPU benefit.

Five existing scripts, three separate boundaries: compile/dispose, warm, and
compile-to-result in an already-running host. Same prior four large script/fixture
bytes plus existing small configuration edit(16paths,181B). CPython3.13.16 plus two
independent identical current Lython roles, normal GC/tiering. Five-second warmup
per role/job; seven rotated150ms samples.15jobs finish311.43s. Quiet endpoints,
full output/file checks, zero terminal balances, Closed/joins and fixture cleanup
pass. Persistent raw archive was created/hash-verified under the owned VM lease;
all processes are terminal. No repeated service query is needed.

Fresh Lython/CPython: CSV1.31x, text1.75–1.83x, JSON1.12x, report1.44–1.46x,
small config1.21–1.22x. Warm:1.34–1.39/1.80–1.82/1.18–1.20/1.59–1.60/1.29–1.31x.
Text fresh has4.44% identical-role difference; warm CSV3.50%. Report spread remains
variable. Preserve those observations, not a single optimistic point. Compile-only
is reported independently; do not subtract separate warm/fresh medians (JSON fresh
is slightly below warm in these distinct job blocks). Historical source-pair debts,
including R17's adverse text result and earlier formatting disagreement, remain.
This refresh cannot establish their cause or an additive speedup for the combined
changes. Exact process-CPU ticks are preserved; they are coarse/overlapping across
threads and are not guest-only or source-pair CPU attribution.

Includes mediated RAM-file I/O, full validation and previous-result cleanup;
fresh adds compilation/program disposal. Startup, fixture staging/reset, transport
and final teardown excluded. CPython keeps imported modules; Lython imports are
isolated per execution. Startup/RSS/embedding-tail/genuinely suspended-I/O latency
remain unmeasured. The reusable docs/BENCHMARKS.md guide records all three tables.

Separate delivery CI42258608463 completed GREEN: Windows/Ubuntu, actual.NET11/10,
original Stress and installed packages. Local installed consumers pass both
runtimes; core has zero runtime package dependencies and adapter is nonpackable.
This closes R17/P01 runtime qualification; publication/tagging remain postponed.

VM disk preflight required removal of two exact redundant payload transfer files.
Both retained local payloads and persistent raw archives were hash-verified before
unlinking; expanded images/dependencies and sole evidence were preserved. All
commands/failed or adverse conditions and cleanup proof remain archived here.
