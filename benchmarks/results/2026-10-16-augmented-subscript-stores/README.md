# Executable subscript augmented assignments — 2026-10-16

One existing configuration read/edit/write job, warm synchronous execution:

| Engine | Revision/version | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython baseline | a21034029a1963ce6d46352850c334f50775b3e8 | 3.338 ms | 6.9% |
| Lython candidate | 4846928c72dd22f318c8d5257dba4fe595912b9f | 3.309 ms | 10.4% |
| CPython | 3.13.16 | 1.695 ms | 4.9% |

The 0.9% lower candidate median is smaller than observed spread. **No performance
improvement is established.** This job contains one subscript augmented update;
it measures the complete workload and cannot attribute costs to that operation.
No repeat, retuning, new producer or long lane was performed. The source
simplification stands independently of inconclusive timing.

The compiler now retains receiver/key in the existing operand stack, reads through
the shared item helper, evaluates RHS/in-place operations, then shares the
three-slot store with a value-last layout. Ordinary and yielding updates avoid
their statement fallback, fallback-only mirroring and prepared read dictionaries/
arrays/1,024-byte working reservations. Yielding ordinary subscript stores share
the same store, so the replaced prepared subscript read/store arms are deleted.
There is no new opcode/frame/owner/binder/cache. Required slice/member/general
routes remain. Native indices convert again after RHS; custom/mapping keys retain
identity. In-place effects survive a later store failure, following Python.

The unchanged `scripts.config-edit.small` script has 357 UTF-8 source bytes,
16 existing paths / 181 input bytes and three stdout bytes (`17\n`). It reads JSON,
edits fields (including `config['retries'] += 1`), appends a path, and writes the
complete file. Each invocation resets disk fixtures and verifies exact output
bytes and stdout against independent goldens. Both revisions pass all three
existing configuration sizes; see [baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json). No script caching assumption changes.

Timers include invocation setup, imports, JSON, mediated file operations,
fixture reset, exact output checks, projection and prior-result cleanup.
Compilation, process startup, transport and full hashing are excluded. This is
warm Run against ordinary warm CPython; it makes no compile-to-result claim.

Measured on the dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD
EPYC 9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16
with ordinary GIL/GC and `-I -S`. Normal GC/tiering, cooperative allowance,
preflights, cancellation and managed stack protection remain active; instruction
fuel is unset. CPython has no equivalent containment. This short diagnostic has
no milestone noise/host-latency qualification and supplies no qualified ratio.

[Raw timing](warm.json) preserves all attempts and binary identities.
[Declaration](comparison-declaration.json) freezes three production differences;
[cleanup/source proof](cleanup-source-proof.json) verifies identical comparison
sources, role-specific loaded revisions, seven exited workers, seven removed
file-fixture directories, inactive services and the available exclusive VM lease.
The owned timing command finished in 11.668 s including cleanup. Receipt transfer
needed explicit filenames because SFTP did not expand shell braces; timing ran once.

[Local validation](local-validation.json): 2,514 ordinary white-box tests,
1,042 focused public and 320 governed/representative/shared-consumer cases pass;
groups overlap, each below 30 s including cleanup. Ten new native cases cover
routing, yielding stores, mode selection and funded/denied in-place growth.
Thirty-four public cases include 16 independent [CPython programs](cpython-goldens.json),
14 actual host success/cancellation paths, retained results through another run
and target/caller spans. A final additional structural assertion passes in the
ten focused native cases after rebuild; production code is unchanged from the
full white-box run. Original baseline failures and fixture corrections are
recorded; no timeout, containment or budget assertion was relaxed. No new Stress
run is claimed. Fuel thresholds and conservative admission can differ when
fallback instructions/working reservations disappear.
