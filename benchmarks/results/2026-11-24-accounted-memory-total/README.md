# B1628 Accounted memory as a running total

The candidate stores total admitted bytes and pending bytes. The committed
balance is their difference. Committing now updates only pending bytes.
Admission, release and rollback change the running total. The default
backing and callable still have separate admission points.

The defaulted rule factory publishes both functions into module bindings.
An observer reads those bindings after the immediate calls and changes
one keyword default. Both revisions agree with CPython in synchronous
and asynchronous execution, including function identity, dictionary
identity and the subsequent call result. Keep the published callables
and their canonical default containers in this workload.

Windows and Ubuntu each pass 1,025 white-box and 1,180 public .NET 11
Release checks. A new regression grants an outer resize, publishes
storage during reentry, then refuses a further nested resize. Rollback
preserves the published balance and the first refusal's attribution.
The borrowed allowance returns to baseline after cleanup. The separate
allocation diagnostic restores its allowance in all three scripts.
Admitted peaks match exactly. Managed allocation matches for the rule
scripts and differs by 6 B across allocation medians of about 3.28 MB
for the quoted invoice.

## Controlled comparison

The baseline is `dd17f1eb5`, whose runtime and test sources match
`27b43cb70`. Both sealed 32-file images pass three complete CPython
goldens. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3
on November 24, 2026 UTC. Two workers per image supply seven rotated
batches per lane after at least five seconds of request warmup each.
Ranges span the four pairings of worker medians. Warm elapsed includes
applicable mediated RAM I/O, verification and prior-result cleanup.
Time to result adds compilation and compiled-owner disposal. The wider
CPU clock includes fixture reset and request framing. Startup is outside
both clocks.

| Workload and boundary | Elapsed change | Wider CPU change | Elapsed spread, baseline / candidate |
| --- | ---: | ---: | ---: |
| Defaulted rules, warm | −6.48 to +3.85% | −7.67 to +4.35% | 10.78 / 0.24% |
| Defaulted rules, time to result | −2.75 to +0.53% | −0.36 to +1.89% | 1.05 / 2.30% |
| Eager annotated rules, warm | −2.85 to +4.71% | −1.35 to +1.77% | 0.52 / 7.23% |
| Quoted CSV invoice, warm | −1.30 to −0.42% | 0.00 to +1.22% | 0.59 / 0.30% |

The rule comparisons span zero and include a moving same-source worker.
The small quoted-invoice elapsed improvement has no corresponding CPU
improvement. Retain the simpler balance invariant and carry the timing
uncertainty into the next whole-script checkpoint. All 112 batches and
both quiet gates pass. Workers joined, fixtures were removed and the
observations were verified under the VM lease.

[Image identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Procedures and patch](procedures.tar.xz) · [Terminal receipt](quiet-terminal-status.json).
