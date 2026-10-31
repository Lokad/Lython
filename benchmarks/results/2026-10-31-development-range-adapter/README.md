# Development range-cursor adapter

**Development evidence, not released-adapter qualification or current-HEAD timing.**
2026-10-31. Baseline b5450ffd adapter; candidate that source plus
[source.patch](source.patch). Both use the exact same **PythonRe 0.5.0-dev.1**
producer. Core production source is unchanged from 3653b968. Loaded assembly
hashes are in every observation. Normal builds still use PythonRe 0.4.0.

The independent UTF-8 flag-prefix portion is committed as **4f2f6e1c** against
normal 0.4. The remaining [development-range.patch](development-range.patch)
applies to that buildable adapter and is not promoted by these timing results.
No core dependency, public provider API, conditional backend or build option is
added. The optional adapter remains non-packable. Development does not wait for
publication; only final released-package qualification waits.

## Integration and semantic finding

The development cursor calls MoveNext(out Utf8PythonRangeMatch, currentToken),
then transports only absolute capture byte ranges/LastIndex through the existing
Lython contract. It removes per-advance detailed snapshots containing unused
strings and per-advance cancellation registrations. Private construction-token
forwarding remains: the cursor cannot retain an earlier caller's token. The
current advance token reaches actual matching directly. Existing conservative
working/program owners and preflights remain; this does not adopt or claim
qualification of the producer's new funded-program overloads. Its Dispose call
is a no-op for the unfunded compile entry used in this bridge.

Directly mapping EffectiveOptions was **rejected before timing**. Two existing
CPython goldens fail: a guest flag object can dispatch verbose parsing but report
32, or report 2 without implicit Unicode/Verbose bits. Producer parsing options
cannot replace that Python value. The original validated-prefix analysis remains,
now scanning supplied UTF-8 without accessing backend Pattern text. No test
expectation, flag-object protocol or core contract was weakened.

Remaining conversions/capabilities: legacy GetGroupNames strings are encoded once;
one-shot matching still uses detailed snapshots; reversed pos > endpos match
still reports an explicit capability error. XML does not demonstrate native name
enumeration or direct range match/search/fullmatch. Those are precise final
integration gaps, not reasons to stop core or development work.

## Exact development inputs and isolation

- PythonRe package SHA256: `e4136eddffdca5da0b9e5733cd9534053d7d99880714669b3695be5305ed7019`.
- Exact base package SHA256: `47216c6c75e1e2a6c980058e690e7f595a53525c37ad8efb35d2094c95bf467b`.
- Both local 0.5.0-dev.1 packages identify producer source
  `0e24cecebbb9093a18f5cb962dae3b6f6426742c`; they are not claimed publicly released.

Frozen local copies feed isolated MSBuild imports/output/intermediates/packages/
locks under ignored agent notes. Standard project pins, locks and output files
are untouched by that build. The [focused-check archive](focused-checks.tar.gz)
retains the exact imports/configuration, logs, failed and corrected TRX files,
and bounded cancellation runner. To reproduce, provide these exact package bytes
in the private feed, adjust absolute import/output paths, and use existing
DirectoryBuildPropsPath/DirectoryBuildTargetsPath/DirectoryPackagesPropsPath hooks.
This is experimental plumbing outside core, not a supported second build system.

## Focused validation

On **each target**, the development bridge passes **376 public** regex/fnmatch/glob
and **84 native** focused cases. The three public subprocess cases locate ordinary
build outputs, so they are excluded from isolated tests; the **actual isolated
fixture** is invoked directly for compile/match/cursor under its original
25-second kill/join bound. All six actual-runtime probes observe work still
running at cancellation, propagate the original token and end at zero allowance.
[Cancellation results](native05-cancellation.json) include loaded adapter/producer
hashes. Entry/exit checks alone are not used as proof.

The initial isolated baseline also records five missing workflow fixtures; copying
the existing fixtures into the isolated search root resolves them (40 selected
scenario cases pass). This is a build-isolation issue, not a producer semantic
failure. The two EffectiveOptions semantic failures and corrected full run remain
visible. No tests or timeouts changed. Separately, the promoted native-prefix
change passes **84 native + 379 public** cases per target on normal **0.4.0**,
including its ordinary three owned cancellation subprocess cases.

