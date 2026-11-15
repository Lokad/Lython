# Live async defaults after admission

R402, runtime **6e276759**, one production source change. The activation
assessment uncovered an existing correctness defect: async binding inspected
`__kwdefaults__` before workspace admission. A host could replace that dictionary
during admission, after which a newly introduced Python key protocol incorrectly
ran synchronously. This is a correctness correction, not a CPU optimization.

Remove that early eligibility snapshot. The shared binder checks the actual
default dictionary immediately before lookup, finishes native dictionaries
synchronously and defers protocol lookups in order. Borrowed arguments already
belong to their final slots before a lookup suspends. No extra argument array,
owner, cache, policy, retry or execution route. Sync binding and public APIs stay
unchanged.

The new regression replaces native defaults during actual workspace admission,
then holds a key comparison genuinely suspended. Success returns the original
payload alias; cancellation and first structured memory refusal prevent body
effects and await that user before cleanup. All three final cases fail on frozen
**51e3/22e5** and pass on the changed runtime. The original forty-local failure
also survives; the final fixture uses seventeen locals to require fresh working
storage without a larger fixture. Reference failures are expected regression
evidence, not failed product qualification.

**204 unique focused Release cases pass per actual .NET11RC3/.NET10.0.8**:
71 internal transport/default/boundary cases and 133 public helper/argument/order/
ownership cases. The final smaller regression separately passes on both targets.
The runtime also passes the [full Windows/Ubuntu .NET11/10, original stress and
installed-consumer checkpoint](delivery/README.md). The original focused receipt
preserves its dispatch-time status separately from this completed qualification.

## Short complete-script diagnostic

One reused framed-worker source screen, copied frozen images, same CoreLib,
worker, adapter, catalog and full stdout/file goldens. Exactly one of581 checked
C# production inputs changes. Candidate core is45dd2da1; complete file identities
are retained. The existing protected search script has a diagnostic wider-helper
variant to exercise ordinary async binding; its input/output bytes are unchanged.
Three processes/job,1.5s full-request warmups, five rotating short batches.

| Complete file-search case | Before µs | Changed µs | Unchanged control µs |
| --- | ---: | ---: | ---: |
| Wider keyword-default helper /async |9,079.64|8,709.33|9,542.68|
| Same /sync control |8,806.58|8,530.50|9,649.07|
| Original protected helper /async control |7,892.05|8,145.62|10,221.04|

Desktop filesystem costs dominate. Unchanged controls move about5–30%; the
original async control includes an adverse+3.21% candidate observation against
the first reference. **No CPU, quiet-VM or speedup claim follows.** Retain for
demonstrated correctness; do not retime to manufacture a performance justification.
All goldens, budget baselines, worker exits and fixture cleanup pass. Clocks include
mediated file work, full output validation and prior-result cleanup; exclude
compilation/startup/reset/protocol/final cache. No new CPython comparison.

[Checks and limits](summary.json), [all samples](local-screen.json),
[complete image identity](identity.json), [final reference proof](reference-regression-proof.json),
[TRX, source patch and procedure](checks-and-procedure.tar.xz),
[committed source proof](source-proof.json), [both complete images and source](source-and-images.tar.xz).
Initial test/accessor and screen-filename setup corrections are recorded; no
measurement or runtime failure is discarded. Publication remains postponed.
