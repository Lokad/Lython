# B1243 and B1244 direct captures for stable parameters

Compares the delivered source at `0269120fc` with private candidate
`fa48b107c` on November 20, 2026 UTC. The candidate uses existing
lexical binding and descendant nonlocal facts to hold an unrebound
parameter directly in a closure. Mutable and namespace-backed names
keep cells. The shared capture representation replaces the singleton
cell collection interface. Direct numeric captures conservatively
fund a box before publication, including when an earlier read has
already boxed the value. Sibling captures can pay that estimate more
than once. Forwarded captures reuse their existing charge.

Both factory scripts make 4,096 helpers across 2,048 inputs. Each
iteration captures the policy multiplier for the intermediate helper.
The observed-annotation control builds and reads dictionaries without
that capture, and the invoice exercises mediated CSV input and output.
Warm elapsed includes guest execution, mediated RAM-backed I/O, goldens
and cleanup of the previous result. Process CPU covers the wider worker
request. Compilation, process startup and final teardown are outside
the sampled warm invocation.

| Complete workload | Local CLR allocation | Accounted execution peak | Quiet elapsed change | Quiet CPU change |
| --- | ---: | ---: | ---: | ---: |
| Postponed factories | −8.99% | 2,968,481 → 2,902,945 B | −8.78% to −0.10% | −8.02% to 0.00% |
| Eager factories | −8.25% | 2,968,425 → 2,902,889 B | −3.78% to +0.84% | −3.82% to +1.42% |
| Observed annotations | 0.00% | 2,444,364 B | −1.34% to +4.73% | −1.24% to +4.41% |
| CSV invoice | −0.002% | 9,432,613 B | −3.53% to +1.60% | −2.46% to +2.50% |

Each quiet range covers all four pairings of two worker medians on
each image. The VM was an Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11
RC3. B1243 used 12 seconds of warmup per worker. The postponed reference
workers differ by 6.03%, and the observed control has adverse pairs.
The allocation improvement is established for this prototype. The
elapsed and CPU observations leave its impact unresolved.

## Exact-image confirmation

B1244 reverses the workload and worker order for the three factory
lanes. It increases per-worker warmup from 12 to 22 seconds and uses
one-second sampling batches. The same 32 files match in each role.

| Warm workload | Elapsed change | Process CPU change | Reference spread |
| --- | ---: | ---: | ---: |
| Postponed factories | −2.77% to −0.23% | −3.11% to −0.44% | 0.41% |
| Eager factories | +3.44% to +5.57% | +4.30% to +5.89% | 0.74% |
| Observed annotations | −4.05% to +0.20% | −4.40% to 0.00% | 2.50% |

Both quiet gates and all 84 repeat batches pass. The observed-control
increase from B1243 does not recur. The eager slowdown appears in all
repeat pairings with low control spread. The private build saves
allocation in both factory modes and improves the postponed case.
Keep it private because of the measured eager regression. Its precise
CPU cause still needs attribution. Revisit if a design can remove that
cost or eliminate a larger call stage.

[Confirmation timing](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tgz) ·
[Confirmation procedures](confirmation/procedures.tar.xz).

Local compilation allocates 48–64 extra bytes across the four scripts.
Each nested script allocates 56 fewer CLR bytes and admits 32 fewer bytes
per iteration. The observed-annotation and invoice host peaks are flat.
The frozen images each pass all four CPython goldens and return the
borrowed allowance to baseline. The exact candidate passes 43 white-box
and 652 public Release checks on each of Windows .NET 11 and Ubuntu
.NET 10. Coverage includes identity across escaping siblings, mixed
stable and mutable captures, lexical rebinding, suspended host reads,
terminal refusal and cleanup. All 32 worker image files were verified.
The quiet gates pass, and the persistent archive was verified under
the lease before removal of guest fixtures.

[Allocation](allocation-summary.json) · [Quiet timing](summary.json) ·
[Raw observations](observations.tgz) · [Sources and procedures](checks-and-procedures.tar.xz).
