# B1021 final split operands

Measured `934fe4a44` / core `1f747b54` against `b2a0cbd48` /
core `589d6b7b` on the quiet Ubuntu Azure F4as_v7 with .NET 11 RC3
on 2026-11-19 UTC. Production `6ce82ba48` has the same runtime source
and adds a focused refusal check. The four jobs used two processes per
source and five interleaved rounds. All 80 responses and both quiet gates
passed in 186.18 seconds. Workers joined and the archive was verified
before fixture cleanup.

| Workload and phase | Prototype elapsed change | Prototype process CPU change |
| --- | ---: | ---: |
| Expanded forwarding, warm | −3.02% to −1.72% | −4.14% to −1.79% |
| Expanded forwarding, compile-to-result | −3.58% to −1.52% | −3.92% to −1.34% |
| Text normalization control, warm | −2.70% to −0.22% | −1.54% to −0.88% |
| Positional-call control, warm | −1.15% to +0.17% | −0.82% to +1.17% |

For an exact builtin split followed by matching fixed unpacking, the
shared scanner writes admitted fragments to the current result operands.
This removes a private list, its backing array and the unpack pass.
Escaping lists, count mismatches and guest index callbacks keep the
ordinary Python paths. If fragment construction fails, speculative slots
are cleared before the associated reservation is refunded.

On the local allocation diagnostic the forwarding script uses 4.26% less
allocation and an 11.34% lower accounted peak. Both allowances return to
baseline after cleanup. The text control keeps its accounted peak and has
2.22% reference elapsed spread. The positional control spans zero. The
first staging preflight stopped below available persistent-disk headroom.
The rerun staged only after verifying additional older archive replicas
against their committed originals.

Windows .NET 11 and WSL .NET 10 passed 215 focused tests per platform,
with 16 additional source probes agreeing through both APIs. Two later
refusal cases per platform check that an inadequate shared allowance
fails at the split, before any target callback or guest exception
handler, and returns all reservations. The initial refusal assertion
used the unnormalized source offset. The test supplies LF source before
compilation and the rerun passes.

Warm includes mediated I/O, verification and previous-result cleanup.
Compile-to-result also includes compilation and disposal of its owner.
Startup, fixture reset, framing and final cache teardown are outside the
guest elapsed measurements. Process CPU has a wider boundary.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)

[Full platform, stress and installed-package qualification](delivery/README.md).
