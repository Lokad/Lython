# Direct arithmetic operands — B185

2026-11-03. **WITHDRAWN.** Runtime source restored to the fully qualified B183
implementation. A focused native arithmetic-return/forwarding regression remains.
This is a decision about this representation, not a ban on operand redesign.

The prototype used the spare second arithmetic-instruction operand to reference
constant-pool leaf sources. Adjacent local/closure/global/name/constant loads were
removed; existing synchronous/asynchronous operator helpers received original
values directly. Stack layout counted remaining stack inputs, and helper-return
proof visited the same sources in original order. Existing subscript/type-protocol
slot lifetimes remained. No new backend, pool, ownership ledger or public mode.

The design removed fetch dispatch and stack transport, but added source records,
metadata/proof links and uniform input-read work, including unaffected operations.
Allocation is essentially unchanged. Its complete-script outcome does not justify
that added machinery. Retain the mechanism/cost lesson rather than another unchanged
retime or a permanent percentage-based veto. The CPU cause of individual deltas
has not been established; no claim that dispatch alone explains them.

## Correctness and qualification limits

Focused .NET11:48 initial internal controls,53 broader native/stack/forwarding
controls,161 public controls PASS. Real operator suspension, evaluation order,
cancellation, terminal admission refusal, retained aliases and budget reuse are
covered by existing tests. New native-return proof/forwarding control passes.
Actual .NET10 broader selection:393 pass/one old architecture-shape fixture fails
(expected two constants, actual three). Its exact standalone-load layout was not
rewritten to qualify a discarded design. See [original result](focused-validation.txt).
No full platform/package qualification is claimed for the prototype. After source
restoration, solution-wide nonincremental Release rebuild PASS,0warnings/errors;
**394 internal/161 public focused checks per actual .NET11/.NET10 PASS**.

## Controlled comparison

Qualified B183 source7a3a8afe versus 36137270+B185 patch, frozen hashes in
[identity](identity.json). Existing protocol2 workers, five complete scripts through
both APIs, CSV/report compilation separately. Three separate workers per job:
old/candidate/unchanged old. Normal GC/tiering; Azure F4as_v7 Ubuntu24.04.4,
.NET11 RC3. Persistent workers/catalogs/evidence; guest fixtures on tmpfs.
1.5s warmup per worker, seven rotated ~150ms batches, .5s settle. Do not pool these
observations with earlier three-second/nine-batch profiles.

Warm timing includes invocation, mediated guest I/O, complete output/file validation
and prior-result cleanup. Fixture staging/reset and final cached teardown/startup
are outside clocks; all users/fixtures are joined/removed. Compile timings cover
compilation/disposal alone. Actual suspended host I/O and CPython are not retimed.
Owned service143.779s, collection143.165s, exit0; all identity,
golden, machine-quiet, balance, fixture cleanup and join gates PASS. Systemd's small
service memory peak is not embedding-process RSS evidence.

Positive percentages mean slower. Candidate versus **each** old worker is shown;
old-control column is the second old worker versus the first. Keep all adverse rows.

| Lane | Complete script | Candidate / first old, candidate / second old | Old control |
| --- | --- | --- | --- |
| warm | csv-summary-helpers | +0.33% / +0.76% | -0.43% |
| warm | text-summary-helpers | +5.48% / +4.99% | +0.46% |
| warm | json-summary-helpers | -1.90% / -0.42% | -1.49% |
| warm | report-render | +5.52% / +0.16% | +5.34% |
| warm | config-edit | -16.30% / -6.34% | -10.63% |
| warm-async | csv-summary-helpers | -3.70% / -1.03% | -2.69% |
| warm-async | text-summary-helpers | -4.35% / +1.18% | -5.46% |
| warm-async | json-summary-helpers | +2.34% / +1.63% | +0.70% |
| warm-async | report-render | +6.31% / +8.60% | -2.11% |
| warm-async | config-edit | -14.54% / -2.24% | -12.58% |
| compile | csv-summary-helpers | -5.50% / -0.41% | -5.11% |
| compile | report-render | -9.77% / -1.00% | -8.86% |

Sync text is about5% slower than both old workers; async report is6–9% slower,
async JSON1.6–2.3% slower. CSV is near controls or modestly lower async. Report sync
straddles a moving control; config/compilation controls move5–13%, so apparently
large gains there are not reliable improvement claims. Allocation diagnostic through
existing three-isolated-image driver (32 warmups, five16-job batches, both APIs)
is effectively flat: complete scripts range−0.01..+0.02%. Accounted peaks and raw
observations are retained. There is no established general CPU/allocation gain.

Do not close the current accepted CSV+1–2% / fresh JSON+1.8–1.9% debt with this
withdrawn experiment. Current CPython evidence remains B184. Publication postponed;
whole-script performance objective stays OPEN.

[Summary](summary.json), [raw observations](observations.tgz),
[exact tested source snapshot](source-snapshot.tgz), [source patch](source.patch.gz),
[collector](collect.py), [service result](service.log).
Raw archive SHA256:162641e495d3ec4fa77909e951d5195124bdf5dd67864d4635862663850a3f22.
Allocation driver source/unchanged tooling: [B183](../2026-11-03-reader-owned-windows/README.md).
