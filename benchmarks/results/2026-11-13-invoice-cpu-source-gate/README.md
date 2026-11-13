# Invoice helper-call source gate

This is a CPU diagnostic of qualified runtime **fc2531b8**, core
`6b468bf11b163e45464f5dac7bd40cdf7694a6d5b627ef661aee17561a91cdb8`,
with the complete invoice workload committed at **b649cbd8**. It is neither an
elapsed comparison nor a CPython ratio. The runtime remains unchanged by this gate.

The existing quiet Azure VM and fixed `fec79afd` worker executed the 4,096-record,
127,031-byte CSV invoice script. After 12 seconds of execution warmup, an 8-second
199 Hz `cpu-clock`/frame-pointer capture selected the actual Lython execution
thread: **1,608 samples over 8.09 guest CPU seconds**. There are 138 unresolved
leaf samples (8.58%), no ambiguous symbol ranges and no malformed map records.
Wider worker/reset/validation CPU and every thread remain in the raw evidence.

The largest resolved leaves include ordinary instruction dispatch (180), native
slot dispatch (128), ordinary input dispatch (66), result publication (45), CSV
record completion (42), binary operations (42), tuple copying (40) and numeric
classification (39). Native call selection has 36 samples; native call entry and
start have 16 each, and workstate construction has 15. Owning/inlined frames include
useful operations: these counts are **not removable shares or predicted speedups**.

Source inspection supplies the concrete opportunity: the pricing and rounding
helpers are native-eligible at 12 and 6 slots, but their two composed user calls
force pricing-caller materialization. R303 may remove that eager frame/workspace
transport using the existing native binder, final slots/PC and prepared linear
runner. Real child suspension must transfer exactly once; failed parent admission
must unwind every prepared descendant. The prototype still needs semantic,
cancellation, ownership and controlled complete-script comparisons before adoption.

Both independent file goldens, loaded-image identity, workstation GC, quiet-machine
checks, worker `Closed`/exit0, process joins and fixture cleanup pass. Every RAM
trace byte was archived and SHA-verified persistently under the VM lease before
unmount. Verified superseded binary aliases were retired only after checking their
canonical Git archive; the current image was installed and verified before retiring
the selected old reference. The 240 MiB OS plus 12 MiB evidence floor was preserved.

[Validation and attribution](validation.json) and
[raw observations, maps and collection scripts](source-checks.tar.xz) preserve the
complete evidence. An initial local collector-generation indentation error and an
early local retrieval guard failure occurred before their respective VM actions;
both conditions are recorded. No unchanged qualification was repeated.
