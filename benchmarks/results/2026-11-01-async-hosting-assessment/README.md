# Asynchronous hosting CPU-path assessment (B50)

2026-11-01, same frozen B48 runtime as [B49](../2026-11-01-keyword-call-assessment/README.md):
core `ecf3ee0fa0318e0143d01483ad8f27343e5d5e4bfe36c5f616b2a50b4fa8bde6`, source
082dd130 + B48 source.patch. **CSV supports testing a shared ordinary instruction
loop. Text is inconclusive. No runtime candidate or speedup is measured here.**

## Same-script API comparison

The [prospective criteria](pre-code-plan.md) require >=15% complete process-CPU gap
against both stable Run controls on at least one script. Reuse existing complete
CSV/text helpers, fixtures and full stdout/file goldens; only Run/RunAsync changes.
RunAsync uses a synchronously completing efficient file host. This measures hosting
strategy for the same work, not overlapping I/O or CPython performance.

Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, .NET11RC3,
ordinary GC/tiering. One **27.984s collection /28.010s service**, six ordered
Run/RunAsync/identical-Run processes. Four warmups, seven batches of64 complete jobs.
The existing external driver joins the public Task before reading its Result via
one cached reflection getter; that extra getter is diagnostic plumbing, not guest
work. It does not justify attributing the entire gap to interpreter awaiters.

| Complete warm job | Run CPU ms | RunAsync CPU ms | Identical Run CPU ms | API CPU change |
| --- | ---: | ---: | ---: | --- |
| CSV helpers | 8.43250 | 10.39170 | 8.26241 | +23.23%/+25.77% |
| Text helpers | 5.98806 | 7.32711 | 6.84359 | Inconclusive: Run control drifts+14.29% |

CSV elapsed8.417/9.714/8.366ms (+15.41%/+16.11%). CSV control CPU drift-2.02%
and allocation drift below0.2% pass5%/0.2% gates. All CPU batches resolve>100ms.
Text controls fail; preserve every observation, no retiming. [Summary](summary.json)
retains all elapsed/allocation observations and controls. These are short
process diagnostics, with ongoing tiering, not steady-state or causal fractions.

Warm public execution includes fixture reset, mediated real file I/O, full golden
validation and result disposal; compilation/startup excluded. Shared unbounded
funding exercises normal ownership. Both quiet gates, all goldens, budget balances
and child exits pass; service inactive/dead/success, VM lease available.

## One allowed attribution capture

[Criteria before capture](pre-profile-plan.md):512 warm-async CSV jobs,199Hz
cpu-clock/fp with CLR perf maps. **4.951s capture /11.984s owned service**,803 actual
named guest-thread samples,64 unknown. Guest CPU4.04s; tiered-JIT0.70s and all other
thread deltas remain visible. Before/after quiet and full-golden checks pass;
worker/profiler close/join. No recapture. Native diagnostics stay outside production.

The async instruction loop MoveNext has86 self samples (largest named leaf).
CsvDictReader.NextAsync29; Monitor enter/exit53; ResolveName25; CsvRecordParser.Feed21;
ExecuteStackTransfer15. Additional async iteration/comparison/file operations are
distributed. Inlining/missing/native frames and ongoing tiering limit attribution.
No count is a removable CPU fraction or explains the complete API gap.

Source confirms compiled functions/caller chains, worker reuse, receiver paths and
many exact scalar operations are already shared. A second async opcode loop still
copies each instruction into its state machine and duplicates block/PC/checkpoint/
abrupt routing. Test one coherent consolidation: ordinary nonasync dispatch shared
by both APIs, awaiting only an actually pending operation, with the existing real
async operation helpers and cleanup behavior. Freeze deletions, semantics and a
controlled materiality criterion in PLAN.md before implementation. Do not build a
universal dispatcher, alternate backend, new policy or frame pool.

[Loaded identities](worker-identity.json), collectors/analyzers and
[raw results](raw-results.tar.gz), SHA256
`d6f78be060470ab40a289f80b9bf472bba7201fd7f5be243b6fe8a7df07ae336`,
preserve all samples, sources/driver, phase boundaries, goldens, map irregularities,
machine gates and complete caller/native origins. Stop B50 after this decision.
