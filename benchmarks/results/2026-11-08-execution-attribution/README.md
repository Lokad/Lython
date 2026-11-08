# Current execution attribution

Source `913ea550`, core SHA-256 `18b563e90d70554ef01bc93e4eec1d63a540e87fed385c98e8368f5887a1f887`.
This is diagnostic evidence, with no runtime change or speedup claim.

B444 profiles the complete synchronous text-summary/helpers workload on the
existing quiet Ubuntu F4as_v7 VM. There are 1,083 actual guest-thread samples
over 5.45 CPU seconds, including 49 unknown samples (4.52%). Perf maps and JIT
listings are diagnostic overrides; this is not a normal-settings timing result.
All source/core/auxiliary/Ready, complete pre/post goldens, quiet endpoints,
worker exit/join, fixture removal and archive verification gates pass.

| Self samples | Share | Interpretation |
| --- | ---: | --- |
| Instruction prefix | 20.96% | Includes inlined work; not entirely removable dispatch |
| Input handlers | 5.63% | Includes useful value operations |
| Whitespace range collection | 4.62% | Useful tokenization, not allocation alone |
| Structural hashing | 4.43% | Existing exact-string path already applies |
| Local-result completion | 3.51% | Includes binding, retirement and pending cleanup |
| Native-call selector | 3.42% | Includes useful native execution |
| Exact integer addition | 2.03% | Insufficient evidence for a universal value rewrite |

Offline address-to-JIT-block analysis places 112 samples in the prefix's loop,
instruction acquisition, checks and opcode selection. These are subsets of the
227 prefix samples, not additional CPU shares. Other prefix samples include
inlined operand access and native-iteration work. Inclusive stacks overlap.

Source verification rules out two tempting but incorrect premises: whitespace
split already reserves its complete result in a batch, and exact strings already
bypass general numeric hash preparation. Replacing either with another accounting
or dispatch layer would not remove the alleged work.

R81 contrasts the same core executing complete text/CSV main bodies at module
scope or inside a function. Helpers/imports remain at module scope; all stdout
and output files independently match CPython. Function forms allocate slightly
more (under 0.07%), and elapsed observations are unstable, including tiering drift.
This does not establish a module-binding bottleneck. Namespace/introspection
semantics deliberately differ, so wrapping arbitrary scripts is not an admissible
compiler transformation. No namespace redesign is selected on this evidence.

The next source/measurement gate must identify a complete removable phase across
representative scripts. Contiguous call arguments already borrow storage; earlier
arena/carrier regressions remain relevant. Small integer-add self time alone does
not attribute boxing or justify changing every guest value representation.

[Complete evidence](observations.tar.xz) contains the verified VM trace archive,
maps, raw stacks, JIT listings, unknown samples, source inventory, offline analysis,
selection contracts, original same-core observations and CPython golden checks.
The frozen core and source inventory are also preserved in the adjacent
[native-workstate evidence](../2026-11-08-native-workstate/README.md).
Owned RAM fixtures were released only after workers and trace users joined;
persistent evidence was verified under the VM lease and independently locally.
