# Shared recursive JSON container grammar — B200

2026-11-04. Parent a805be97, same production source as qualified ee2b4529.
**Retained for simplification; focused checks pass, full delivery pending.**
No CPU or allocation improvement is established. Publication remains postponed.

One recursive container reader replaces the dictionary-object, pairs-hook-object
and array grammar loops: **95 net production lines and one generated state-machine
type removed**. Scalar scanners, direct storage, recursive callback suspension,
depth checks, property-name snapshots and ownership remain. No explicit parent
workspace, alternate parser, new owner, option or policy. Earlier B137/B176 used
manual continuation state; this change keeps ordinary recursive ValueTasks.

The initial shared reader used21 state fields. Before timing, one payload owner
replaces two nullable container references and the redundant saved closer. Final
state has19 fields, versus19 for the prior object reader and13 for its array reader.
This does not prove lower per-call memory: array state is larger. There is no new
state-size precision guarantee. Existing container admission and live funding stay.

## Controlled complete jobs

Quiet Azure F4as_v7, Ubuntu24.04.4, four EPYC9V45 vCPUs, .NET11 RC3, normal GC/tiering.
Ten jobs, five scripts BOTH APIs, each with independent before/candidate/after
workers;6s warmup per image, nine rotating~200ms samples. Source/core Ready identities,
stdout/file goldens, ownership balances, machine gates, cleanup and joins PASS.
Capture310.982s / complete owned service311.593s, below420s bound.

Compilation/startup/fixture reset/response hashing are excluded. Warm execution,
actual mediated RAM-file I/O, full checks and prior-result disposal are included.
Final cached-owner teardown is excluded from the clock but completed and joined.
Process CPU is a separate Linux tick diagnostic, including worker/reset overhead;
no isolated CPU improvement is claimed.

Elapsed change in percent; negative is faster. Each pair compares candidate with
the first and second unchanged old worker. Last column is old/old change.

| Complete script | Sync vs old roles | Async vs old roles | Old-role sync / async |
| --- | ---: | ---: | ---: |
| CSV helpers | +2.67 / −0.95 | −0.76 / −1.57 | +3.65 / +0.82 |
| Text helpers | −1.65 / −1.34 | **+2.95 / +2.68** | −0.31 / +0.27 |
| JSON helpers | −0.06 / +0.90 | +0.81 / −1.21 | −0.96 / +2.04 |
| Report render | −1.23 / −0.68 | **+4.01 / +3.22** | −0.56 / +0.76 |
| Configuration | +0.98 / +1.06 | −0.09 / −1.76 | −0.09 / +1.70 |

Allocation through the existing three-isolated-image eight-case driver is effectively
flat (about±0.01%) on both APIs. Admitted execution/root peaks match. There is no
external CPython refresh; current primary comparisons identify their older source
in [BENCHMARKS.md](../../../docs/BENCHMARKS.md).

**Debt decision:** retain the code deletion, with async report3–4% and text2.7–3%
explicitly OPEN. Text IQR0.3–0.9%, report2.0–4.0%; codegen/tiering sensitivity is
possible, not an established explanation. The synchronous points do not erase these
adverse results. B201's native formatted-return/frame milestone will make a combined
retention/correction/withdrawal decision before the full delivery checkpoint.
An independent report improvement cannot explain an unaffected text regression.
No repeated unchanged campaign or fixed improvement threshold is required.

## Correctness and source assessment

Final focused Release **178 internal /815 public per actual .NET11 and10 PASS**,
nonincremental solution build, zero warnings/errors. Existing controls cover
duplicates/order, pairs-hook precedence, nested/reentrant JSON, strict/raw/Unicode
errors and offsets, actual suspended host callbacks, managed nesting, shared
allowance, terminal refusal, retained aliases and awaited cleanup. No assertions
or timeouts changed and no architecture-specific tests were added.

The preceding B199 instruction census is compile-only evidence. Text's hot loop
has17 instructions,9 local/constant transfers; report helper has19 instructions.
The earlier B147/B185 operand plans already removed related loads but added
metadata/input dispatch and measured script costs. Register work stays open only
with a different complete cost/lifetime model; no duplicate prototype was built.

Frozen candidate SHA256:
`68899ba8c4740f7bcd523d8ea3cd548a432f84ccc1dfad7d61a17df0dfae41c2`.
Baseline:
`1b9b3230ca075b32ac8fbd3e117605f93499b9356b4afcd768c76ca4a535ca07`.
Raw archive SHA256:
`49001622aa1a2520635723af5b45bc735f52e668b43cdbf81691aae219f18a5f`.

`observations.tgz` retains original source patch, identities, manifest, all responses,
controls, gates and conditions. Summaries/receipts are alongside it. State-field
inventories preserve initial and final layouts. `qualification-and-source-assessment.tgz`
keeps all preliminary/final focused TRX, the initial source implementation and B199
source/census evidence. Full Windows/Ubuntu, stress and installed-consumer qualification
is pending and is not implied by these focused checks.
