# B1847 Insert ready collection values inline

Ordinary elements and dictionary pairs already arrive evaluated. The
candidate inserts them from the shared prepared-operation dispatcher in
both APIs. Starred iterable and mapping expansion retain the original
suspending path and share the same mutation helpers. This removes an
activation per ordinary insert and preserves operand retirement, error
order, cancellation checks and admitted growth.

B1845 attributed 50 of 1,436 wide async shipment samples to the nearest
generic prepared-operation frame. That frame also serves other operations.
Each wide and bound report performs 4,096 list-comprehension inserts.
[B1737](../2026-11-25-forwarded-comprehension-binding/README.md) tested a
list append change alongside scope forwarding. The delivered isolation
kept the existing insertion path. This candidate also covers set and
dictionary comprehensions without changing their scopes.

Windows .NET 11 and .NET 10 pass 213 public and 75 white-box focused
Release checks each, including protocols that suspend before insertion
or during expansion, cancellation, refusal and retained results. Peak
shared admission matches across four scripts in both APIs and every run
returns its allowance to baseline. Median CLR allocation differs by at
most 300 B per complete invocation.

The two frozen images differ only in the core DLL from `4919f6748`.
Each script reads 2,048 records from the same 27,350-byte fixture. The
shipment index has one dictionary and one set comprehension. The file-fed
map has neither display insertion nor a comprehension. Both images match
four independent CPython goldens on Windows and on the VM.

## Quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 27,
2026 UTC. Two workers per source warm for at least five seconds each and
supply seven rotated batches. Ranges span all pairings of worker medians.
Warm elapsed includes mediated RAM-backed I/O, verification and prior-result
disposal. Process CPU also includes fixture reset and request framing.
Both clocks exclude startup and compilation.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Wide shipment | Async | −3.26% to +2.80% | −3.00% to +3.57% |
| File-fed map control | Async | −4.87% to +5.89% | −4.41% to +3.67% |
| Dictionary and set shipment index | Sync | −7.18% to −0.77% | −5.85% to −0.63% |
| Bound shipment | Async | −0.63% to +1.19% | +0.40% to +1.22% |
| Wide shipment | Sync | −5.70% to −3.65% | −4.98% to −1.89% |

Retain the common insertion path. Both targeted sync workloads improve in
every pairing. Wide async timing remains inconclusive. Bound async CPU
rises at closely grouped worker medians and remains open. The unchanged
file-fed control varies, with 7.35% CPU spread between candidate workers.
The comparison supports the sync improvements on these scripts. It leaves
the wide async gap and the cause of the bound CPU increase unresolved.

All 140 batches and both quiet gates pass. Staging reuses all 32 reference
files and 31 candidate files from B1846 and streams the changed core and its
inputs. The workers join, both images verify, the RAM-backed fixtures are
removed and the persistent archive verifies under the lease.

[Timing summary](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Patch and checks](procedures.tar.xz).
