— ordinary compiler cancellation (next bounded correction).**
Demonstrated source gap: Parser receives CancellationToken, but ordinary Parse,
CurrentToken and ReadToken do not observe it; only nested f-string tokenization
uses it. Lexer/provider checks do not establish interruption after lexing. This
is an existing limitation, not a defect introduced by B46. Do not claim ordinary
parser or later analysis is currently cancellable. Correct current documentation
and report remaining stage limitations precisely.

B47 reproduction BEFORE production changes: six new native parser cases fail;
one public annotation-storage cancellation case returns a valid script. Public
simultaneous refusal/cancellation already attributes SharedMemoryAllowance.
Deterministic token-view reads and real annotation working-storage admission
supply the cancellation windows; no timers, large fixtures or checkpoint counts.
Implementation scope: central token consumption/recovery, parser scans/literal
loops, missing forwarding to existing f-string scanner tokens; checks before/after
major compilation phases. Preserve existing 64 parser-descent limit, 512 delimiter
and 256 unary bounds. CLR copies/normalization and later static-analysis,
lexical-fact/lowering/executable passes still finish their current work before
phase checks unless they have their own checks. Document that limit explicitly.
One fixed complete CSV/text compile comparison plus unaffected warm text, existing
4warmups/7batches (compile512warmups/4096batch), before/candidate/identical-before,
<=60s collection plus cleanup; cost diagnostic, no cancellation acceptance target.

Smallest coherent change: propagate the existing current token through ordinary
parser work, including nonallocating consumption/recovery and long literal or
expression paths where existing loops can delay observation. Use existing token
checks and attribution, no fuel/watchdog/counter/test-only checkpoint machinery.
Check before advancing later frontend/lowering/executable stages; identify any
long analysis loops still needing their own checkpoints rather than claim phase
entry/exit checks prove in-flight interruption. Keep diagnostics for uncancelled
source, managed nesting and coarse admission/owner cleanup. First refused
admission remains authoritative if cancellation also arrives. No public option,
new policy, general compiler framework or cancellation abstraction.

Acceptance: focused native parser check with a token cancelled after tokenization,
plus controlled in-flight ordinary parsing/public compilation cancellation and
shared-budget cleanup/reuse. Independent uncancelled semantics/diagnostics pass;
no exact checkpoint-count assertions or relaxed timeouts. Use existing focused
Release suites on both actual targets. One short unchanged-workload compilation
comparison reports any cost separately; no performance target is a reason to
omit required cancellation. Full delivery only at retained coherent source.
Stop this slice once this demonstrated gap is fixed and remaining phase coverage
is explicit; do not begin a broad compiler/ownership audit campaign.
