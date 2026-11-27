# B1871 Attribute nested JSON reads and file export

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 27,
2026 UTC. It reused the frozen `6e59cd41e` worker from B1869. The only
runtime source difference at inspected `43ef47bba` was `CsvDictWriter`
lookup, which neither workload uses. This capture measures CPU stacks,
including host I/O, fixture reset, verification and cleanup.

The nested report loads 2,048 records and totals nested quantities in
`Run`. The async export loads 768 records from 47,361 UTF-8 bytes, changes
fields and writes 54,272 JSON Lines bytes through `json.dumps` and
`file.write`. Both cases match independent CPython 3.13.2 output and file
goldens. The worker warms at least eight seconds per case, then CPU stacks
are sampled at 199 Hz for about six seconds.

| Observation | Nested document | JSON Lines export |
| --- | ---: | ---: |
| Verified commands during capture | 7,680 | 5,632 |
| All samples | 1,276 | 1,315 |
| Samples with a resolved runtime ancestor | 1,070 | 1,189 |
| Samples with an unresolved leaf | 324 | 229 |
| Nearest iterative JSON construction loop | 150 | 48 |
| Nearest JSON string read | 93 | 34 |
| Nearest JSON string emission | 0 | 66 |
| Nearest builder growth | 2 | 61 |
| Nearest explicit reservation called by builder growth | 1 | 63 |
| Nearest explicit reservation called by record creation | 75 | 24 |
| Nearest explicit reservation called by UTF-8 copy | 75 | 5 |

Record and string admissions on the read path fund final guest values. The
export identifies a more contained candidate. Replace the scalar loop over
ordinary ASCII with bounded span scanning. Admit a complete plain quoted
token in one write window, removing intermediate capacity growth where the
quotes and body previously crossed separate boundaries. Preserve strict UTF-8,
escape semantics, published chunks and cancellation. Compare the complete
export, an escaped export and the unchanged nested read before deciding.
Sample counts locate work and give no estimate of the attainable speedup.

Both quiet gates pass. The 32-file image verifies before and after. Workers
and profilers join, the guest fixtures are removed and the persistent archive
verifies under the machine lease.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Verification and procedures](procedures.tar.xz).
