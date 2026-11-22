# B1311 hold one positional default in the callable

The compiled definition pre-admits the final tuple before evaluating
defaults. The existing private operand owns that reservation through
evaluation and suspension. An ordinary call reads the value directly
from the completed callable. The first `__defaults__` read publishes a
tuple with the same value identity and the same funding. Replacing
metadata keeps prior aliases charged. Wider defaults, generated
layouts and ordinary tuple storage retain their existing paths.

| Local Windows Release workload | CLR allocation per execution, reference → candidate | Change |
| --- | ---: | ---: |
| Eager factories | 1,144,272 → 1,029,584 B | −10.02% |
| Postponed factories | 1,029,744 → 915,056 B | −11.14% |
| Factories observing default tuples | 1,373,960 → 1,423,112 B | +3.58% |
| Ordinary fully supplied helper | 142,808 → 142,808 B | 0.00% |
| CSV free-function invoice | 3,672,528 → 3,672,402 B | −0.003% |

All five accounted peaks match and both execution owners refund.
The default observer reads tuple length. An added identity assertion
reads the actual item, increasing reference allocation by 65,632
bytes and prototype allocation by 96 bytes. Forcing the shared
integer box on both sides changes the observer comparison to
1,439,592 → 1,423,208 B, a 1.14% reduction. Both images match the
CPython golden and keep the same accounted peak. This local
diagnostic isolates the cost of eager boxing from tuple publication.

Both exact images match the five CPython goldens. Windows .NET 11
and Ubuntu .NET 10 each pass 77 white-box and 444 public focused
Release checks, including refusal, cleanup and retained aliases.
Windows Debug passes 48 default and ownership checks. CPython 3.13.2
agrees with the new tuple-valued and wide-integer identity case.

## Quiet comparison

Compared `4337b7765` with `6c72832e1` on November 22, 2026 UTC
on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two independent
processes per revision warm for 12 seconds before seven rotated
batches each. Changes span all pairings of per-process medians.
The warm clock includes captured output and prior-result disposal.
Time to result adds compilation and code-owner disposal. Process
CPU covers the wider worker request. Startup and final teardown
have separate boundaries. Negative changes favor the prototype.

| Workload and boundary | Elapsed change | Process CPU change | Ref / prototype elapsed spread | Max IQR |
| --- | ---: | ---: | ---: | ---: |
| Eager factories, warm | −3.26 to +1.70% | −4.73 to +1.67% | 2.50 / 2.57% | 0.78% |
| Postponed factories, warm | −6.51 to +1.11% | −7.18 to +1.04% | 4.39 / 3.60% | 1.04% |
| Defaults tuple observer, warm | +2.60 to +6.90% | +3.24 to +7.60% | 4.17 / 0.02% | 1.17% |
| Fully supplied helper, warm | −1.90 to +2.54% | −1.65 to +2.38% | 1.58 / 2.90% | 1.75% |
| Postponed factories, time to result | −2.70 to +0.44% | −3.93 to +0.40% | 2.02 / 1.19% | 0.79% |

Keep this representation private. Both nested factories allocate
substantially less, and three of four pairings for each favor the
prototype on warm CPU. The quiet ranges and time to result span zero.
The tuple-length observer uses more CPU across every pairing. The
fully supplied helper remains mixed. The extra box is a concrete
representation cost. A further design needs a stable shared numeric
identity with deferred boxing while avoiding additional storage in
every callable or another argument packet.

All 140 timed batches and both quiet gates pass. The service checks
output, joins its workers and verifies its persistent archive before
the lease ends. The full run takes 353 seconds. The raw observations
retain both directions and their original conditions.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Sources and checks](checks-and-procedures.tar.xz) ·
[Retained factory attribution](../2026-11-22-retained-factory-attribution/README.md).
