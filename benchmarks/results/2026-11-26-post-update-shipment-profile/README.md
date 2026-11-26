# B1765 Runtime attribution after complete dictionary updates

Two warm `RunAsync` profiles use the exact `7f7c4ed59` image from the
[B1764 CPython checkpoint](../2026-11-26-converted-update-cpython/README.md).
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 26,
2026 UTC. Workers warm for at least eight seconds and sample `cpu-clock`
at 199 Hz for about six seconds. Samples cover the wider request, including
mediated RAM-backed I/O, output and file checks, fixture reset and cleanup.

| Observation | Shipment callbacks | Unicode text report |
| --- | ---: | ---: |
| Verified invocations during capture | 3,072 | 6,144 |
| All-thread samples | 1,353 | 1,246 |
| Samples with resolved runtime ancestry | 1,301 | 1,122 |
| Those with unresolved leaves | 80 | 98 |
| Nearest instruction dispatcher | 215 | 103 |
| Nearest executable arithmetic entry | 100 | 0 |
| Nearest map advancement | 80 | 0 |
| Nearest complete converted dictionary update | 67 | 0 |
| Nearest native text dictionary loop | 0 | 146 |
| CSV parsing and row construction ancestry | 158 | 0 |
| Frame and operand-binding attribution | 2 | 492 |

The arithmetic and iteration entries remain visible in shipment. The text
report has different work, including native casing and dictionary
accumulation. Counts locate routes. Inlining and unresolved leaves limit
attribution. They give no estimate of removable CPU or a speedup.

The shipment tape contains `reviewed += policy is review`. A separate
route inventory locates 2,048 machine-integer updates with a boolean
right operand in each shipment variant and API. All take the generic
arithmetic path. B1766 sends each through the existing integer path.
Its allocation effect is immaterial. Repeated whole-script comparisons
show lower async shipment CPU with mixed sync CPU evidence.
[B1766 source gate](boolean-increments/README.md).

Independent output and file goldens and the full 32-file image verify
before staging. Both quiet gates and post-capture goldens pass. Workers
and profilers join, guest fixtures are removed, and the archive verifies
under the lease. The preflight uses the verified prior two-profile
footprint of 1,420,018 uncompressed bytes to keep the capture within the
available persistent storage.

[Attribution](attribution.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
