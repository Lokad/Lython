# B1494 Find the buffered CSV line end during literal field scanning

Private `ded838747` shares the literal descriptor scan with newline discovery
for native universal-newline readers. Before a physical line advances it
uses funded descriptor capacity and exact short decimal conversion. Wider
or otherwise non-short numbers, quote transitions and continued rows use
the existing parser. A fully described literal row skips the separate
newline search. Partial prefixes can narrow that search on fallback.

The quiet Azure F4as_v7 compares the verified 32-file images on Ubuntu
24.04.4 and .NET 11 RC3, November 23, 2026 UTC. The reference image has
the same production sources as `138e91373`. Two workers per source warm
for eight seconds and supply seven rotated batches. Ranges pair their
medians. Warm elapsed includes mediated RAM-file I/O, output verification
and prior-result disposal. Compile to result adds compilation and compiled
owner disposal. Process CPU also includes reset and protocol. Startup and
final worker teardown sit outside elapsed.

| Workload | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Native typed ASCII-number CSV | Warm | −4.18 to −3.61% | −4.23 to −2.84% | 0.19 / 0.41% |
| Native typed Unicode-number CSV | Warm | +5.39 to +12.80% | +5.67 to +12.15% | 4.24 / 2.68% |
| CSV with explicit float conversion | Warm | −0.58 to +1.21% | +0.97 to +1.96% | 0.30 / 1.50% |
| Quote-heavy invoice | Warm | −0.69 to +0.57% | −2.44 to +1.05% | 1.15 / 0.12% |
| Short nonlocal word report | Warm | −1.20 to +1.42% | −2.46 to +1.06% | 2.32 / 0.32% |
| Native typed ASCII-number CSV | Compile to result | +2.68 to +8.39% | +3.66 to +9.30% | 1.61 / 3.88% |

Keep the prototype private. The Unicode fixture uses non-ASCII whitespace,
digits and underscores, so it reaches the fallback at its first numeric
field. The prototype repeats the search for that field's marker in the
bounded line. Test deferring its conversion in the same descriptor so the
scanner can keep finding the terminator. First-use and explicit-float CPU
remain adverse evidence. Local allocation and admitted peaks match all
five workloads. Every allowance returns to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 203 white-box and 447 public
checks. The four new read-order cases also pass on retained production
and are committed as `053bf92c8`. Both frozen images verify five independent
workload goldens and 480 native file/newline/dialect combinations against
CPython. All 168 timed batches, joins, fixture retirement and archive
verification pass under the quiet lease.

[Timings](summary.json) · [Identity](identity.json) · [Allocation](allocation-summary-six.json) ·
[JIT](jit-summary.json) · [Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Tests and procedures](procedures.tar.xz) · [Exact reference core](../2026-11-23-forwarded-global-call/README.md).
