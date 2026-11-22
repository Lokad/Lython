# B1319 Native number slots in retained tuples

Extend the existing unpublished integer slots to floating-point tuple
items. Numeric indexes borrow the funded tuple slot. Python object
observers still publish one stable box. A mixed tuple preserves
existing aliases and chooses the wider backing when fresh numbers
make up at least half of its inputs. The backing and eventual boxes
are admitted before construction.

Compared `8c81fb8d2` with `8bb572375` on November 22, 2026 UTC on
the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3.
Two independently warmed processes per revision supplied seven
rotated batches each. Ranges cover all process-median pairings.
Elapsed includes mediated RAM-backed I/O and prior-result disposal.
Process CPU includes worker request handling. Negative changes
favor the candidate.

| Whole warm script | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Typed ASCII CSV meter report | +0.92 to +2.87% | +0.65 to +3.29% | 1.77 / 0.15% |
| CSV meter report with explicit float | +0.47 to +4.22% | +0.69 to +1.85% | 1.08 / 2.62% |
| Plain integer invoice control | −4.84 to −0.57% | −5.27 to −0.95% | 4.15 / 0.33% |

Keep the candidate private. Both targeted meter reports use more
process CPU and take longer on elapsed across all pairings. The typed
report removes some boxes and allocates less, while both meter reports
carry higher accounted peaks. The plain invoice is faster with a
4.15% reference spread. Its result does not reverse the consistent
adverse meter results. The original local summary expected four timed
cases when there were three. The raw collection was already complete;
only the reducer assertion needed correction.

The typed ASCII meter allocates 2.83% less locally and its accounted
peak rises by 229,376 bytes, from 4,142,729 to 4,372,105. The explicit
float meter has effectively flat allocation and increases its peak
by 458,752 bytes. The local invoice and formatted report have
effectively flat allocation and unchanged peaks. All four budgets
return to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 109 white-box and
425 public focused Release checks. A retained mixed tuple preserves
negative zero, alias identity, deepcopy behavior and observation
after actual asynchronous input. Both full worker images pass the
timed CPython goldens. All 84 timed batches match. Workers join;
the persistent archive validates under the lease after fixture
cleanup in 169 seconds.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-22-sparse-lexical-preparation/candidate-core.dll.gz).
