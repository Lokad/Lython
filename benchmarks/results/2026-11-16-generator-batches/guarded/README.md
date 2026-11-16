# Guarded bulk-yield engine: private pending constructor consolidation

R458/B865,2026-11-16UTC. Qualified9e8d1cf5/e92d9598 versus private82e45812 plus
archived8dd7eda1. The inactive bulk helper is checked at its three callers; ordinary
Advance/AdvanceAsync remain original. **No production promotion or full qualification.**

| Complete workload | Phase | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Normalization,4,096lines/52,224B | Warm | −17.31..−11.69% | −15.76..−10.62% |
| Same normalization | Compile and run once | −15.39..−8.20% | −14.86..−7.04% |
| Ordinary generator/list/render control,16,384values | Warm | +3.51..+13.16% | +3.66..+13.56% |
| Numeric report,4,096records | Warm | −4.21..+14.21% | −3.56..+13.50% |
| CSV invoice control,4,096rows | Warm | −0.60..+0.67% | −0.58..0% |

All four pairings of two process medians/source retained. Negative means faster.
Normalization is consistently favorable across the archived shapes, but ordinary
generator consumption remains adverse here. Its candidate spread6.70%limits
magnitude. Numeric reference spread15.22%is inconclusive, not proof of failure or
absence of regression. Normalization reference/candidate spreads1.54/5.17%warm,
3.78/4.55%fresh. Allocation differs by only32B per affected invocation (<0.006%);
accounted peaks/compiled allowance unchanged. This is neither an allocation gain
nor a universal CPU improvement. All adverse originals remain [linked](../README.md).

Decision: do not ship the partial drain-only integration yet. Next bounded
construction milestone assesses existing PyList private capacity/rollback as one
sequence builder, removes the async tuple staging list/separate per-item growth
route, and shares native buffering with constructor consumers. It must retain
Python identities/order, mutation, real suspension, first terminal refusal and
funded aliases. Conservative capacity/scalar headroom can deny earlier and must
be documented/tested on useful budgets. No ledger, arena, controller, wider value
representation or replacement execution backend. A changed-source whole-script
comparison must decide remaining CPU debt before retention; no unchanged retiming.

Quiet AzureF4as_v7 Ubuntu24.04.4,.NET11RC3 normal tiering/workstation GC,two
processes/source,6s full-request warmup,five rotated~0.5s samples/process. Five
jobs192.911s; maximum within-process IQR2.24%. Warm includes Run, mediated RAM I/O,
all stdout/file goldens and prior-result cleanup; excludes compile/startup/reset/
protocol/final-cache teardown. Fresh adds compilation and compiled-owner disposal;
CPU clocks are wider. No new CPython timing/RSS/embedding-tail claim. All identity/
quiet/Closed/exit/join/archive gates pass; evidence SHA-verified persistently under
lease before RAM teardown. No users remain; terminal cached permanently. Only
exact committed older prototype-core replicas retired with fresh ownership/path/
hash/all-inode-alias proof; current images/toolchains/canonical evidence preserved.

Focused actual Windows.NET11/10 each167public/59internal pass, including seven
semantic/refusal rows per target. Seven frozen-worker/CPython goldens and five
independent semantic programs agree. Refused admission inside a generator cannot
be suppressed and sequential executions restore the shared allowance baseline.
An initial local verification path referred to the wrong copied helper directory;
that setup failure and correction are preserved, with no runtime assertion relaxed.
Full platform/stress/installed-package qualification waits for a retained design.

Independent regex source refresh records00e7905d removing UTF16 result coordinates;
both local dev105 package hashes remain unchanged. Cursor creation-token retention
still justifies the existing bridge. No producer source build, package promotion,
new qualification or safety certification; core remains decoupled, normal adapter0.4.
Missing published producer capabilities defer only affected final qualification.

[Results](summary.json),[allocation](allocation-summary.json),[validation](validation.json),
[frozen inputs/images](identity.json),[source](candidate-sources.tar.xz),
[exact core](measured-core.tar.xz),[raw evidence](observations.tgz),
[focused checks](focused-checks.tgz),[procedures/custody](procedures.tgz),
[next source gate](sequence-construction-assessment.json),
[provider source/package distinction](provider-source-assessment.json).
Publication and tagging remain held; the whole-script objective stays active.
