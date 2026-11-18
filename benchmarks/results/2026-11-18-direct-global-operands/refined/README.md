# Direct global operands: refined retained source

R509/B934, privateabab8760 on7664a04d versus qualified0691d64f/core65fcf7d0.
Retain direct adjacent global operands and centralized native/resumable/numeric
reads. Constant/name pools expose their existing immutable arrays directly,
removing collection-interface reads and the generic immediate-probe layer.
Checked complemented indices address constants followed by names; no new table,
cache, owner, opcode, guest marker or policy. Reads stay ordered and live; values
crossing calls/effects remain staged. Cold spans preserve missing-name diagnostics.
Public API/semantics unchanged; internal metadata properties use their actual arrays.

Both actual11/10 each400internal/113public focused plus42metadata checks pass
(subsets can overlap). Seven new error-order/span/protocol/real-I/O/cancellation/
alias/reuse cases also pass unchanged qualified source. Six exact-worker goldens,
allocation/reuse pass. Actual-core IR removes2floating-gen loads(15→13instructions),
9slots unchanged. Allocation/peaks effectively flat; no storage reduction claim.

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering, fourjobs156.33s/six-second
warmups/five rotated rounds/two processes per source:

| Complete script/phase | Elapsed change | Wider CPU change | Ref/candidate spread |
| --- | ---: | ---: | ---: |
| Floating warm |−9.29..−7.30%|−10.52..−7.03%|0.18/2.01%|
| Floating compile-to-result |−2.35..−0.40%|−3.71..+1.39%|1.36/0.63%|
| Text warm |−1.62..−0.34%|−2.48..+0.60%|0.05/1.25%|
| Positional calls warm |−6.42..+1.66%|−7.77..+2.04%|5.55/2.92%|

Retain the coherent producer/pool simplification and named floating warm gain.
Fresh CPU/text/call controls remain uncertain; all pairings and original adverse
9e source remain. Do not claim universal improvement, causal regression recovery,
source-condition subtraction or a new CPython ratio. Full platform/originalstress/
installed Release qualification follows this retained boundary, not inherited from
the reference. The larger whole-script objective remains active.

Warm includes Run, mediated RAM I/O, goldens and prior-result cleanup; fresh adds
compile/compiled-owner disposal. Startup/reset/protocol/final cache teardown
excluded, processCPU wider. All32image hashes/catalog/quiet/join/archive/localSHA/
RAM teardown pass. Only exact committed old9e prototype core retires after fresh
idle path/UID/hash/link/all-alias proof; current65fc/catalogs/toolchains/unmatched
evidence preserved,254/252MiBguards unchanged. Original preparation failures are
linked in the initial source, never relabelled. No unchanged retiming or new framework.

[Decision](decision.json),[summary](summary.json),[raw](observations.tgz),
[identity](identity.json),[source](candidate-sources.tar.xz),
[patch](prototype.patch.gz),[checks/procedures](procedures-and-checks.tar.xz),
[initial adverse source](../initial/README.md).
Publication postponed.
