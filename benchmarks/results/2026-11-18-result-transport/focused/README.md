# Stable result transport: isolated focused prototype

R531,2026-11-18 UTC. Fully qualified **925397f65 / reference2c6b304b** versus
private **76555c49(net11) / 4124f5c2(net10)**. Two runtime files change five
private parameters from by-value to readonly-reference transport through native
Publish/Put and common PublishInstructionResult/CompleteResultConsumer layers.
Stable private result locals survive synchronous publication; authoritative
stack/local storage still snapshots values. Retirement, reentrant admission,
protocols, actual suspension, scalar identity materialization and owner transfer
retain their existing snapshots and cleanup. No public API/layout/provider change,
wider metadata, arena, runner, policy, runtime counter or new framework.

Hypothesis follows the current [same-process call attribution](../../2026-11-18-native-call-body/corrected/README.md):
an explicit16-byte result shuttle is the hottest named native Call block.
Sampling skid and useful/inlined work prevent treating its53samples as removable
time. Eliminating intermediate copies may reduce register pressure and helper/
floating script CPU; magnitude and even direction remain unmeasured.

The settled nonincremental Release solution build passes with zero warnings or
errors. Both actual.NET11/10 pass **151internal/84public** focused checks, covering
slot/value transfer, invocation boundaries, tuple/scalar identities, helper locals,
containers, generators, real async cleanup, cancellation, terminal admission
refusal, retained aliases and budget reuse. Existing regressions are reused;
no test-only checkpoint counters or implementation-shape tests are added.

One-off Roslyn caller-shape audit inspects81publication sites and finds no direct
backing element or ref-local argument. It is a navigation aid, not proof of every
alias/reentry behavior or a permanent architecture test. Object overloads and
rvalue expressions establish their own private snapshots; mutable backing must
never be borrowed through retirement. Existing slower caller-window lessons apply.

**Not promoted or fully qualified. No allocation or speedup claim.** Next verify
complete frozen images, existing goldens/allocation/reuse, then a bounded source
pair(helper warm/fresh,floating warm,positional control), separate compilation,
and actual affected assembly. Confirm an eliminated shuttle and decide measured
debt/retention or withdraw this footprint. This contained transport step does not
complete the broader call/frame/collection performance objective. Full platform,
original stress and installed Release consumers belong at a retained checkpoint.
Normal optional adapter0.4/core dependency edges unchanged; publication postponed.

[Identity](identity.json),[checks](checks.json),[caller audit](caller-audit.json),
[source](candidate-sources.tar.xz),[patch](change.patch),
[procedures/logs](procedures-and-checks.tar.xz).