## Fixed allocation diagnostic

Existing [driver](driver/Program.cs), four warmups, seven rounds of five complete
warm public RunAsync jobs, full return/stdout validation and result disposal.
Compilation/startup are excluded; common reflection overhead is included. Each
process has a 30-second bound and finishes normally. Windows 10.0.26200, 28 logical
processors, .NET 11 RC3, ordinary GC, equally disabled tiering; no concurrent local
build/test. The [frozen sources](driver/cases.json) and independent CPython 3.13.2
[golden generator](driver/cases.py) are unchanged. The file/text control uses the
maintained 1,000-line discard job. This is not a timing qualification.

| Shape | Legacy 0.5 B | Native 0.5 B | Identical legacy B | Change | Separate legacy 0.4 B |
| --- | ---: | ---: | ---: | ---: | ---: |
| capture split, 300 matches / 1,200 bytes | 410,704 | 384,281.6 | 410,704 | -6.43% | 410,944 |
| template sub, same captures/input | 245,928 | 219,488 | 245,928 | -10.75% | 246,168 |
| 100 match.expand calls | 203,440 | 203,368 | 203,440 | -0.04% | 203,720 |
| complete text-file control | 252,224 | 252,224 | 252,224 | unchanged | 252,224 |

[Before](before.json), [after](after.json), [identical-before](before-control.json)
and the separate [0.4 bridge](bridge04.json) retain every sample and identity.
The first three isolate adapter changes at one producer pin; 0.4-to-0.5 differences
are separate. The historical A02 substitution regression is only partly mitigated;
these numbers do not erase it or show a whole-application speedup.

## Fixed VM comparison and limits

Eight predetermined jobs, two observations, **62.49 seconds**, below the 120-second
outer bound. Both quiet gates and every complete stdout/file golden pass. Dedicated
Ubuntu 24.04.4 F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3. CPython 3.13.16
is an independent control in each job. Existing micro tooling uses one-second
warmup, settling and seven rotating 25-ms batches with ordinary GC and equally
disabled tiering. Warm execution excludes compilation/startup; fixture setup/reset,
full validation and cleanup are included. Regex jobs print the same independently
frozen scalars; the unchanged VM text-helper control uses its maintained large
file workload. No unrelated-median phase subtraction.

Microseconds per complete job; delta is native / legacy minus one:

| Job | Observation 1 legacy / native | Delta | Observation 2 legacy / native | Delta | Legacy/native IQR, observations 1; 2 |
| --- | ---: | ---: | ---: | ---: | --- |
| regex.capture-split | 781.2 / 3,915.6 | +401.3% | 1,134.9 / 1,301.1 | +14.6% | 24.8/33.0%; 249.2/232.1% |
| regex.template-sub | 537.9 / 557.2 | +3.6% | 537.5 / 515.7 | -4.1% | 185.9/220.8%; 170.0/231.8% |
| regex.match-expand | 432.7 / 736.8 | +70.3% | 542.1 / 437.2 | -19.4% | 105.6/37.3%; 75.5/127.1% |
| scripts.text-summary-helpers.large | 6,447.1 / 6,103.9 | -5.3% | 5,695.3 / 5,878.2 | +3.2% | 5.2/21.6%; 6.2/8.2% |

The first split observation increases **401%**, the second 14.6%; expand/sub reverse.
Several spreads exceed 100%, reaching 249%. Keep every unfavorable row. Quiet
controls alone do not qualify these timings. **No speedup, no absence-of-regression
claim, and no promotion of the range patch on this evidence.** No retiming or branch
rescue follows. The source-level architecture/development semantics are tested;
actual backend performance and final released-adapter qualification remain distinct.
The primary CPython table is unchanged.

[collection.json](collection.json) summarizes jobs. The [raw VM archive](raw-vm-results.tar.gz)
preserves receipts/logs/gates, exact catalog bytes and [collector](collect.py).
All owned workers finish; the outer service completes successfully and releases
its lease. No background matching or production profiler is introduced.
