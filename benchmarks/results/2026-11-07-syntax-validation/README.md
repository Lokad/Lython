# Demand-driven syntax validation — R62/B418

2026-11-07. Retain on f519e54b's restored, fully qualified e66b8f72 core.
Before/current cores463f6230/74c88248; unchanged normal0.4 adapter and worker.
[Frozen source/cores](identity.json), [pre-code plan](pre-code-plan.md).

The existing full validity walk selects three additional validators only when
their syntax can produce diagnostics: yield, aliases/type parameters, or future
imports. Their distinct scope rules and ordered diagnostics remain; no combined
visitor framework or retained feature cache is introduced. Empty comprehension
scope sets and per-statement clones of unchanged type-name scopes disappear.
No supported syntax, runtime route, public option or contract is removed.
Lexical preparation deliberately has different postponed-annotation/comprehension
rules and is not merged. Coarse compilation funding and stage cancellation remain;
later passes can still finish before the next token check. No in-flight claim.

559internal/1,492public focused Release checks pass per actual.NET11/10, with
matching probes. The existing isolated compile diagnostic compares358sources with
and without a provider:716identical validity/ordered diagnostic records, including
messages and spans. Cases include dead bodies, lambda defaults/body, generator
boundaries, generic/nonlocal scopes, postponed annotations and mixed errors.
Each script is disposed and the shared allowance returns to zero. No guest runs
in this differential. No tests/timeouts/containment assertions are weakened.

## Controlled comparison

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering; three independent
reference/reference-control/current processes,5s warmup per role/job and11rotated
200ms rounds. Five jobs complete in125.261s. Full goldens, source/auxiliary/Ready,
quiet endpoints, Closed/exit0/join/emptyfixtures pass. Persistent evidence is
separate from the owned128MiB fixture tmpfs. Archive verified under lease and
locally; guest mount removed.180sowner+30sstop+30smargin, observe240s. Terminal;
never query B418 again. Initial staging failed before measurement because copying
each owned core twice crossed the256MiB reserve. Exact-core hard links recovered
11,264,000bytes; setup passed at271,523,840freebytes. Original failure, corrective
command and logs remain; no reserve or measurement condition was relaxed.

Positive changes mean slower current execution. This is a source pair, not a
CPython refresh. The primary table still describes its recorded55e510ff revision.

|Boundary / complete script|Current|Change vs both unchanged roles|Old-role spread|Current IQR|
|---|---:|---:|---:|---:|
|compile / csv-summary-functions.large|63.21 µs|-4.54%/-5.88%|+1.43%|0.75%|
|compile / text-summary-helpers.large|47.17 µs|-2.94%/-3.50%|+0.58%|0.64%|
|compile / report-render.large|43.29 µs|-3.57%/-5.51%|+2.05%|0.88%|
|compile-run / config-edit.small|147.74 µs|-1.45%/-1.32%|-0.13%|1.91%|
|warm / text-summary-helpers.large|1893.08 µs|-0.51%/-0.42%|-0.09%|0.25%|

Compile includes construction/disposal; fresh includes compilation, mediated RAM
I/O, full validation and prior-result cleanup; warm excludes compilation. Startup,
fixture staging/reset, transport and final cached teardown are excluded. Compile
elapsed improves2.94–5.88%; fresh config1.32–1.45%. Warm text−0.51/−0.42% is a
control observation, not an established runtime speedup. External worker CPU is
10ms-quantized and includes work outside job clocks: compile−3.46to−10.13%, fresh
config−3.07/+0.76%, warm−0.94%. Baseline CPU spreads and that adverse fresh point
prevent a general CPU or whole-script runtime claim.

The existing five-script desktop diagnostic separately establishes compile
allocation reductions3.15–4.07%. Fresh allocation reductions0.12–1.32%; warm
allocations essentially unchanged. Desktop clocks include adverse points and
identical-role drift up to32.69%in compile/17.37%warm; retain all raw observations,
do not use those timings as demonstrated CPU gains. Goldens, image isolation,
funding and fixture cleanup pass. This is a complete removable-work milestone,
not completion of the substantial whole-script performance objective.

Full Windows/Ubuntu, actual11/10, original Stress and installed-package delivery
is pending on the retained source. Publication remains postponed.
[Focused counts](final-focused.json), [controlled assessment](assessment.json),
[desktop assessment](local-assessment.json), [VM receipt](archive-receipt.json),
[VM raw/source/cores](observations.tgz),
[local source/tests/differential/failure/raw records](local-observations.tgz).
