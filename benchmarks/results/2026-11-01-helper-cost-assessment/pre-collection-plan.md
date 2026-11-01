**B58 — bounded whole-script helper-call cost assessment (before any frame redesign).**
The source/CPU read finds no redundant ordinary argument package: positional and
named calls already bind existing operands into the final local array. The stable
lexical context remains necessary for closures, names, traceback and guards; the
interpreter holds real suspended state. B34 allocation percentages alone do not
justify pooling/fusion/arenas. B43 has small named arithmetic/call-helper self
counts, with inlining and allocator/monitor origins unresolved; it cannot prove
that boxing or frames dominate. Do not reopen rejected shared-buffer/retention,
keyword-binder, shared-loop or local casing/parser proposals.

One precise unresolved question: does the per-record helper call materially add to
CURRENT complete text/CSV elapsed, compared with the identical script spelling its
known helper expression directly? Keep original function definitions, annotations,
fixtures, output and fresh-run setup; replace only parse_amount(row['amount']) with
int(row['amount']), or words_from_line(line) with line.lower().split(). This trusted
source diagnostic preserves these two scripts' results, not arbitrary Python
inlining semantics. It changes code shape/name resolution/JIT behavior as well as
calls; no claim that the whole difference can disappear through a frame redesign.
No runtime code, binding cache, value representation or benchmark framework change.

Use existing frozen original worker/source95b5955c; verify current production tree
matches it. One<=45s owned same-process assessment, same core/host bits and shared
CoreLib/actual input hashes: original/helper-expanded/identical-original images,
128 interleaved warmups/image/case,2s settle/case, seven rotating32-job batches.
Complete warm public jobs include reset/mediated real I/O/full stdout/file goldens/
result disposal, compilation/startup excluded. Ordinary GC/tiering; all outliers.
Independent isolated CPython results for both source forms, all quiet/hash/output/
funding/cleanup/exit gates mandatory. Every batch>100ms, relative IQR<=10%, original
control medians within3%. A >=15% elapsed reduction against BOTH originals on at
least one script supports a material helper-cost question; it is not an accepted
runtime speedup or permission to implement a universal frame/value rewrite.

Stop after the one assessment: if inconclusive/immaterial, do not retime, tune,
change gates or start frame work from allocation counts. If material, document
which concrete state/transport can disappear while retaining lexical identities,
locals/closures/generators, integer identity/overflow, protocol callbacks, exception
cleanup, genuine suspension, current cancellation and shared admission. Only then
freeze a separate design/semantic/deletion/ownership and complete-job comparison
contract before code. No discarded candidate is automatically reopened.
