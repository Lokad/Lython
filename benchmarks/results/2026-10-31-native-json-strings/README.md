# Native JSON string materialization — rejected

The prototype is removed. Its fixed acceptance condition required at least 10%
lower complete-script allocation on both maintained JSON shapes, substantial
removed machinery, and no repeatable material script/control regression. The
first allocation gate failed, so the planned VM timing collection was not run.
There are no timing retries or retained alternative decoder paths.

Baseline is **bf33d502139f614bb8afcd722936b9d3cce05cb7**; candidate is that revision
plus [source.patch](source.patch). These are source-specific diagnostics, not a
new CPython baseline or a claim about current HEAD. The optional adapter is
unchanged; these JSON workloads do not use it.

## Hypothesis, candidate and correctness

The existing token path scans a borrowed string, reserves blanket 4x scratch,
validates/decodes with Utf8JsonReader, and rewrites lenient bare controls with
original-offset translation. The candidate replaced that entire string path
with original-coordinate UTF-8 validation and a shared escape decoder. Ordinary
strings acquired one independent copy; escaped strings acquired one preflighted
private byte array plus their independently admitted result. It removed the
backend error normalizers and outer catches without changing container/number
parsing, options, hooks or suspension behavior. No parser cache, storage
framework, unsafe code or new budget policy was added.

Focused net11 Release checks passed **169 native / 729 public** cases with the
candidate, including ownership, retained aliases, host-driven batches, real
callbacks, cancellation and terminal refusal. Prototype-only fixture changes
are preserved in [prototype-tests.patch](prototype-tests.patch) and the added
[semantic goldens](prototype-semantic-tests.cs.txt); they are evidence, not active
suite requirements. CPython 3.13.2 goldens included original scalar/line positions,
malformed-escape precedence after unsupported surrogates and EOF Unicode escapes.
Candidate backend-specific message changes were deliberate, but are **not shipped**.
The restored runtime retains its existing backend wording and UTF-8 restrictions.

## One fixed allocation diagnostic

[driver/Program.cs](driver/Program.cs) adapts the existing complete-script
allocation diagnostic, using the unchanged representative catalog and file host.
The boundary is warm public Run, including fixture reset/staging, full stdout/file
goldens and result disposal. Compilation/startup are excluded; common reflection
cost remains. Four warmups precede seven rounds of five executions per shape.
Every invocation returns allowance to its compiled baseline; compiled disposal
returns it to zero. No forced collections are used.

| Complete JSON-lines script | Before allocated B / invocation | Candidate | Identical-before | Change |
| --- | ---: | ---: | ---: | ---: |
| Plain | 5,615,155 | 5,221,984 | 5,615,155 | -7.0% |
| Helpers | 7,878,205 | 7,484,944 | 7,878,205 | -5.0% |

Both miss the recorded >=10% requirement. Do not weaken that threshold after
seeing the result or tune branches to rescue the candidate. Local elapsed medians
are unqualified: plain 30.22 / 25.13 / 27.63 ms, helpers 31.73 / 38.22 / 32.95 ms.
They are not evidence of a speedup or a stable regression.

Machine: Windows 10.0.26200, 28 logical processors, .NET 11 RC3
(11.0.0-rc.3.26522.148); ordinary GC and tiered compilation disabled equally.
No local Lython build/test overlaps collection. All three owned diagnostic
processes complete; no background work remains. [Before](before.json),
[after](after.json) and [identical-before control](before-control.json) preserve
all rounds, machine/runtime details, catalog/assembly SHA256 identities and
ownership balances. The catalog is the maintained
[representative.json](../2026-10-31-architecture-profiles/representative.json),
SHA256 `a2b51dfe71c890faeda7198687298f58b377f0c615bce2ecec065c20db16587f`.

The planned <=180-second VM comparison (two fixed warm/fresh observations with
CSV/text controls and config editing) was not started. That avoids expanding a
failed allocation experiment into a timing campaign. Compilation, I/O and cleanup
attribution remain separately documented in the earlier
[architecture profiles](../2026-10-31-architecture-profiles/README.md); do not
infer those phases by subtracting independent medians here.

## Restored state and stopping point

No production decoder changes or estimate-specific fixture edits remain.
The explicit rebuilt benchmark core matches the frozen baseline byte-for-byte:
SHA256 `7cbb74fafdcb354d3549562c4168ccdf0b519a9aaf65f09d0d57decfeb3ecbb7`.

Four independent cancellation/lifetime controls remain: cancellation raised by
actual working-storage or final-result admission must prevent a returned value,
unwind frames/reservations, preserve input and permit full shared-budget reuse
after cleanup. They pass the **existing** decoder on both targets, with **167
native JSON checks per target**. They count no checkpoints and require neither
scratch architecture. No new runtime fix or performance gain is claimed.

Stop this candidate. It reduces token machinery but does not meet the material
complete-script allocation criterion. Do not rerun unchanged qualification or
repackage unchanged production. The next independent runtime investigation must
address a demonstrated larger cost; final released-adapter qualification does
not block that work.
