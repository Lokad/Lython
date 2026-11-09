# Native UTF-8 construction ownership (R167)

Retain native split/ordinary CSV result storage in its existing reservation until
the complete result is ready, then commit once. Remove per-fragment accounting and
the payload completion method's optional reservation/empty-result/returned-object
plumbing. Writers handle empty Python values before allocation; private completion
now only checks and sets a nonempty payload length. No new owner, class or option.
Plain casing keeps its allocation-free single-result reservation balance. Earlier
CSV numeric conversions keep their existing ownership/order; this is not general
reference reclamation or an exact object ledger.

Correctness: cancellation of private construction returns its allowance immediately
while retaining input funding. Three Stress regressions exercise split, cased split
and quoted CSV completion with a short first fragment and64MiB second fragment.
They pass current source and fail all three balances on frozen80a3540d using the
same corrected test assembly. Canceller is ready before admission and joined before
cleanup. Successful completion before token request is legal and must return correct,
funded values; cancellation must refund exactly. An initial fixture incorrectly
demanded cancellation always win; preserve its failure/source, not that assumption.
This is concurrent evidence, not deterministic scheduling/checkpoint counting.

Focused Release **386 internal/824 public per actual .NET11/10** pass, including
Unicode expansions/sigma, empty/whole aliases, cross-owner results, admission refusal,
native CSV value/conversion order, terminal failures and cleanup. The three new long
observations remain Stress only. Documentation states the current ownership contract.

Source pair: reused R164/R165 final precommit freeze80a3540d, production-source
equivalent to96e534f9/dc319b1a, versus freshly nonincremental final R167 working source.
Common frozen worker/adapter unchanged; exact source/binary hashes retained. No
postcommit/future-HEAD or CPython performance claim. First prototype/diagnostics and
the unmeasured casing-wrapper draft remain separately identified.

One quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 confirmation on2026-11-09: four jobs
**115.748 seconds**, four independent processes,5s warmup/role/seven rotated150ms
rounds, ordinary GC/tiering. Includes mediated RAM-file I/O, outputs and prior-result
cleanup; excludes compilation/startup/reset/protocol/final cache teardown.

| Whole warm script/API | Both candidates versus both baselines | Control spread |
| --- | ---: | ---: |
| CSV validation sync |+0.76% to+4.23% elapsed|baseline0.98%,candidate2.37%|
| Text word count/report sync |−3.12% to+0.19% elapsed|baseline2.81%,candidate0.59%|
| File search async control |−3.66% to+0.34% elapsed|baseline3.96%,candidate0.18%|
| JSON configuration async control |−0.85% to+0.87% elapsed|baseline1.54%,candidate0.17%|

Coarse CPU includes reset/protocol: CSV opposite−3.32/+3.13%, text−3.66to0%,
search−6.84to−1.96%, config−0.56to+0.63%. Accept the possible small CSV cost for
the demonstrated private-storage rollback and consolidated ownership. Text overlaps
controls; adverse async controls did not reproduce. No general CPU gain/equivalence.
Retention/debt decision closed; the larger whole-script performance roadmap stays open.

Both desktop diagnostics preserve effectively-flat allocation. Initial textsync
+4.77–6.83%, CSVasync+3.76–6.03% are adverse; after the justified nonempty/void
source refinement, textsync−2.94–−2.45%, but async search+6.83–7.67% and config
+4.51–8.89% are adverse. Keep all controls/raw images; no demonstrated causal
inlining explanation. One quiet comparison resolves the retention decision, not
an assertion that adverse data was invalid. Compilation was not retimed here.

Initial VM space preflight refused before mutation; fresh proof then removed only
four exact tracked B515/B496/B495/B494 compressed duplicates with verified SHA,
single aliases, metadata, parents and completed receipts. Canonical Git archives,
raw trees, receipts and frozen workers survive.240MiB OS/12MiB evidence floors
unchanged. All identity/golden/quiet/owner/Closed/join/unmount/archive-under-lease/
localSHA gates pass; no VM users or fixtures remain, terminal cached.

Full R167 platform/original Stress/installed-consumer qualification is pending the
coherent checkpoint. Predecessor R164/R165 is fully green; its receipt does not cover
this change. Publication remains postponed.

[Quiet summary](summary.json), [decision/checks](decision.json), [source](source.json),
[prototype source](prototype-source.json), [regression proof](regression-proof.json),
[original desktop](original-desktop.json), [refined desktop](refined-desktop.json),
[quiet raw/cores](observations.tgz), [source/checks/desktop/failures](source-checks-desktop.tar.xz),
[space proof](space-recovery.json), [terminal](terminal.json).
