# Short-script invocation attribution

R366/B754, 2026-11-14 UTC. Committed `1c5af539` / frozen `ff2b5d76`,
[fully qualified separately](../2026-11-14-native-integer-workspace/delivery/README.md).
Quiet Ubuntu 24.04.4/F4as_v7, four EPYC9V45, .NET11RC3. Existing workers,
adapter0.4, normal GC/tiering plus PerfMap only. Two five-second 199Hz cpu-clock/fp
captures after eight guest seconds of warmup. No elapsed/source-pair or allocation
comparison; no speed forecast. All active interpreter/caller/GC threads retained.

| Complete workload | Guest samples / thread CPU | Wider capture |
| --- | --- | --- |
| 96B JSON config edit, read and write | 450 /2.39s | 955 samples,742 unknown leaves,0 ambiguous;5.02s active-thread CPU |
| 2,049-line early-exit search | 776 /3.92s | 1,012 samples,372 unknown leaves,0 ambiguous;5.18s active-thread CPU |

Config has289 kernel-unknown guest leaves. Strict nearest core owners place227
samples under OpenedByteFile.OpenAsync; owning stacks include benchmark-host
component/kind validation and FileStream acquisition. Builtin creation owns only6
nearest samples; context construction8. Worker Run/Take owns57, including useful
synchronization and bookkeeping. CPU sampling cannot establish blocked scheduling
latency or justify bypassing dedicated managed stacks.

Source confirms one core host open per guest open, with original path/contained Cwd.
The benchmark host checks components, kinds and symlinks before acquisition; its
filesystem work is not duplicate interpreter initialization. Module setup does
not copy builtins into module globals. Do not share mutable per-execution type
graphs or weaken host authorization to obtain a benchmark gain.

Search retains ordinary dispatch77/native entry58/native loop55/ForNext41 leaf
samples. These include useful binding and execution, not removable shares. File
iteration already pulls directly from IPyIteratorValue, and decoded read windows
already borrow admitted UTF8 backing. No missing protocol adapter or second line
conversion is established. The initializer/scheduler hypothesis is not selected
for a prototype; this is not a permanent design ban. R364's slower merged entry
and R268's slower single engine remain separate evidence.

Next bounded question: valid-data defensive helper forms with try/except currently
require ordinary activation (CanRunNativeInstructions excludes exception regions).
Compare complete existing invoice/search scripts with and without those helper
regions on identical inputs and this same image. This measures the whole syntax
form, not frame cost alone. Require a material, repeatable script-level effect
before selecting a protected-helper activation redesign; retain compilation,
allocation, CPU, semantic checks and unchanged controls separately. No prediction
or production change follows from the source exclusion alone.

The initial nearestCoreOwners field matched assembly mentions in parameter
signatures too. The corrected strictNearestCoreOwners field checks the method's
declaring assembly. Both derived views and the original raw stacks are preserved;
leaf counts and the conclusions above do not rely on that broad classifier.

B753 fails before creating a root, worker, unit or fixture at the254MiB staging
floor. Before B754, retire only four exact withdrawn-prototype DLL aliases/two
inodes after proving bytes against committed archives, SHA, owner, link count and
paths under the idle lease. Current images, catalogs and toolchain remain. The
254/252MiB staging/archive floors are unchanged; free space after cleanup is
276,865,024B. Failed conditions and custody proofs are in the collection archive.

All image/catalog/output/file/budget/quiet/join gates pass. Raw bytes are verified
under the lease before RAM teardown; no workers or fixtures remain. SHA256
`90cdbe6c7362541dee945580c1ddd3eff7e52626a8f1f283f4e9e9e6e7e8321b`.
Publication stays held; the representative performance objective remains active.

[Config](boundary-census-config.json), [search](boundary-census-search.json),
[raw](observations.tgz), [collection, failed conditions and analysis](collection-and-analysis.tar.xz).
