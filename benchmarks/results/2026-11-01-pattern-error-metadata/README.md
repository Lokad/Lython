# Python regex error metadata (B60)

2026-11-01. Retained for Python semantics and ownership correctness. The previous
runtime discarded provider scalar positions and lacked PatternError attributes.
The core now preserves original patterns, raw messages, positions, line/column,
ordinary mutable __dict__ and stable args. Its constructor supports optional and
keyword arguments, bytes/scalar coordinates and actual asynchronous protocols.
No new provider/backend dependency, error framework, owner or default allowance.

[Plan before code](pre-code-plan.md), [exact frozen-source patch](source.patch.gz).
The optional PythonRe0.4 adapter documents UTF-16 positions/-1 unknown. It now
normalizes on failure using borrowed compilation UTF-8 or its existing retained
pattern string. Successful matching adds no pattern copy or decoding. Provider
messages exclude position suffixes; Lython formats the original pattern's scalar
position. Managed strptime has no synthetic pattern and reports unknown fields.
Final newer released-adapter qualification remains a separate gate.

Twenty-one [independent CPython3.13.2 goldens](../../../tests/Lokad.Lython.PublicApi.Tests/Fixtures/RegexErrors/cases.json)
cover ordinary/omitted/keyword arguments, supplementary Unicode, bytes, multiline,
negative/past-end/unknown positions, mutable/deleted/replaced fields, stable args,
ordered message/index/pattern protocols, real adapter and replacement-template
errors. All run through sync and async public execution. Twelve lifetime controls
cover five provider stages plus unknown positions, terminal cancellation/admission,
held rendering, two simultaneous executions sharing one allowance, retained
patterns/results, awaited actual users, disposal back to baseline and reuse.

Error payloads now retain funding after failed compilation. The native regression
therefore checks zero outstanding temporary reservations and retained metadata
until owner disposal, then zero shared balance. It does not require refunding
storage that the new error still retains. The fixed test replaces its former
message-only/baseline assumption. Initial fixture failures also exposed omitted
optional arguments in the new constructor; defaults were corrected. Two held-test
setup mistakes (missing timing capability and projecting an unsupported custom
message) were corrected without production workarounds. All failures are preserved
in the validation logs. Focused Release:219 native/1,294 public per actual.NET11RC3/
10.0.8. Full Windows/Ubuntu original-stress/package delivery is batched with B59.

## One fixed short cost comparison

Source9620192a plus the exact patch; original B59 corec19c2c68, fresh candidate
14937ef4. [Binary identities](candidate-identity.json) prove fresh nonincremental
core/worker/test/probe agreement. The private comparator retains identical old
adapter/host auxiliaries; neither job invokes a regex provider. The changed real
adapter is qualified by semantic tests, separately from core timing.

Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, .NET11RC3.
Images share CoreLib/verified actual hashes and ordinary GC/tiering. Full warm
execution includes fixture reset, mediated I/O, output/file goldens and result
disposal; excludes compilation/startup. No CPython ratio or phase subtraction.
128 interleaved warmups/image,2s settle/case, seven rotating32-job batches.

| Complete job | Original ms | Candidate ms | Original control ms | Status |
| --- | ---: | ---: | ---: | --- |
| CSV helpers, 4,096 records | 8.28897 | 8.07523 | 8.15245 | Valid controls; -2.58%/-0.95%, no material regression |
| 128 PatternError raises/catches | 0.79347 | 0.91025 | 0.81943 | Inconclusive; +14.72%/+11.08% raw observations |

CSV drift-1.65%, IQR0.77–1.14%, batches>=254.99ms. Error batches24.91–29ms
fail the prospective>100ms resolution gate; original drift+3.27% exceeds3%.
IQR0.69–4.09% alone cannot qualify it. The previously absent metadata requires
real additional construction/storage, but these samples do not establish its
size. No retiming or favorable threshold change. [All observations/gates](summary.json).

Quiet/hash/full-golden/balance/cleanup gates pass. Service exits0 in an18-second
UTC window within45s; inactive/dead/success, MainPID0/empty group, lease free.
[Driver](Program.cs), [collector](collect.sh), exact criteria and [raw results](raw-results.tar.gz)
SHA256`523b660872e503377c994a892547063b524274b818d02a8365ea6c2d7e596df3`
preserve all samples. No qualified speedup, embedding-latency or external-baseline
claim. The final accounting test refinement is test-only and postdates this
frozen patch; measured production source is unchanged.
