# B1726b Shared field scan with a carried debug marker

This refinement carries the marker's rendered position through the existing
cancellable debug-text pass. It removes the reverse whitespace scan and keeps
comment removal and the original debug whitespace. Ordinary fields still avoid
the whole-field copy, separate delimiter scan and debug builder.

The reference, catalog, VM, timing boundaries and two-worker protocol match
the [initial comparison](../README.md). The two frozen images again verify
all complete output and file goldens. The report still allocates 57,720 B per
compilation against 58,400 B in the reference. The manifest allocation and
retained admission match. The inspected 241 instructions, source spans and
storage metadata match across all three scripts.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| 16-record formatted report | Compile | −2.23% to −1.21% | −2.25% to −1.03% | 0.98% / 0.07% |
| 16-entry file manifest | Compile | −2.10% to +0.65% | −2.37% to −0.80% | 1.71% / 1.09% |
| 16-record formatted report | Compile to result | −0.88% to −0.62% | −1.35% to −0.61% | 0.19% / 0.07% |
| 1,024-record formatted report | Warm sync | −0.81% to −0.03% | −0.58% to +0.00% | 0.07% / 0.73% |

Retain the shared scan for the removed work, lower compiler allocation and
consistent direction through result. The magnitude varies by block and the
unchanged compile control also uses less CPU in confirmation. The initial
warm spread remains visible. This result also leaves the earlier B1725 warm
observation open because the reference already contains that change.

Windows .NET 11 and Ubuntu .NET 10 each pass 351 public and 25 white-box
focused Release checks. The added public case checks that top-level `!=`
stays part of the expression and delimiter-like bytes in a quoted string
do not start a conversion. Its output agrees with CPython 3.13.2. All 112
timed batches pass both quiet gates. All workers join, guest fixtures are
removed and the persistent archive verifies under the lease.

[Frozen identity](identity.json) · [Timing](quiet-summary.json) ·
[Compiler allocation](compile-allocation-summary.json) ·
[Instruction comparison](tape-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Source and checks](procedures.tar.xz).
