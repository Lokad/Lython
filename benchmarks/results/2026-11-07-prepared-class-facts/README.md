# Prepared class lexical facts — R30

2026-11-07. Retain the existing class-own-cell predicate in prepared lexical facts
and remove its repeated runtime syntax walk. Creating a class cell remains distinct
from capturing an enclosing one. Fresh function, class, closure and cell identity
stay invocation-owned. No new cache, execution route or memory mechanism.

Source: e9b55acd plus the two-file runtime patch in development-evidence.tgz.
Frozen candidate net11 75e60e66 / net10 89ee0097, reference R29 81036f25 / 32dcf141.
Complete source inventories and loaded-image hashes are preserved. Class-own-cell
analysis still exists in compilation; no dormant visitor is retained.

## Correctness and allocation

Release solution build: zero warnings/errors. Per actual .NET 11/.NET 10,
382 internal and 1,921 public focused cases pass: class/scope/super, annotations,
generators, cooperative cancellation and shared memory ownership. Four public
sync/async/actual-target probes independently match CPython 3.13.2.

The affected diagnostic constructs two classes per factory call, with 32 calls,
zero-argument super, an enclosing captured value and an explicit __class__ read.
Compilation allocation increases 1,048 B (+0.77%); warm execution removes 29,980 B
(7.75%). Admitted execution/root peaks stay unchanged. The unchanged text script's
allocation is flat. All output/file goldens, final balances and cleanup pass.

Precise process allocation uses existing complete-job diagnostics, 32 warmups,
five rotated rounds of 16 jobs, isolated before/current/identical-before images.
Desktop clocks remain diagnostic. The adverse text result (10.046 ms current vs
8.791–9.101 ms before) is preserved, not explained away or hidden.

## Bounded quiet-machine confirmation

B382: quiet Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3, ordinary GC/tiering;
unchanged b94fa98c comparison worker/host and normal optional 0.4 adapter.
Five complete jobs, two before/two current processes, 5 s warmup per role,
seven rotated 150 ms rounds. 139.51 s; every Ready identity, output/file golden,
final funding, quiet endpoint, Closed/join/fixture and persistent archive gate passes.
Observation follows the full 210 s owner + 30 s stop + 40 s margin. No live work.

| Case / boundary | Before roles | Current roles | Elapsed change, all pairings |
| --- | ---: | ---: | ---: |
| Class factory, compile | 40.796–40.913 µs | 41.200–41.327 µs | +0.70–1.30% |
| Class factory, warm | 180.892–181.133 µs | 169.702–170.345 µs | −5.83–6.31% |
| Class factory, compile-to-result | 241.999–244.094 µs | 231.090–233.167 µs | −3.65–5.33% |
| Unchanged text, compile | 53.676–54.271 µs | 53.753–54.490 µs | −0.95 to +1.52% |
| Unchanged text, warm | 2.027–2.043 ms | 2.001–2.020 ms | −0.35–2.04% |

Compile is measured separately. Warm/fresh include mediated RAM I/O, full result
validation and prior-result cleanup; fresh adds compilation/program disposal.
Startup, staging/reset and final teardown are excluded. Class warm role spreads
are below 0.4%; fresh below 0.9%. Coarse external CPU is recorded, without a causal
or broad CPU claim. This synthetic class workload is not a representative CPython
refresh. The original desktop text adverse result is not reproduced here; its cause
remains unresolved. Retain the simplification and measured class-workload benefit;
accept the small compile cost. No broad speedup or compound debt repayment claim.

B381 failed before Ready: case size32 disagreed with embedded fixture size256.
It collected no timings; original catalog/source/failure archive are retained.
Correction changes metadata only, preserving Python source and empty file maps.
Normal supervised Lython/CPython catalog verification passes before B382. The local
allocation diagnostic used the original metadata but the identical script/fixtures;
its original identity is preserved separately. No runtime retiming refinement.

Full platform, original Stress and installed consumers are batched for the next
coherent runtime delivery. R29's completed qualification does not cover R30.
The complete-script performance objective remains active; publication postponed.
