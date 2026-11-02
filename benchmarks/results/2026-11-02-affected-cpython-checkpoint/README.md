# Affected CPython checkpoint

2026-11-02, exact source af82bdfc (runtimefacca3e9), CPython3.13.16, .NET11RC3, quiet dedicated Azure F4as_v7 Ubuntu24.04.4 machine. Same source/fixtures/full output-file and stdout goldens, actual worker/reference binary identities and ordinary worker GC/tiering. Source has the retained consuming-output change; withdrawn dispatcher/CSV prototypes are absent. **Diagnostic external comparisons, not qualified release multipliers or controlled earlier-Lython speedups.**

| Workload / lane | Lython | CPython | Lython / CPython | Lython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers, compile+run |7.490ms|3.690ms|2.03x|1.95%|
| Text helpers, compile+run |6.370ms|1.472ms|4.33x|2.07%|
| CSV helpers, warm |7.304ms|3.498ms|2.09x|2.82%|
| Text helpers, warm |6.117ms|1.300ms|4.70x|1.95%|
| CSV helpers, compile only |85.11us|54.21us|1.57x|0.66%|
| Report, compile only |57.30us|38.21us|1.50x|0.31%|

Identical current Lython controls differ+0.77% fresh CSV,–0.41% fresh text,–0.07% warm CSV and–3.09% warm text. Earlier082179c4 warm CSV/text were6.390/5.755ms; today's higher clocks are preserved. These are independently conditioned collections with different source/identities and historical control drift; their difference does not establish an optimization regression or improvement. CPU/allocation claims still require the controlled internal comparisons. Substantial external gaps remain.

Existing framed comparison protocol/CPython worker and immutable B104 fixtures are reused. Four-second conditioning per engine, nine counterbalanced batches targeting200ms, baseline/candidate identical Lython images and CPython primary reference. No new benchmark framework. Complete fixture staging/reset, efficient mediated I/O, full stdout/file verification and prior-result disposal occur inside execution batches. Startup/IPC/response hashing/final cached-owner/process/fixture teardown are excluded. Fresh Lython disposes every compilation inside; compile-only retains its last compilation until later cleanup. Last execution result stays funded through response then disposes outside the timed batch. Independent phase medians are not additive.

**Supervisor correction and status:** the first76.110s service completes all four execution jobs with goldens, joined workers and removed owned fixtures, then a private reused guard incorrectly demands stdout/files from a compile-only batch. Maintained workers intentionally emit no actual output for compilation. The four jobs are completed supervisor prefixes; the original final machine gate was not reached. The original failed condition, collector and journal remain. No execution sample was retimed or discarded.

The correction requires null actual output/files only for compile-only batches while preserving completed counts and echoed source/fixture/golden metadata. Verify and all execution requests still require exact full output/file hashes. Only the two missing compile jobs run in a new38.267s owned service with the exact same binaries/fixtures/conditioning. Both its quiet gates pass (the first is also a later post-stop check for the original collection), and every worker joins/removes its fixture. Both groups are terminal/inactive/MainPID0/empty/lease free; no background or matching work remains. This does not retroactively turn the original supervisor into a successful campaign.

[All medians/IQR/control drift](summary.json), [original partial service and failure](initial-evidence.tar.gz), [completed compile-only correction and quiet gates](followup-evidence.tar.gz), [exact collectors/protocol/fixtures](collector-and-fixtures.tar.gz), [prospective correction](correction-plan.md), [binary identities](identity.json).

Runtime delivery is fully green at this exact source in cached terminal [41449285870](https://github.com/Lokad/Lython/actions/runs/41449285870): Windows/Ubuntu ordinary.NET11/10, original Stress both and installed Release consumers both. This validates runtime correctness/resources; it does not turn the diagnostic ratios or incomplete initial supervisor into a qualified performance campaign.
