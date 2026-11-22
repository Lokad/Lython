# B1321 Bypass CSV row-list construction for fixed unpacking

An exact `csv.reader` supplies its unpublished field array when the adjacent
executable instruction is a fixed unpack of that iteration result. Reuse the
existing target check and publication. Drop and refund the private array
capacity after consumption or failure. Scalar boxes and string payloads keep
their allowance. Ordinary iteration and starred targets still receive lists.

Compared `8c81fb8d2` with private `1de1811da` on November 22, 2026 UTC
on the quiet Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3. Two independent
processes per revision supplied seven rotated batches each. Ranges cover all
pairings of process medians. Negative changes favor the candidate. Warm elapsed
includes mediated RAM-backed I/O, output verification and result cleanup.
Process CPU includes request handling. Compilation and startup are excluded.

| Complete warm workload | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Typed ASCII CSV meter report | −6.57 to +6.86% | −7.23 to +7.50% | 6.44 / 7.45% |
| CSV meter report with explicit float | +0.70 to +1.81% | 0.00 to +2.56% | 0.27 / 0.83% |
| Plain integer invoice control | −2.13 to +4.33% | −2.47 to +4.52% | 3.43 / 3.07% |

Keep the prototype private. Typed elapsed and CPU span zero with moving
process controls. Explicit-float elapsed is consistently adverse. The
candidate adds a per-row refund while retaining the array and boxes. A
follow-up should eliminate that array and refund by using already-admitted
executable slots. The unchanged invoice allocation and peak match closely.

Local managed allocation falls 6.60% in both typed reports and 4.89% in
the explicit-float report. All three accounted peaks fall by 589,824 bytes.
These are allocation and allowance measurements. All four command allowances
return to baseline. The initial VM staging attempt refused because there
was insufficient disk headroom. The corrected attempt keeps the original
floor and preserves the first failure. Only verified redundant copies of
older images and catalogs were retired. The canonical archives remain.

Windows .NET 11 and Ubuntu .NET 10 each pass 118 white-box and 405
public focused Release checks. A CPython check confirms the arity-error
message, reader position after the error, starred unpacking and retained
values after the stream closes. The async test suspends through host I/O.
Both full worker images pass four local CPython goldens and all 84 timed
batches match. Workers join. The durable archive verifies after RAM
fixture cleanup under the lease.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-22-sparse-lexical-preparation/candidate-core.dll.gz).
