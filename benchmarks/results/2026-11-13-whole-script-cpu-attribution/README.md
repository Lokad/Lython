# Complete-script CPU attribution, 2026-11-13

B686 profiles the qualified cb8736ff runtime (frozen core SHA256
`5a184a1d40a111ef2217f58789a66443fcc42540c850b79b562dcdb800ff740c`),
with the fixed fec79 benchmark worker and optional 0.4 adapter. Source and machine
are the same as the [representative comparison](../2026-11-13-fixed-definitions-cpython-checkpoint/README.md).
This is CPU attribution, not a before/after comparison or an allocation profile.

| Complete script | Guest CPU samples | Unmapped leaves | Leading resolved leaves |
| --- | ---: | ---: | --- |
| Captured text summary | 571 / 2.88 CPU seconds | 41 (7.18%) | Dictionary loop 107 (18.74%); cased-word preparation 67 (11.73%); ordinary instruction dispatch 48 (8.41%); native runner 42 (7.36%) |
| File search with helper/default arguments | 454 / 2.30 CPU seconds | 88 (19.38%) | Ordinary instruction dispatch 66 (14.54%); native-call start 36 (7.93%); input instructions and native runner 24 each (5.29%) |

Each script received eight guest seconds of warmup and one three-second cpu-clock
capture at 199 Hz with frame-pointer stacks. `DOTNET_PerfMapEnabled=3` is an explicit
diagnostic override; these captures must not be substituted for normal timings.
Compilation is excluded. Worker execution includes mediated file I/O, complete
stdout/file checking and prior-result cleanup; reset/protocol work is outside the
guest window. Analysis selects the actual guest worker TID and resolves JIT ranges
offline. No ambiguous leaves were assigned. Owning frames include useful/inlined
work and are not estimates of removable overhead. Kernel/CLR unknowns remain visible.

The owned run completed under its unchanged 120-second bound. Quietness, image and
golden checks passed; all users joined and the raw SHA256 archive was persisted and
verified before RAM storage was unmounted. Full maps, stacks, records and machine
observations are in observations.tgz (SHA256
`575be067bba40f8ee71908b8d891ee1f298f2863eceaa297263cc80f1d4ff68a`).

Source follow-up finds that dictionary accumulation already shares one native
probe, stores ordinary small counters without intermediate boxes, and preserves
publication at callbacks/unwind. Word preparation already uses a common UTF-8
scanner and eager admission. This profile does not justify a second dictionary
representation or establish an allocation win from another call arena. Search
still identifies call/dispatch as a meaningful redesign boundary, but stack-slot
capacity, exact suspension transfer and useful callee work must be accounted for.
Allocation attribution and the search's unknown CPU fraction remain unresolved.
The next bounded storage change is native JSON byte input using the existing
parser and ownership; it is motivated by the complete ZIP/config script's forced
decode boundary, not by these CPU samples.

`retired-build-image.tar.xz` preserves the exact 2026-10-10 function-frame baseline
core (SHA256 `2b68163c1401df96efcf511f9706fea3fe64f75811360789b517333f9045d53f`)
before retiring duplicate historical VM build outputs for working space. This
binary is unrelated to the profiled source and is not new performance evidence.
