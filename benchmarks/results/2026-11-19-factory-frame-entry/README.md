# B1077 frame-first factory entry

Private `62f1013b9` tests whether a function whose first executable
instruction requires an invocation context can skip its unusable inline
entry. The ordinary and annotated rule factories both begin with prepared
default storage. The exact candidate selects an owning frame immediately
for those entries and keeps their emitted instructions. The reference is
`773be8dc2`. This isolates the entry change from the separate R646 dispatcher.

Windows .NET 11 and WSL .NET 10 each passed 643 focused Release cases.
The complete 32-file images both matched five CPython goldens. Local
allocation and accounted peaks are effectively equal.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover four combinations of duplicate process medians. Negative
values favor the candidate.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Warm | −1.08…+2.21% | −1.11…+2.00% |
| Ordinary rules | Compile-to-result | +0.71…+2.25% | −0.49…+2.37% |
| Annotated rules | Warm | −1.75…−0.87% | −2.34…+0.40% |
| Large positional calls | Warm | −1.77…+1.04% | −2.77…+1.24% |
| Captured rules | Warm | −1.15…+3.52% | +0.73…+4.63% |
| Expanded forwarding | Compile-to-result | −1.60…−0.41% | −2.71…−1.29% |

The prototype stays private. Ordinary compile-to-result elapsed rises in
all four comparisons. Annotated warm elapsed falls with mixed CPU.
The captured factory itself already has a frame, yet its workload has
4.50% candidate elapsed spread and higher CPU. The entry fact applies
throughout the program, so this observation cannot isolate machine
variation from changed entry selection elsewhere. The favorable
forwarding control also limits attribution. Removing the immediate
handoff leaves the factory frame and its per-call work. A redesign that
can remove those costs needs separate proof.

Both quiet gates and all 120 timed goldens passed in 273.91 seconds. Workers
joined, fixtures were removed and the archive verified under the lease.
Warm includes mediated I/O, goldens and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process CPU
observes a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Prototype, source probe and checks](checks-and-procedures.tar.xz).
