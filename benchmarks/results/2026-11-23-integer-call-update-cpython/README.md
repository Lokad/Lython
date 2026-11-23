# B1523 supplied arithmetic against CPython after direct call updates

Retained `1b9841606` and stock CPython 3.13.16 run the same 178-byte
script with 2,048 fully supplied calls. The test ran November 23, 2026 UTC
on a quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.

| Phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm | 0.31932–0.32146 ms | 0.20994–0.21320 ms | 1.498–1.531× |
| Compile to result | 0.31366–0.31437 ms | 0.22701–0.22985 ms | 1.365–1.385× |

Ratios divide Lython elapsed by CPython elapsed across four process
median pairings. Warm worker spread is 0.67% and 1.55%. Time-to-result
spread is 0.23% and 1.25%. Independent process clocks represent the
two phase boundaries. Compilation-only and startup keep their existing
measurements. [B1522](../2026-11-23-integer-call-update/README.md) holds
the matched internal attribution.

Warm execution reuses code and includes result verification and prior
result disposal. Time to result includes compilation and code disposal.
The workload has no file I/O. Startup and request reset/transport are
outside elapsed. Process CPU includes wider request work. Two workers
per engine warm for eight seconds each and contribute seven rotated
batches. All 56 batches verify. Both quiet gates and all image hashes
verify, the workers join, fixtures are removed and the evidence
is archived and verified under the lease.

Three independent probes also match CPython through Run and RunAsync.
Later arguments may return the original object while rebinding its
global, a numeric protocol may rebind the accumulator, and an unbound
earlier argument fails before later effects. These are acceptance cases
for removing the caller boxes identified in B1522.

[Identity](identity.json) · [Summary](summary.json) ·
[Caller probes](caller-probes.tar.xz) · [Observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
