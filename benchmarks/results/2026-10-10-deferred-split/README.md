# Fresh split ownership, 2026-10-10

The accepted structural change keeps untouched fresh split strings funded under
their list, and publishes an independent registration when an item escapes.
Native synchronous join can borrow the list's strings. Two short comparisons
show split/count taking 64.79/70.75% less time, split/join 58.23/59.62% less,
and single-item extraction 64.53/63.48% less than the preceding runtime.

These are diagnostic results for accepting this improvement round. They do not
qualify a Python speed multiplier. The second split/count baseline has 12.91%
IQR/median; keep that variation. Loop and empty controls rise slightly, and
Unicode controls vary. No full comparison lane ran, and no native-handle cost
share or CSV lifetime cause is established.

## Frozen comparison

Baseline: `ce61d05097f96e69abb8d060a1518e08f82aed58`, production tree
`cce0331ac74a1348f86312e2ead4d2e4b7c658da`.
Initial all-at-once candidate: `df9be8b12bced6e50fc06d9e9c17541e56d5f249`.
Refined per-item candidate: `5be2e72befc3b22663b7e0002c6542f49489106c`, production
tree `fd4c4a7a223976c27f882119aab44b350d470f33`, test tree
`177887380335e4a8e404ee54d94f8f71805f77a9`.

All producers use Release SDK 10.0.401/runtime 10.0.12 on the same dedicated
four-core AMD EPYC 9V45 Ubuntu 24.04.4 VM. Lython workers keep default limits,
unset instruction fuel, ordinary workstation/Interactive GC, tiering and PGO,
and no runtime overrides or forced collection. CPython 3.13.16 uses a PGO/LTO
build, ordinary GC/GIL, no JIT, and isolated `-I -S` workers. Build preparation
and correctness are outside these short performance collections.

The 14 canonical cases remain unchanged. Two added diagnostics use the same
1,024-copy `abZ!::tail/` fixture as `strings.pipeline-ascii.medium`: replace `::`
with `/`, split on `/`, then print either the length (`2049`) or item 1024
(`abZ!`). The canonical pipeline prints the joined complete content. Fixtures,
sources, output hashes and before/after goldens match across all three workers.

Each source comparison declared two repetitions before timing: empty, integer
loop, split/count, split/join, split/index, Unicode scan; then repeat that order.
The existing micro command uses seven measured batches per engine with rotating
order, and preserves every warmup, calibration and verification response.
Every micro completes in 6.37–6.99 seconds under an owned 30-second hard
process-group deadline with zero stop grace. All workers are absent afterward,
producer/input hashes still match, sources are clean and the shared VM lease is
free. Full ten-minute lanes remain reserved for separately declared milestones.

## Refined per-item results

Times are median microseconds per warm public invocation. IQR columns are
IQR/median percentages. Change compares candidate time with its paired baseline;
a negative value means less time. CPython medians are context, without certified
cross-language ratios. [All 24 rows](micro-results.csv) include both candidates.

| Case / repetition | Baseline us | Candidate us | CPython us | Baseline IQR % | Candidate IQR % | CPython IQR % | Change % |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.58 | 24.20 | 1.46 | 4.60 | 3.80 | 0.76 | +2.60 |
| Loop / 1 | 1644.54 | 1668.21 | 600.43 | 0.82 | 0.98 | 0.09 | +1.44 |
| Split/count / 1 | 282.20 | 99.37 | 32.40 | 2.00 | 3.80 | 0.28 | -64.79 |
| Split/join / 1 | 330.58 | 138.07 | 41.39 | 2.95 | 2.32 | 0.21 | -58.23 |
| Split/index / 1 | 280.80 | 99.60 | 33.81 | 10.82 | 1.42 | 0.30 | -64.53 |
| Unicode scan / 1 | 872.34 | 875.16 | 220.29 | 0.82 | 0.77 | 0.06 | +0.32 |
| Empty / 2 | 23.10 | 23.28 | 1.48 | 4.21 | 4.03 | 0.44 | +0.79 |
| Loop / 2 | 1632.60 | 1664.14 | 638.48 | 0.58 | 0.43 | 0.11 | +1.93 |
| Split/count / 2 | 345.43 | 101.04 | 32.40 | 12.91 | 3.02 | 0.45 | -70.75 |
| Split/join / 2 | 344.69 | 139.19 | 41.47 | 9.62 | 1.91 | 0.32 | -59.62 |
| Split/index / 2 | 278.32 | 101.65 | 32.28 | 8.74 | 3.75 | 0.28 | -63.48 |
| Unicode scan / 2 | 914.89 | 876.71 | 218.01 | 0.78 | 0.93 | 0.34 | -4.17 |

