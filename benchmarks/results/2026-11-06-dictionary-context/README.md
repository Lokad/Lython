# Explicit dictionary context — 2026-11-06

R14 passes the context already available to native dictionary reads and funded
string-key writes into probing. The explicit query boundary owns provenance for
structural/protocol keys. Remove caller ambient installation/rediscovery from
native queries and reuse the existing no-growth mutation for ordinary assignment.
Growth, protocol hashing/equality, first refusal and asynchronous cleanup remain
on their existing paths. No new table, owner, cache, policy or dispatch framework.
KeyError keeps the original key payload; no eager rendering is introduced.

The [current CPU gate](cpu-gate/README.md) selected this work and deferred namespace
mirroring without removing its annotation/lambda lifetime correction. It does not
justify a universal frame/value rewrite or assign all locks/TLS to accounting.

B350 compares the archived patch after `89d81ae3` (core `9e05892e`) with runtime
`66ae029d` (source-equivalent frozen `977da8d6`). Existing worker `b94fa98c`,
protocol2, complete scripts/fixtures and CPython3.13.16; five warmup seconds per
role/job, seven rotating150ms rounds and two independent identical-candidate roles.

| Complete workload / boundary | Elapsed vs reference, candidate / identical role |
| --- | ---: |
| CSV warm | +1.52% / −1.81% |
| CSV compilation plus execution | −6.51% / −7.46% |
| Text warm | −5.20% / −7.80% |
| JSON warm | −4.57% / −5.79% |
| Report warm | −7.92% / −1.54% |

Retain the simpler context boundary. CSV warm is inconclusive with3.28% role
divergence; report has6.92% divergence and cannot support a narrow improvement
claim. CPU ticks are coarse: warm CSV+2.94%/flat, text flat/−8.57%, JSON
−3.12%/−6.06%, report−7.32%/−2.56%. No broad CPU, exact allocation, RSS or
embedding-latency claim. One brief service-status SSH observation during the
collection is retained in the limitations; no samples or adverse roles excluded.

Five jobs completed140.32s, quiet endpoints, actual loaded hashes, full output/file
goldens, funding, Closed, joins and fixture cleanup passed. Mediated RAM I/O,
validation and prior-result cleanup are included; fresh adds compilation/disposal.
Startup/staging/reset/transport/final teardown excluded. Normal GC/tiering.
Raw evidence was archived and verified under the owned collection lease. Terminal
service is inactive, PID0, success/exit0. Its status is cached; do not query again.

Both-target Release build has zero warnings/errors. Focused365internal/1,281public
checks per actual .NET11RC3/.NET10.0.8 pass, including protocol effects, native
mutation, refusal, retained aliases and real I/O. A contextual-hash cancellation
regression verifies the active origin and restoration of outer provenance.
Full local .NET11 ordinary suites pass:4,260internal/16,187public. Combined
R13C+R14 runtime62141d4d is fully qualified by terminal CI42243450458, GREEN
all8 Windows/Ubuntu, both runtimes, original Stress and installed-package gates.
Local installed consumers pass actual.NET11RC3/.NET10.0.8. Terminal status is
cached in delivery-ci.json; never query that completed run again.
Publication remains postponed. Whole-script performance and independent regex
development remain active.
