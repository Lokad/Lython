# Execution-owned function workspace capacity

Measured 2026-11-09 on the local Windows development machine, .NET 11 Release.
The baseline is `4175fe6c` (production `99844959`); exact production source and
binary hashes are in [identity.json](identity.json). This is an allocation and
ownership diagnostic, not a quiet-box CPU comparison or a CPython comparison.

One existing execution-owned idle array now retains the largest returned capacity
and serves smaller requests. Remove the two parallel byte ledgers and derive
refunds from actual backing length. Bindings and clearing use logical extents;
active calls and genuinely suspended users retain distinct funded storage.
Larger idle capacity can conservatively cause earlier admission refusal.

The unchanged nested-constructor CSV script allocates about **0.81% less** under
both execution APIs. Ordinary CSV/text/JSON and compilation allocation are
essentially unchanged. Five complete scripts use independent Python goldens;
all output, zero-balance, fixture cleanup and assembly-isolation checks pass.
Includes mediated file I/O, output validation and prior-result cleanup; compilation
is separate. Excludes startup, fixture reset/protocol and final cache teardown.

Elapsed results are **inconclusive**. The original image has an adverse async-text
result (+7.7–11.35%); corrected-image unchanged JSON controls differ by26.43%.
The large constructor elapsed reduction and wide-workspace observations do not
establish CPU improvement. Preserve both [original](original-summary.json) and
[corrected](summary.json) observations; no broader performance claim follows.

The first logical-clearing prototype failed a new deferred-prefix reference
retirement test under both APIs. Corrected cleanup clears the compiler workspace
even when no invocation context was created. Original failures and source remain
in [observations.tar.xz](observations.tar.xz), together with catalogs, raw rotated
samples, drivers, nonincremental build logs and focused validation.
[Delivery source](delivery-source.json) records focused actual .NET11/10 checks;
initially preceded the full delivery batch. [Delivery42615908221](delivery-ci.json)
on78fd7bb4 passes Windows/Ubuntu actual .NET11/10, original Stress both and
installed core consumers, including the subsequent valid-DEFLATE correction.

No new pool, reservation layer, public option or cancellation policy was added.
