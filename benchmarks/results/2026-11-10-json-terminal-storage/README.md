# Terminal JSON buffers: allocation and ownership

Two terminal JSON construction sites now use the existing completed-buffer
transfer instead of copying into another byte array and immediately releasing the
builder. Reused `iterencode` chunk builders retain their independent-copy behavior.
No new owner, pool, accounting mechanism or policy is introduced.

The images compare qualified `cac4acf8`-equivalent production against `fcf018d2`
plus these two changes, before the later handler-call eligibility change. Exact
source/binary hashes are in [identity](identity.json); the source, cores, original
conditions, commands and observations are in [the archive](observations.tar.xz).
The existing complete-script diagnostic driver was reused. These are Windows
desktop diagnostics, **not CPU qualification or a current CPython comparison**.

The ordinary report/config catalog does not materially exercise eager JSON
serialization. A complete JSON-lines manifest fills that coverage gap: read
189,441 bytes of records, serialize all 3,072 records, write a 189,440-byte file,
then print its record count. Independent isolated CPython 3.13.2 supplies the
golden, not a timed baseline. Fixture size parameter 1,024 generates 3,072 records;
the canonical report ID is imposed by the existing driver and does not identify
the published Markdown report. [Golden/source identity](jsonl-golden.json).

| JSON-lines manifest | CLR allocation | Diagnostic elapsed |
| --- | --- | --- |
| Warm synchronous | −3.04% | −4.96% to +1.24% |
| Warm asynchronous | −3.03% | +8.65% to +11.59% |
| Compilation | Unchanged | Inconclusive |

All complete outputs, zero balances, isolated images and owned-fixture cleanup
passed. The unchanged text control moved +15.83–29.05% synchronously, and other
controls also moved substantially. The adverse async result remains visible;
neither CPU improvement nor CPU equivalence is established. Warm execution and
compilation have separate lanes. Raw elapsed/CPU/allocation observations and
boundary metadata are preserved rather than interpreting a noisy control away.

Existing public-probe peak reporting, with identical auxiliary binaries and a
supplied 16 MiB allowance, checks the capacity tradeoff on both APIs:

| Workload | Before execution peak | After execution peak | Projection peak |
| --- | --- | --- | --- |
| Large Unicode JSON result | 84,653 B | 76,454 B | 24,698 B, unchanged |
| Bounded 32-record sequence | 60,688 B | 72,807 B | 516 B, unchanged |

These are accounted peaks, not physical CLR memory. Adopting storage removes
construction overlap but retains unused backing capacity. Conservative retention
and earlier denial remain possible; there is no general live-memory reduction
claim. [Peak observations](peak.json) retain sources, current result disposal,
tool/core identities and stdout goldens. No forced collection or timing is used.

Focused Release checks passed 193 internal and 861 public cases on each actual
.NET 11/.NET 10 target. Coverage includes full backing-capacity funding, metadata
admission before publication, first refusal, rollback, aliases, hook/error order,
cancellation and cleanup. The public probe now disposes each result after eagerly
snapshotting its response. Combined platform/Stress/installed-consumer delivery
qualification is pending and is not implied by these focused results.

Original failing refusal fixtures, the subsequent wrong-property compile error
and the Windows CRLF stdout setup rejection are retained. The corrected stdout
golden normalizes only process stdout; file bytes remain exact. No observations
were discarded. The allocation/structural simplification is retained with the
capacity tradeoff and unresolved elapsed evidence explicitly recorded.
