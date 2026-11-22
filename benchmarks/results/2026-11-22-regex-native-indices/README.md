# B1381 Named capture numbers in existing dictionary storage

This compares frozen Lython `a3338b0a8` and `a4cb961be` on November 22,
2026 UTC. The change is retained at `aef642701` on top of the separately
qualified regex key protocol. Both source patches are archived. They
edit the same lines, with different hunk locations and blob identities
in the file changed by the protocol correction. Each frozen image has
32 verified files. Only the core DLL differs. Both use the same optional
PythonRe 0.4.0 adapter.

The ordered dictionary stores each capture number in an existing
integer slot. Named capture lookup, `groupdict()` and `lastgroup` read
those numbers directly. `lastgroup` searches the existing ordered
metadata and returns the original key. Ordinary `groupindex` reads
materialize and reuse the usual Python integer. One dictionary owns
both the native numbers and the Python view. Eager boxing disappears
with no extra table or ownership change.

| Complete warm script | Elapsed change | Wider process CPU change | Local allocation per run |
| --- | ---: | ---: | ---: |
| Compile 64 named rules | −1.85 to −0.05% | −0.94 to +1.54% | 414,800 → 410,704 B |
| Compile 64 named rules and read all 192 `groupindex` values | +0.51 to +2.73% | +0.70 to +3.16% | 428,352 → 428,352 B |
| Named captures over 3,072 Unicode rows | −3.19 to −1.81% | −2.16 to −1.72% | 3,437,280 → 3,437,184 B |
| Named captures over 3,072 ASCII rows | −1.85 to −0.55% | −1.52 to −1.43% | 3,128,584 → 3,128,488 B |
| Compile 64 unnamed rules, unchanged control | −2.24 to +0.72% | −1.78 to +1.24% | 382,424 → 382,424 B |

The ranges use every pairing of two worker medians per source with
negative values favoring the candidate. For each case, the quiet Azure
F4as_v7 ran four persistent workers on Ubuntu 24.04.4 and .NET 11 RC3,
using default tiering, 8 seconds of full-request warmup per worker and
seven timed batches per worker. All 140 batches verified. The largest
same-source elapsed spread was 1.61%. Timing includes the guest script,
including any guest regex compilation, output verification and disposal
of the previous result. Wider CPU includes worker reset and request
handling. Host compilation, process startup and final teardown are
outside the elapsed clocks. The scripts generate their inputs and use
no guest filesystem I/O.

The named-rule diagnostic allocates 4,096 fewer bytes locally. Admitted
peaks match on all five scripts and the allowance returns to baseline
after each. Retain the change for the eliminated eager boxes and lower
CPU in both matching reports. The deliberately observation-heavy case
regresses with stable workers. Ordinary matching benefits are modest
and these results do not qualify backend performance. The earlier
parallel-table experiment remains private. [B1342](../2026-11-22-ordered-regex-group-metadata/README.md).

Both frozen images matched CPython 3.13.2 goldens. No CPython times
were collected. Windows .NET 11 and Ubuntu .NET 10 each passed 70
white-box and 433 public focused Release checks, including alias
identity, providers, cancellation, refusal and budget reuse. Broader
delivery testing belongs to the next combined gate.

[Raw observations](observations.tgz) · [Summary](summary.json) ·
[Allocation measurements](allocation-summary.json) · [Images](source.json) ·
[Procedures and focused test output](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
