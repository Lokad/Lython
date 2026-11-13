# Positional ZIP input: implementation checkpoint

2026-11-13. Frozen core97bbeff3,575production inputs. This is a focused-test
checkpoint; full delivery and the controlled whole-script comparison are pending.
No CPU or allocation gain is claimed yet.

Subsequent completion: the [source-pair comparison](comparison/README.md) and
[full delivery](delivery/README.md) are now recorded. The text below describes
the original implementation checkpoint, including its original failures.

The optional Lython-owned ILythonPositionalByteFile contract fixes an opened
readable extent and preserves identity/cursor/lookahead. A capable read-mode ZIP
uses governed end/directory/local-header regions and selected compressed input,
instead of acquiring/retaining the entire archive. Sequential hosts select the
existing admitted buffer path before work; refusal never changes strategy.
One parser serves both sources, including eager local-header checks, wide ZIP64
offsets and CRC validation. Read/open/testzip and extraction perform genuine async
host operations. Private decoded output remains funded through scratch retirement,
transfer, host borrows and cleanup; opened members remain independent after close.
Estimates deliberately admit construction overlap and may reject earlier.

The migration removes duplicate synchronous extraction execution. It also uncovers
and fixes ZIP64 extra-field order (file size before compressed size), physical-order
overlap bounds without reordering the directory, malformed foreign-header CLR
exception leakage and late/mutable-name index construction. Index admission now
precedes capacity allocation and keys use the original directory snapshot.

Focused Release results:27new public cases on each actual.NET11/10 target;
49public/26internal on.NET11, including archive-config worker lanes. Earlier
488public/137internal focused cases also pass. Coverage includes a1MiB archive
under a256KiB allowance with under80KiB acquisition, sparse directory offsets
above4GiB, path replacement, real provider cursors/extent, short reads, retained
member output, CRC/truncation errors, admission before destination borrowing,
actual constructor/member suspension and cancellation/refusal with joined close.
Independent CPython3.13.2 goldens validate foreign-info and name-index behavior.

Original failures remain in source-checks.tar.xz: initial nullable compilation,
empty-value ownership, malformed-header CLR leakage, unsupported json.loads(bytes)
worker verification and the ZIP64 field-order regression. ZIP-config deliberately
uses the same explicit UTF8 decode in both engines. Native JSON byte input is a
separate planned correction. The initial2MiB-padding catalog was never launched;
the selected1MiB workload already demonstrates the acquisition/retention gap and
reduces fixture storage. No source-pair result is inferred from these tests.

The comparison reuses existing workers/goldens. The reference keeps its old worker;
the candidate worker adds the optional file capability and fixture. Their hashes
remain explicit. The unchanged normal0.4 adapter is byte-pinned across the pair;
the newly built Git-metadata variant is preserved separately. Core remains decoupled.

[Exact sources, binaries, checks and original failures](source-checks.tar.xz),
[independent Python output](python-info-goldens.log).
Publication remains postponed. This checkpoint does not finish the wider roadmap.
