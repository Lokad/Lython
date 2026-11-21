# B1305 inline storage for one prepared keyword default

Compiled functions with one keyword-only default keep that value
in the existing admitted definition operand and callable fields.
The final dictionary is still pre-admitted before default expressions
run. Binding reads the stored value directly. The first metadata
read builds the dictionary from the admitted balance, preserving
value and dictionary identity. Replacing unpublished metadata refunds
its storage, while observed aliases retain their original funding.
Ordinary tuple layout is unchanged.

The quiet comparison uses `d51891009` and `55826c73f` on the Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Two independently warmed processes per source contribute seven
rotated samples each. The ranges span all process-median pairings.
Warm elapsed includes guest execution, golden checks and prior-result
disposal. These scripts perform no guest file I/O. Time to result
also includes compilation and code-owner cleanup. Process CPU covers
the wider request. Startup and final teardown are separate.

| Workload | Boundary | Elapsed change | Process CPU change | Local CLR allocation |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −4.87% to −1.28% | −3.55% to −1.12% | −6.68% |
| Postponed nested factories | Warm | −5.92% to −2.35% | −5.21% to −2.68% | −7.37% |
| Observe defaults on every factory result | Warm | −3.12% to −0.19% | −2.11% to +0.98% | 0.00% |
| Fully supplied ordinary helper | Warm | −3.83% to +0.67% | −3.40% to +0.88% | 0.00% |
| Postponed nested factories | Compile to result | −2.00% to +3.67% | −2.76% to +2.62% | — |

Retain the change for the lower allocation and warm CPU on both
factory scripts. The eager reference spread is 3.43%; every pairing
still favors the candidate. The observed control's CPU spans zero,
as do the ordinary helper and time-to-result observations. The
latter has a 3.56% candidate elapsed spread. The separate local
CSV invoice control allocates six bytes more per invocation. No
invoice timing was collected. Accounted peaks match and all
allowances return to baseline.

Local allocation and focused checks use `3a6accc97` and `da45b3bf2`.
The source change is identical on the two baselines. Windows .NET 11
and Ubuntu .NET 10 pass 322 white-box and 2,185 public Release checks
each. The new test uses a dictionary as the default itself and
observes identity and replacement through both run APIs. All 32
files in each image and all four quiet and five local CPython
goldens verify. All 140 timed batches and both quiet gates passed.
Workers joined and the durable archive verified under the lease
before collection ended.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
