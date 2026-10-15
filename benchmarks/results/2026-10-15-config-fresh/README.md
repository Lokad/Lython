# Fresh configuration read/edit/write comparison

This fills a missing ordinary agent workload: load JSON, edit a flag and retry
count, append a path, preserve untouched Unicode/nested metadata, and write the
same file back. The [script](../../scripts/config-edit.py) runs once after fresh
compilation inside an already-running host. Identical script caching is excluded.

| Fresh compile and one execution | Lython identical-code controls | CPython 3.13.16 |
| --- | ---: | ---: |
| 16 existing paths / 181 input UTF-8 bytes | 2.817-3.142 ms | 1.572 ms |

One [short diagnostic](compile-run.json), 2026-10-15. **Both Lython processes use
the same code**, revision `654b4f5ade359d776b306a7229d273d883ccf27b`; these are
controls, not earlier/later revisions or an optimization claim. Their medians
differ by 11.5%. IQR/median is 4.7% / 5.7% for the two Lython controls and 9.5%
for CPython. The range contains the two medians, not a confidence interval.
No multiplier qualified; retain the discrepancy without retrying or retuning.
The owned command took 8.751 seconds including cleanup.

Timers include fresh compilation, one synchronous execution, fresh input staging,
result projection, reading/checking complete output bytes, script disposal and
previous-result cleanup. They exclude process startup, protocol transport and
full response hashing. The file host supplies native ranged I/O/append overrides.
This is a whole compile-to-result job; it does not isolate compilation, warm
execution or disk costs, and cannot attribute the gap to any one subsystem.
No startup, compile-only or warm measurement is claimed by this receipt.

The existing comparison machinery is unchanged apart from allowing an **exact**
input/output overwrite in trusted fixtures. Both workers still reject ambiguous
case aliases, same-map duplicates where represented, file/parent conflicts, path
escapes, excessive unique paths and combined payload bytes. Each timed invocation
restores the original input; repeated edits cannot accumulate across invocations.
[Correctness](correctness.json) verifies independent stdout and file bytes at
zero,16 and1,024 existing paths. Zero paths still contains configuration metadata.

Machine: Azure Standard_F4as_v7, Ubuntu24.04.4 LTS, four AMD EPYC9V45 vCPUs,
16GiB RAM; SDK10.0.401/runtime10.0.12, CPython3.13.16 with ordinary GC/GIL,
`-I -S -X utf8`, and explicit UTF-8 protocol I/O. Normal Lython policies,
cooperative allowance, GC/tiering and managed stack admission remain; CPython
does not have equivalent containment. No runtime dependency or benchmark framework
was added. [Declaration](comparison-declaration.json) fixes this case/boundary;
[source/cleanup proof](cleanup-source-proof.json) verifies the exact source archive,
identical Lython binaries, five exited workers/fixture directories, inactive units
and an available VM lease.

The same delivery also fixes reflected augmented callbacks for builtin numeric
scalars and preserves complex/matrix augmented error tokens. Existing numeric
recognition and operator tables are reused; builtin container mutation order
remains. [Local checks](local-validation.json) pass2,306ordinary white-box and
317focused public cases, each group under30seconds including cleanup: all numeric
operator slots, CLR integer widths, actual suspension/cancellation, declined errors,
retained-result funding, fresh file replay and cancelled same-file publication.
Thirteen [error goldens and Python fixture checks](independent.json) and the
[script verifier](cpython-script-goldens.json) independently pass CPython3.13.2.
Original failed regressions are retained privately; no timeouts/assertions relaxed.
Full platform/package gates are recorded at delivery rather than inferred here.
