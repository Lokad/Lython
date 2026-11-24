# B1547–B1548 Borrow short CSV dictionary keys

The exact `dict.get` and existing-key assignment consume the deferred
UTF-8 key that CSV already provides. Both use the existing native text
probe. A new key, guest callback or ordinary observing read materializes
the Python string. The dictionary retains the first inserted key. The
prototype `4e0ef9eb9` is measured against `3e5826f7d`. The source is
integrated at `d23dd5c72` alongside the independent named regex work.

The repeated-key meter reads 4,096 rows with three keys. Its distinct-key
counterpart reads the same numeric input with 4,096 five-byte keys and
writes the larger report. Both frozen images matched all five stdout
and file goldens against stock CPython 3.13.16. Assembly hashes confirm
the same workers, adapters and runtime settings. The marked instructions
appear on the two meters and the distinct-key variant. The typed Unicode
meter uses ordinary keys and is an allocation control.

On Windows, the median local allocation on the explicit meter falls
from 1,221,390 to 992,088 bytes per full invocation, an 18.77% reduction.
Distinct keys, typed Unicode and both unchanged controls are effectively
flat. Admitted peaks match for all five scripts and every invocation
restores its shared-budget baseline. Windows and Ubuntu each pass 1,247
public and 597 white-box focused Release checks. The new file-backed test
checks canonical identity with repeated Unicode keys in both sync and
suspending hosts and checks file disposal and allowance reuse.

## Warm observations

November 24, 2026 UTC, Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Each worker warms for twelve seconds and contributes seven rotated
batches. Ranges span all pairings of process medians. Warm elapsed
includes mediated RAM I/O, stdout and file verification, and disposal
of the previous result. Process CPU includes fixture reset and
protocol. Compilation, process launch and final teardown are outside
these clocks. Negative changes favor the candidate.

| Run and workload | Warm elapsed change | Process CPU change |
| --- | ---: | ---: |
| B1547 explicit meter | −12.42% to −9.58% | −10.37% to −7.88% |
| B1547 distinct-key meter | −2.25% to +2.30% | −2.12% to +0.08% |
| B1547 quoted invoice | +0.80% to +2.19% | +1.33% to +3.38% |
| B1547 short Unicode report | −1.63% to +12.37% | −1.92% to +13.57% |
| B1548 explicit meter | −12.81% to −9.07% | −12.58% to −8.92% |
| B1548 quoted invoice | +0.54% to +2.14% | −1.96% to +0.76% |
| B1548 short Unicode report | −5.91% to +0.05% | −6.78% to 0.00% |

B1547 completed all 112 timed batches and joined the workers. Its final
idle check observed 15% CPU during a half-second window and failed.
The candidate short-report workers differed by 10.70%. Its observations
remain diagnostic. B1548 used the same binaries and goldens, reordered
the three repeated lanes, and added longer settling outside the clocks.
It passed both quiet checks, all 84 timed batches, joined cleanup and
read-back of the archive under lease. The explicit meter improves in
both. The unchanged text report's adverse result does not recur. The
quote-only elapsed penalty recurs at smaller magnitude with mixed CPU.
Retain the allocation and meter improvement while carrying that timing
debt into the combined CPython checkpoint. Distinct-key elapsed remains
inconclusive.

[Allocation and admission](allocation-summary.json) ·
[Prepared instructions](prepared-sites.json) · [Image hashes](identity.json) ·
[Initial summary](initial-summary.json) · [Initial terminal status](initial-terminal.json) ·
[Initial raw observations](initial-observations.tar.xz) ·
[Confirmation summary](confirmation-summary.json) ·
[Confirmation terminal status](confirmation-terminal.json) ·
[Confirmation raw observations](confirmation-observations.tar.xz) ·
[Procedures and exact source patch](procedures.tar.xz).