Loop rises 1.44/1.93% and empty 2.60/0.79%; retain these observations rather than
declaring exact zero overhead. Unicode is +0.32/-4.17%. The loop/empty paths
have no new split decorator or publication work. A larger milestone can assess
small control changes; their variation does not justify attributing native
costs or certifying a multiplier from these seconds-long collections.

## Superseded initial model

The initial candidate registers every part on any ordinary read. Its split/count
time falls 64.86/65.57%, and split/join 57.79/58.74%, but extracting one item
increases time 15.56/11.32%. It is superseded. The refinement registers only the
requested identity; slices acquire selected identities and whole storage copies
or mutations acquire the remainder. No same-source timing was repeated to seek
a favorable result. Both original controls and target receipts remain retained.

## Correctness and failed preflights

The [ownership design](OWNERSHIP.md) describes funded metadata, independent
escapes, exact snapshots and denial recovery. No PyList/PyString field is added,
no resource budget is relaxed, and sync join retains its checked iteration
policy. Async join keeps ordinary acquisition. Thirty new ownership cases and
two public sync/async cases cover funding, partial failures/retries, aliases,
copy/slice/mutation, Unicode cache updates, parent/sibling collection, clear,
projection and cancellation.

A separate ordinary-object regression fails on the unchanged baseline: with one
byte of headroom, a newest live entry's promotion denies before the sweep reaches
an older dead entry. Draining collected young targets before promotion resolves
the false denial; 31 focused checks pass after that independent correction.
Its original failure is retained. This is not evidence for the C05 CSV cause.

The original prototype also needed a progress correction: publishing a prefunded
entry must restore reclamation eligibility after an earlier caught denial,
even though no entry fee is newly committed. Its deterministic failed regression
is preserved. Successful publication now marks progress without changing totals
or disabling the repeated-denial guard.

Before any timing, an untimed Python golden preflight caught a formatting error
in the two added diagnostics. The original catalog is retained; all 16 corrected
goldens pass pinned isolated CPython. The refined VM preparation later exited 1
solely because its final checksum command referenced a wrong filename, after
both builds, tests, shutdown and producer hashes succeeded. The failed service
receipt is retained, and a separate audit verifies those original outputs;
preparation was not repeated. These are tooling failures, not runtime results.

Initial candidate Linux/local Release passes 1,352 white-box and 1,033 selected
public checks; baseline Linux passes 1,323/1,031. Refined candidate Linux/local
Release passes 1,354/1,033 with matching probes. Full Debug passes all 9,104
checks (1,354 white-box and 7,750 public) after a matching Debug probe build.
The committed runtime's production/test trees exactly match the measured and
tested refined producer. Exact final-head Windows/Ubuntu Release/package CI
remains the delivery gate.

## Evidence and reproduction

Private original receipts live under `.git/agent-notes/deferred-split-ownership-20261010/`.
They include prospective declarations, frozen source bundles, original failures,
raw worker responses, all TRX/build logs, script and producer hashes, and cleanup
proofs. Independent audits validate 1,056 responses and recompute all 72 medians
and IQRs from measured ticks and invocation counts. The [evidence manifest](evidence.json)
binds receipt hashes and producers to the [complete numeric export](micro-results.csv).

Build both frozen workers and the matching probe in Release with the pinned SDK;
perform correctness checks before collection. The existing micro command accepts
explicit executables, the unchanged catalog plus the two diagnostic cases,
and a fresh receipt path:

```text
<dotnet> <candidate-benchmark.dll> --compare micro --catalog <catalog.json> --dotnet <dotnet> --baseline-worker <baseline-benchmark.dll> --python <python> --python-worker <cpython-worker.py> --out <new-receipt.json> --case <case-id>
```

Run each invocation in an owned 30-second service/process group, preserve
ordinary worker options, prospectively declare order/repetitions and verify
hashes, goldens and cleanup afterward. Do not substitute a full comparison lane
for a routine micro round. Original pipeline-cause diagnosis, C05, U01 and
milestone qualification remain separate pending work.
