# Supplied dev.9: development integration passes

2026-11-02, Windows .NET 11 RC3 / .NET 10.0.8. **Development qualification only**;
final released-adapter qualification and backend performance remain separate.
No core/adapter source, ordinary 0.4.0 pin, normal lock file or dependency change.
Lython source base is `650b4766`, runtime source `4cae38f4`, adapter `d703767c`.
The independent aggregation tests added in this checkout are outside this selection.

Both immutable 0.5.0-dev.9 packages identify producer
`2c6cb298c4841983f74af9587275263df86536cb`:

| Package | SHA256 |
| --- | --- |
| Lokad.Utf8Regex.PythonRe | `54dbe8129568130e3c33b7917f40650e4e71a7bee3919cf5f1d6daf51fb2cfcf` |
| Lokad.Utf8Regex | `4dfd805494110b2a3b9905eb3a874d63fffb93b5e9b8c0ddf64e2405cb3d4c34` |

The private feed/cache/outputs/intermediates/locks leave normal builds unchanged.
All twelve loaded producer assets, across the internal/public suites and worker
fixture on both targets, match their exact same-target package DLL bytes.

The existing regex/fnmatch/glob selections pass **84 internal / 424 public tests
per runtime**. Six additional owned real compile/match/cursor probes confirm work
was active at cancellation, the original token propagates, all users finish and
the allowance returns to zero. Existing 25-second process bounds and assertions
are unchanged. Root-relative immutable fixture data and the newly built private
worker are staged before testing; no setup-failure replay or fallback is needed.

Reflection of the exact same-target package confirms all **143 declared public
Utf8PythonRegex members are unchanged** from dev.7; the documented API also has
no additions/removals. Native capture-name enumeration and one-shot ranges with
a working-funding receipt remain missing. Reversed-window behavior is still an
explicit capability error in the current adapter, while core/test-provider Python
semantics continue. No adapter bridge can be deleted based on this API change.

No performance measurement was needed for this unchanged adapter architecture;
producer benchmark improvements do not establish a Lython speedup. Ordinary
0.4/dev.7 checks were not repeated. The final released immutable package still
needs managed-safety and real-backend qualification, independently of core
development and releases. No sibling edits, package publication or adapter
packaging occurred.

[Exact packages, loaded assets, checks and remaining capabilities](summary.json),
[actual cancellation results](native09-cancellation.json),
[API inventory](api-dev9-net11.json), [frozen scope](pre-code-plan.md),
[validation logs, test results and isolated build configuration](validation-logs.tar.gz).
