# Compiler child traversal

Retained. This changes the three shared frontend child-walk helpers, removing
ordinary per-node iterator objects without an AST cache, edge inventory, visitor
framework or new compiler. Baseline is `ee88ca25`; candidate is that revision plus
the exact [source.patch](source.patch). The existing passes, scope rules,
diagnostic order and emitted execution strategy remain unchanged. Complex
formatted/comprehension/target/header and optional-slice walks retain their real
nested iterators. Common yield cases are deleted rather than copied into another
execution route. The public API is unchanged.

## Hypothesis and acceptance

The [existing text compilation profile](../2026-10-31-architecture-profiles/README.md)
attributes approximately 77.1 MB of 181.9 MB sampled allocation ticks to the
three central walkers over 512 compilations (42.4%). Actual CPU leaves include
nine expression-iterator MoveNext/Dispose samples out of 137. These are allocation
and sampled self-cost findings, not a 42% elapsed attribution.

One prototype was bounded to ordinary direct-field/list enumeration across those
three helpers. Acceptance required at least 20% lower complete-compilation
allocation on two nontrivial existing scripts, unchanged semantics/ownership and
no repeatable material compile/run/control regression. No pass merger, runtime
frame pool, lexer, tagged-value or interpreter redesign was included. Existing
upfront compilation admission and cancellation remain in place; no working
storage escapes the walk or needs another owner.

## Complete-compilation allocation

One fixed local before / after / identical-before observation uses the
[existing allocation diagnostic's adapted driver](driver/Program.cs). It performs
complete public Compile(source, sharedBudget) and script disposal, checks validity
and verifies zero accounting balance after each batch. It excludes execution and
process startup; every call parses/analyzes/builds a fresh program. Reflection
overhead is common. Four warmups precede seven rounds of five compilations per
shape. No other local Lython build/test overlaps collection; all owned processes
finish within their 30-second caps. There is no forced GC.

| Existing script | UTF-8 source bytes | Before B / compile | After B / compile | Reduction |
| --- | ---: | ---: | ---: | ---: |
| Configuration edit, small input | 357 | 307,560 | 182,744 | 40.6% |
| File search, large input | 319 | 256,458 | 166,728 | 35.0% |
| CSV helpers, large input | 599 | 450,768 | 268,448 | 40.4% |
| Text helpers, large input | 424 | 360,504 | 226,208 | 37.3% |
| JSON-lines helpers, large input | 607 | 420,520 | 250,400 | 40.5% |
| File manifest helpers, large input | 574 | 508,272 | 309,184 | 39.2% |

The identical-before allocation control matches the baselines. These are managed
allocated bytes, not retained storage or a process-memory limit. Windows
10.0.26200, 28 logical processors, .NET 11 RC3, tiering disabled equally and
ordinary GC. Local timings remain unqualified. [Before](before.json),
[after](after.json) and [identical-before](before-control.json) preserve every
round and loaded assembly hash. Their catalog hash identifies the existing
[representative sources and goldens](../2026-10-31-architecture-profiles/representative.json),
also used by the VM collection below.

## Fixed VM compilation and complete-script controls

2026-10-31 UTC, dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs,
16 GiB; identical .NET 11 RC3 and CPython 3.13.16. The fixed twenty-job collection
takes **162.08 seconds**, under its 220-second total bound, holding the shared
VM lease. Both machine quiet gates pass. The maintained micro command rotates
baseline/candidate/CPython order across seven 25-ms batches, one-second warmups
and settling. Workers use ordinary GC/tiering; only supervisor tiering is disabled. Each owned
process group has a 30-second external deadline and joins cleanup.

Compilation microseconds, baseline / candidate:

| Script | Observation 1 | Observation 2 | Status |
| --- | ---: | ---: | --- |
| CSV helpers | 240.1 / 166.8 | 298.7 / 237.5 | 30.5% / 20.5% lower point medians; noisy |
| Text helpers | 149.6 / 123.7 | 212.3 / 121.1 | 17.3% / 43.0% lower; second baseline noisy |
| JSON-lines helpers | 288.9 / 152.5 | 287.8 / 149.1 | 47.2% / 48.2% lower; baselines noisy |

Baseline/candidate IQR fractions reach 19.4% for CSV, the second text baseline
19.3%, and JSON baselines 18.4%/15.7%. Keep these limitations: the point changes
support the allocation finding but do not establish a precise multiplier.
CPython compilation context remains in every receipt; this experiment does not
replace the primary CPython table or relabel its measured revision.

Complete fresh compile/run remains inconclusive. Text reverses from 37.5% higher
to 1.7% lower; JSON changes from 27.7% lower to 1.0% lower; file search reverses
from 7.6% higher to 39.8% lower. CSV is essentially unchanged (-0.1%/+0.3%);
configuration control point medians are 4.1%/2.0% higher. The compilation saving
is small beside whole-script host/execution costs. Warm execution controls are
mixed: text +3.2%/-1.3%, JSON +0.0%/-1.4%. There is no repeatable material
complete-script regression or qualified overall execution speedup in these short
observations. Do not subtract unrelated medians to infer compilation or I/O time.

Every baseline/candidate/CPython job validates complete stdout/file goldens before
and after timing. [collection.json](collection.json) retains all rows and IQRs;
[raw-results.tar.gz](raw-results.tar.gz) preserves all samples, identities,
requests/responses, machine gates, full logs and the exact collection source/patch.
The [collection driver](collect.py) records its fixed lanes and stopping budget.
The existing phase diagnostic separately documents I/O/setup/cleanup boundaries;
this collection adds no runtime instrumentation or measurement framework.

## Validation and stopping decision

Release solution builds cleanly for both targets. Each target passes 232 focused
native and 4,382 public syntax/function/class/generator/scoping/annotation/
diagnostic/execution/memory checks, including admission, cancellation, retained
aliases and real suspension. Existing direct-child/body-order assertions remain;
their collectors are adapted to value enumeration, with no relaxed semantics.
Per-compilation borrowed-budget balances return to zero without collection.

Retain the shared traversal protocol for its 35–41% measured allocation reduction
and consistent compilation direction. Stop this redesign; do not tune its
individual branches, merge passes or add syntax caches to rescue elapsed rows.
Most warm interpreter/CPython gaps remain open. The fully qualified runtime
foundation and this frontend's focused delta have separate source identities in
PLAN.md; this experiment makes no claim of unchanged full-platform qualification
for the new frontend commit.

VM tiering description corrected 2026-11-01 from [recorded worker identities](../2026-11-01-native-utf8-errors/tiering-correction.json).
The supervisor alone disables tiering; both workers retain ordinary settings.
Local Windows allocation diagnostics still use their stated disabled tiering.
Raw measurements and performance conclusions are unchanged.
