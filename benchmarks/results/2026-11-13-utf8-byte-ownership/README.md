# Shared UTF8 byte decoding ownership

2026-11-13UTC, R334 frozen0b2929ca against committedR333/7db788d7/frozena1f31dcd.
Retain this as an ownership/construction simplification, **not a CPU improvement**.
The last full qualification is R331; combined R333/R334 delivery is pending.

One strict scalar-UTF8 boundary serves owned bytes.decode/str(bytes,encoding),
JSON and XML. It reuses existing immutable PyBytes windows and private-input
retention, admitting the new text shell while keeping source capacity funded.
JSON's duplicate validation loop disappears. No new owner, ledger, pool, cache,
quota, option or unsafe code is introduced. Other codecs/error modes and
unrelated-owner adoption remain on their existing paths.

The initial source hypothesis is corrected before editing: whole-array decoding
already avoided a physical copy but charged the same capacity again. Sliced/BOM
input copied. The final change removes duplicate same-owner funding and sliced
copies. Once shared, payload funding can remain conservative until command-owner
cleanup even if a temporary text value is withdrawn. Tiny-budget thresholds are
not a compatibility promise; no live alias is refunded.

## Focused correctness and allocation

45internal/218public focused Release cases pass per actual.NET11/10, then25
owner/ElementTree cases per target (10 overlap the first group;60distinct internal
cases total). They cover same/unowned/separate owners, cleanup to baseline,
admission refusal/cancellation before publication, scalar chunk edges, retained
results across two executions, actual pending I/O, encoding/error/newline behavior
and the XML key-protocol correction. Two isolated CPython3.13.2 complete-script
controls agree on stdout/files and unchanged input.

Two fresh isolated processes/source, existing allocation harness,4s warmups and
five64-invocation batches: automatic binary XML allocates38,409–38,414B less,
about1.874%; explicit UTF8-BOM decoding38,483–38,484B less, about1.877%.
Unchanged text control is flat (0..+0.00056%). Full-reset/I/O/output validation/
result cleanup are included; compilation/startup/final caches excluded. Process-wide
CLR allocation counters include incidental/JIT traffic; no forced GC or elapsed
gain is inferred. Borrowed-capacity assertions independently prove the funding change.

## One bounded source-pair comparison

| Whole script / phase | Elapsed change | Wider CPU change | Candidate/reference process spread | Max IQR |
| --- | ---: | ---: | ---: | ---: |
|xml-settings.bytes /warm|+1.32..+2.35%|+1.14..+3.54%|0.14/0.88%|1.15%|
|xml-settings.decode /warm|-0.52..+2.30%|-0.37..+2.58%|1.95/0.87%|2.13%|
|xml-settings.decode /compile-run|+0.21..+0.35%|+0.83..+1.86%|0.03/0.11%|2.31%|
|xml-settings /warm|-0.12..+0.06%|-1.72..+0.76%|0.13/0.06%|1.73%|

B705completes188.411s/all gates under one300s owner, quiet
AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary workstation GC/tiering, fixed B685
workeracda3ef3/optional adapter0.4. Two independent processes per source/job,
eight-second full-request wall warmups, five rotating0.5s wall-sized batches.
Each case has identical source/fixture/output bytes across revisions. The two
affected scripts read the same512-setting XML file as binary UTF8+BOM, then select
fields and write a report; text control retains its original source/fixture.

Guest clocks include mediated RAM-file I/O, complete stdout/file validation and
prior-result cleanup. Compile-to-result adds compilation/compiled-owner disposal;
startup, fixture reset/protocol and final cache teardown are excluded. Wider CPU
includes reset/protocol. Ranges pair process medians, not confidence intervals.
There is no new CPython timing, embedding-tail or live-heap comparison.

Automatic bytes has a small consistent warm cost (+1.32..+2.35%, roughly12–21µs),
with supporting wider CPU adversity. Explicit warm is mixed; fresh elapsed has a
small +0.21..+0.35% cost and widerCPU+0.83..+1.86%. Text control remains flat.
Accept this recorded CPU debt for the common ownership boundary and copy/funding
removal. No further unchanged retiming at this checkpoint; materially different
refinement remains possible. This does not complete the performance objective.

All image/source/golden/balance/quiet/join/archive gates pass. Raw observations
stay persistent; users join and RAM fixtures unmount before the archive is
independently SHA-verified. No working evidence/image retired or disk floor
changed; no VM users remain. Publication/tagging remain postponed.

[Allocation](allocation-summary.json), [timing/wider CPU](summary.json),
[raw](observations.tgz), [exact frozen source/bits and checks](source-checks.tar.xz).
