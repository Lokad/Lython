# Direct Python tokenization (B25)

Baseline **472d64ef**, candidate baseline plus [source.patch](source.patch),
2026-11-01. Windows 10.0.26200, Intel i7-14700KF / 28 logical processors,
pinned .NET 11 RC3. The optional adapter stays on PythonRe 0.4.0.

Retained: one Python-specific reader over normalized original source replaces
masked clones, generic rule matching, reconstruction of source metadata and the
parser's continuation-trivia bitmap/workaround. Existing parser, static analysis,
scope facts, lowering and executable engine remain. Minimal Lython token/result
records replace external generic types; enum attributes, FAttribute and the unused
token-namer interface disappear. Core has **no runtime NuGet dependencies** after
removing Lokad.Parsing and its obsolete lock edges. No reader pool, mutable
singleton, fallback backend or new option is added.

The [incomplete B24 profile](../2026-11-01-frontend-cpu-incomplete/README.md)
put the lexical pipeline in 332/1,022 compiler-thread chains. That was a hypothesis
selector, not a removable CPU fraction or qualified timing measurement.

## Fixed comparison and acceptance

| Maintained source | UTF-8 source bytes | Before B/compile | Candidate B/compile | Identical-before control B/compile | Candidate change vs before / control |
| --- | ---: | ---: | ---: | ---: | ---: |
| Configuration edit | 357 | 176,421.6 | 133,672.0 | 175,976.9 | -24.23% / -24.04% |
| CSV helper report | 599 | 253,447.3 | 195,032.1 | 253,778.0 | -23.05% / -23.15% |
| Text helper report | 424 | 214,208.0 | 165,632.0 | 214,168.0 | -22.68% / -22.66% |

One before/candidate/identical-before sequence, four warmups, seven rounds of
1,024 complete public Compile/dispose jobs per source. Shared accounting returns
to its retained compiled control after each batch and to zero after final
cleanup. All compile calls are fresh; startup is excluded. Allocation is
process-wide, not a sampled tick estimate or retained-byte measurement.

The predeclared material gate requires at least 20% less complete-compilation
allocation on two nontrivial sources against both controls, or a separately stable
CPU improvement. **All three pass the allocation gate.** Semantic, cancellation
and ownership requirements are independent; their evidence is below.

Separate lanes perform seven rounds of five complete fresh compile/run jobs and
warm Run jobs. These include the existing fixture reset/staging, mediated file
I/O, full stdout/file goldens, result disposal and accounting checks. Config input
has 1,024 paths / 7,177 bytes; CSV has 4,096 records / 57,361 bytes; text has
3,072 lines / 35,840 bytes. Compile-only excludes host I/O; fresh/warm boundaries
are explicit rather than estimated by subtracting unrelated medians.

Fresh-job median allocation changes respectively **-4.0%, -0.62%, -0.87%**;
warm allocation changes **0%, about -0.02%, about -0.02%**. Reflection and
diagnostic overhead are common. No tests/builds overlap the candidate/control
processes; the workstation is not qualified as a dedicated quiet box.

[Raw before](before.json), [candidate](after.json), [control](control.json) and
[acceptance](acceptance.json) retain every wall/CPU/allocation sample and identity.
There are important timing limitations: text compile CPU controls differ 13.4%;
CSV warm CPU controls differ by over 50%; CSV fresh candidate CPU is higher than
both controls. Configuration compile wall time also increases despite lower CPU.
These are unqualified timings, including regressions, **not an elapsed-speedup
claim, embedding-latency qualification or current CPython ratio**. Required lexical
work remains; its old inclusive sample share is not an improvement estimate.

Each process was owned, bounded at 30 seconds and joined with exit zero. Combined
duration is **24.3098 s**, under the fixed 90-second limit. Process records remain
alongside the raw observations. No forced GC, performance retry, conditioning
policy, VM lane or producer-readiness polling was used.

## Semantics, ownership and limitations

The development-only [oracle](oracle/Program.cs) compares 491 frozen old-reader
sources against the candidate, including maintained scripts, numeric boundaries,
keywords, Unicode identifiers, prefixes, joins and malformed/unclosed input.
Former public diagnostics retain their codes/messages/positions except seven
CPython-confirmed corrections. Malformed internal token streams need not be
identical when the public error remains the same. The oracle is diagnostic
evidence, not a replacement architecture enforced by production tests.

Public tests use independent CPython 3.13.2 [value goldens](python-goldens.json),
cover both invocation APIs, original logical-end diagnostics, shared-budget reuse
and cancellation signalled after source admission. Ordinary strings drop escaped
physical newlines, raw strings retain them, and escaped triple quotes remain in
the correct literal. Whitespace after a continuation backslash is rejected.
Unicode normalization, numeric forms and nested formatted quoting remain covered.

The compiler's coarse source estimate still precedes allocation and covers working
token/line/indent capacity. Changed lexical scans and nested formatted scans receive
the active token, including embedded expressions and annotation re-lexing.
No token-bearing mutable reader escapes to a cache or compiled result. Terminal
admission refusal and existing retained compiled/result ownership remain.
The inherited indentation-width policy is unchanged by this measured patch;
B26 corrects mixed tab/space compatibility separately, without retiming this work.

Release ordinary suites pass **3,942 native / 15,312 public** per framework on
actual .NET 11 RC3 and .NET 10.0.8. After the final logical-EOF correction, both
frameworks pass **113 focused** value/span/provider checks, and the 491-source
oracle confirms the seven intended diagnostic changes. The three new EOF cases
are additional to those initial full sweeps; full delivery qualification is
recorded separately. Raw logs are under checks/.

## Reproduction

Apply source.patch to the baseline and build the existing Release benchmark
project with the pinned SDK. Freeze one worker directory per variant. Decompress
catalog.json.gz; its uncompressed hash must match the raw observations. Build
[lexical.csproj](lexical.csproj) and run [Program.cs](Program.cs) using the
[fixed runner](run-fixed.ps1); its existing temporary-directory guard validates
cleanup targets and it refuses to overwrite observations. Historical output is
immutable: use different output paths when reproducing. No new benchmark CLI or
runtime instrumentation is introduced.
