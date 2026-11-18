# Deferred duplicate index for exact keyword dictionaries

An exact dictionary already guarantees that its string keys are unique.
When it supplies the first keywords in a call, expansion keeps those names
in the existing argument storage. Another keyword source creates the
duplicate index only if it needs it. That index is admitted before allocation.
For a custom mapping, duplicate detection still precedes item lookup.
The original key objects still reach the callee's `**kwargs`.

B996 compared frozen reference `b62bfd52` (`98da93f7d`) with the candidate
on the quiet Azure F4as_v7 VM on 2026-11-18 UTC. Both 32-file .NET 11 RC3 images
passed stdout and output-file verification. Warm elapsed includes mediated
RAM-backed I/O, golden checks and prior-result cleanup. It excludes
compilation, process startup, fixture reset and final cache teardown.
Process CPU covers the wider worker window.

| Warm workload | Reference medians | Candidate medians | Elapsed change | Process CPU change |
| --- | ---: | ---: | ---: | ---: |
| Expanded forwarding report, 1,024 records | 3.158–3.159 ms | 2.997–3.041 ms | 3.71–5.12% lower | 4.32–6.71% lower |
| Positional control | 2.121–2.173 ms | 2.113–2.129 ms | −2.76% to +0.36% | −2.33% to +1.54% |

The report's reference spread was 0.03%, candidate spread was 1.46% and
largest elapsed IQR was 1.35%. The positional reference spread was 2.46%,
with elapsed IQR up to 4.68%. The local allocation screen shows a 6.84%
report reduction, from approximately 5.392 MB to 5.023 MB per execution.
Accounted peak stayed at 5,896,862 bytes and shared allowance returned to
baseline. Positional allocation and peak were unchanged. This pair
measured warm execution. The separate CPython checkpoint includes
compilation and compile-to-result. [CPython checkpoint](../2026-11-18-retained-cpython-checkpoint/README.md)

The expanded-call checks cover duplicate timing, non-ASCII key identity,
terminal refusal before custom item access and allowance reuse after
cleanup through both execution APIs. The initial Ubuntu run with an
out-of-checkout artifacts path failed five unrelated subprocess fixture
lookups. Re-running from the expected checkout layout passed; the
additional budget test was also checked in isolation on both targets.

The first two storage preflights fell 9,344 bytes short of the staging
requirement. They started no workers. The corrected preflight verified
additional copies against the pushed archives and retired only exact
replicas. The 96.4-second run passed both quiet gates and joined every
worker. The local raw archive matches the archive made under the VM lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, focused checks and procedures](checks-and-procedures.tar.xz)
