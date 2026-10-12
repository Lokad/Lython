# Complete-script comparison worker delivery

Measured producer for correctness/timer smokes:
`ed9870879594b9567ccf9106c71d01ccdd194658`, Release on Windows with .NET
10.0.12 / SDK 10.0.300-preview.0.26177.108 and CPython 3.13.2. Production tree
`6f5e6e8f9130fa0cd2349396ffddb32a157f4c9b` is unchanged. These are local
correctness checks, **not a performance baseline**, quiet-machine qualification
or a claim about today's HEAD. See [BENCHMARKS.md](../../../BENCHMARKS.md).

The existing comparison workers now run the four ordinary CSV/text/JSON/file
scripts at 0, 16 and 1,024 fixture repeats. Both use physical disk files in a
parent-owned temporary cwd. Each invocation resets/stages the inputs and validates
every declared output byte inside its execution clock. File-digest hashing stays
outside persistent clocks; compilation-only batches do not execute or stage files.
Cold samples include owned directory creation, launch, execution, drain and exit;
directory cleanup follows outside the sample and within the owned group deadline.
No new benchmark framework, runtime switch or protection bypass was introduced.

The first Windows persistent smoke retained two failures: CPython's default
text encoding changed the Unicode text report at both nonempty scales. File-job
launches now explicitly use `-I -S -X utf8`, matching Lython's text contract.
Pure catalogs retain their prior flags. The failed receipt remains preserved;
it was not turned into a timing sample or retried for statistical advantage.

Committed gates:

- **58 Release checks** cover complete-script file output, fresh-state replay,
  compilation boundaries, same-length wrong/missing/extra files, malformed paths,
  forged digests, persistent/cold public workers and the existing protocol/micro checks.
- **31 Release supervisor checks** cover framing, watchdogs, descendant ownership,
  pipe draining and cleanup. A dedicated file-hold fault proves a killed worker
  cannot leave its open file or directory behind.
- **12/12 persistent cases** match in both engines, including before/after
  verification and warm, compile-run and compile-only batches.
- **12/12 fresh-process cases** match in both engines after independent preparation;
  all 24 once-only children return the expected stdout and output-file digest.

The two test groups take 9.062 / 13.544 seconds including cleanup; the persistent
and fresh smokes take 4.888 / 7.667 seconds. Each has a 30-second external bound.
Two earlier broad regression groups hit their 28-second execution deadline;
those partial receipts remain failures, not full-suite results. The smaller
focused groups completed. Ordinary full-suite CI has a separate finite limit.

Source, binary, catalog and receipt hashes are recorded in [evidence.json](evidence.json).
Raw exploratory and committed receipts remain under ignored
`.git/agent-notes/file-workers-20261012/`. The lasting managed-only constraint
is preserved: new file-host/fixture code uses managed APIs, and the pre-existing
benchmark process supervisor's native ownership implementation is unchanged.
No production file capability, cancellation, memory accounting, forced-GC policy,
thread policy or managed stack-admission behavior changes.

Quiet-VM comparative timing, host-impact qualification, parser/lease closure
on failures and forced-GC independence remain open. This delivery supplies the
file comparison mechanism; local smoke clocks must not populate the performance table.
