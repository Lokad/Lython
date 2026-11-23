# B1495 Commit the physical CSV line before converting deferred numbers

Private `fbc042720` extends [B1494](../2026-11-23-csv-combined-scan/README.md).
The single literal scanner retains each non-short numeric byte range in
its existing field descriptor and continues finding the physical line
end. It resolves pending numbers in source order after line advance and
before later grammar errors or row admission. The parser discards partial
descriptors before a refill. Descriptor size and owners stay the same.

The quiet Azure F4as_v7 compares the verified 32-file images on Ubuntu
24.04.4 and .NET 11 RC3, November 23, 2026 UTC. The reference image has
the same production sources as `138e91373`. Two workers per image warm
for eight seconds and supply seven rotated batches. Ranges pair their
medians. Warm elapsed includes mediated RAM-file I/O, output verification
and prior-result disposal. Compile to result adds compilation and compiled
owner disposal. Process CPU also includes reset and protocol. Startup and
final teardown are outside elapsed.

| Workload | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Native typed ASCII-number CSV | Warm | −3.22 to +0.99% | −5.08 to +3.21% | 1.72 / 2.57% |
| Native typed Unicode-number CSV | Warm | +2.74 to +6.97% | +1.90 to +6.93% | 0.27 / 3.83% |
| CSV with explicit float conversion | Warm | −2.59 to −1.13% | −3.39 to −1.94% | 0.60 / 0.89% |
| Quote-heavy invoice | Warm | +0.28 to +3.65% | +0.31 to +1.05% | 1.03 / 2.30% |
| Short nonlocal word report | Warm | −8.13 to −0.02% | −4.91 to +0.35% | 7.96 / 0.81% |
| Native typed ASCII-number CSV | Compile to result | −2.98 to −0.31% | −3.86 to −0.71% | 2.18 / 0.56% |

Keep the variant private. Explicit-float warm and ASCII first-use improve
with supporting CPU. Unicode recovers some of B1494's loss and still
takes longer than the production reference. ASCII warm spans zero. The
short control has a wide reference-worker spread and mixed CPU. These
results leave the cause of the remaining Unicode cost unresolved.

Local allocations match for both typed workloads. The other differences
are at most 672 bytes per invocation. All five workloads have identical
admitted peaks and return their budgets after cleanup. Windows .NET 11
and Ubuntu .NET 10 each pass 203 white-box and 447 public checks. Both
frozen images match five independent workload goldens and 510 native
file and dialect combinations against CPython, including the order of
two numeric errors before an unclosed quote. Production retains the four
independent line-order and refill regressions in `053bf92c8`. All 168
timed batches, worker joins, fixture removal and archive verification
pass under the quiet lease.

[Timings](summary.json) · [Identity](identity.json) · [Allocation](allocation-summary-six.json) ·
[JIT](jit-summary.json) · [Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Tests and procedures](procedures.tar.xz) · [Exact reference core](../2026-11-23-forwarded-global-call/README.md).
