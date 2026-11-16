# Generator consolidation delivery qualification

GREEN [CI 43884041092](https://github.com/Lokad/Lython/actions/runs/43884041092)
at `cc794368`. Windows/Ubuntu actual .NET 11/10 each pass 4,806 internal and
17,930 public ordinary tests, original 14 internal and 406 public stress tests,
and installed explicit Release package consumers. All 16 summaries and 613
committed inputs (585 core production files) verified. Matching source text
connects the measured private prototype to qualified production; rebuilt binary
equality is not inferred. Normal optional adapter remains 0.4.0.

Local nonincremental Release build passes both targets with no warnings/errors;
the full local .NET 11 ordinary suite passes the same counts. Relevant focused
checks on both targets remain in the parent evidence. Original failed fixture,
class-cell and staging conditions remain preserved. Qualification does not resolve
moving benchmark controls, prove RSS/embedding latency or imply additional speedup.
Five additional pure lazy alias/type-parameter probes agree with isolated CPython
under both APIs. An initially uncaught class-barrier NameError agreed in both
runtimes; the harness was corrected to catch that expected error, without changing
the runtime. Originals and exact probe image hashes remain in local checks.

[Terminal](terminal.json), [verified summaries](qualification.json),
[exact inputs](inputs.tar.xz), [full logs](run-logs.zip), [local checks](local-checks.tar.xz).
Complete terminal and logs are cached forever; do not query or repeat this unchanged
qualification. Publication/tagging remain held; the broader performance roadmap
continues independently of producer publication.
