# Shared native regex result shaping

Architecture accepted; no speedup claim. Source is `83b19237` plus the frozen
[source.patch](source.patch). The before worker was built from `3d0cd5e6` plus
the CSV patch committed as `83b19237`: its production sources are equivalent to
that commit; the build's embedded revision still names its construction source.
Every observation records loaded assembly hashes. PythonRe **0.4.0** is identical
on both sides. These are revision-specific diagnostics, not today's HEAD or a
new CPython comparison.

Lython now owns split, template substitution and match.expand using one native
Python template parser and absolute capture byte ranges. Providers expose only
matching and finditer progression. The optional adapter's string-template/split
operations, private replacement cache, high-water funding and lock disappear.
The existing compiled-pattern cache remains unchanged. A template plan lives
for one operation, is reused for every match and releases scratch after actual
users finish. Retained output obtains separate admission. This deliberately
removes the old adapter's private cache across separate substitution calls.

## Fixed allocation/timing diagnostic

2026-10-31, Windows 10.0.26200, 28 logical processors, .NET 11 RC3; tiering
disabled equally, ordinary GC. One fixed before / after / identical-before
sequence uses the [existing diagnostic's adapted driver](driver/Program.cs),
four warmups and seven rounds of five warm public RunAsync calls per shape.
Each call validates return/stdout and disposes its result; compilation/startup
are excluded. Reflection overhead is common. No concurrent builds/tests run;
each owned process has a 30-second external cap and completes normally.

Capture split/sub operate on 1,200 ASCII bytes / 300 matches with an optional
second group. Expand performs 100 calls on one match. The unchanged complete
text-file control uses the maintained TextScanBenchmarks discard script on 1,000
six-column lines, including mediated file iteration and cleanup. Golden scalars
are computed independently by CPython 3.13.2 in [cases.py](driver/cases.py).
This is not a quiet-machine or backend-only performance qualification.

| Shape | Before B / invocation | After B / invocation | Allocation change |
| --- | ---: | ---: | ---: |
| Capture-heavy split | 485,824 | 436,555 | -10.1% |
| Template substitution | 123,632 | 246,168 | +99.1% |
| 100 match.expand calls | 177,371 | 203,720 | +14.9% |
| Complete text-file control | 284,192 | 284,192 | unchanged |

The identical-before allocation control matches the listed baselines. The
substitution regression stays visible: the bridge's detailed cursor still
materializes unused strings/snapshots per match, whereas its former backend
Subn path shaped output internally. The new core also validates capture ranges
and admits its own plan/output. This experiment does not separate those costs
or attribute every extra byte to the producer. No backend template API is needed
by the core; a released range-only cursor can remove the temporary bridge cost.
Expand's new plan/piece storage also has an explicit allocation cost in exchange
for the single shared grammar and deterministic private-storage cleanup.

Median milliseconds before / after / identical-before: split 1.03 / 1.43 / 0.98;
sub 0.29 / 0.38 / 0.27; expand 0.25 / 0.22 / 0.25; text 0.72 / 0.58 / 0.70.
Timing is diagnostic only; the substantial unchanged-control spread prevents
a qualified elapsed or control-adjusted speedup claim. Preserve
[before](before.json), [after](after.json), [identical-before](before-control.json),
all rounds, stderr and exact source/driver. Do not retime favorable rows.

## Correctness and stopping point

Both targets pass 1,013 focused public and 62 native checks: independent CPython
replacement errors/escapes/counts, Unicode names, windows, empty/unmatched groups,
real callbacks/suspension, range validation, cancellation during matching and
capture processing, terminal refusal, construction overlap, retained output and
budget reuse. The cooperating provider implements no shaping APIs; it is contract
evidence, not a benchmark of an actual regex engine. Actual 0.4 compile/match/cursor
interruption remains separately checked in owned fixture processes.

Retain the smaller Python-specific contract and shared shaping implementation.
Stop this slice rather than adding a template-cache lifetime framework or a
second backend shaping path. Final released adapter qualification remains open;
it does not block independent runtime development. Full delivery qualification
is recorded separately in PLAN.md when complete.
