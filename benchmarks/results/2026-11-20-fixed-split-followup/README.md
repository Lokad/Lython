# Fixed split range follow-up

Private `7a0008de3` replaces a fixed unpack's source replay with a bounded
managed stack buffer of field ranges. The `a5214a158` reference image and
candidate matched five CPython workload goldens. Allocation was effectively
flat, accounted peaks matched, and the allowance returned to baseline.
Windows .NET 11 and Ubuntu .NET 10 each passed 122 focused checks.

| Windows .NET 11 warm diagnostic | Reference | Candidate | IQR / median, reference and candidate |
| --- | ---: | ---: | ---: |
| Expanded forwarding | 23.09 ms | 22.93 ms | 62.6%, 75.0% |
| Inline forwarding | 19.92 ms | 20.80 ms | 8.6%, 27.8% |
| Positional invoice | 17.02 ms | 17.52 ms | 33.0%, 14.8% |
| In-memory fixed unpack, 2,048 records | 2.93 ms | 3.31 ms | 17.4%, 21.2% |

The short local timings show substantial variation. The in-memory case
removes filesystem reset from the diagnostic and still varies. The earlier
[B1083 quiet comparison](../2026-11-19-fixed-split-ranges/README.md) tested
the same sizing tradeoff and measured 1.68–2.11% higher warm forwarding CPU.
Both prototypes give up the single-byte bulk count for a full per-field
sizing pass. The change from operand slots to stack scratch still writes
and reads a coordinate for each field. Keep the new prototype private and
advance to report rendering. A new split design needs to preserve bulk
sizing or remove another complete stage before another quiet comparison.

[Local medians and image hashes](summary.json) ·
[Raw diagnostics, source and checks](diagnostics.tar.xz).
