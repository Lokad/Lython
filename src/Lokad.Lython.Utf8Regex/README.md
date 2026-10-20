# Lython Utf8Regex adapter

Optional PythonRe 0.2.0 integration for Lython. The core package has no dependency
on this adapter or its backend. Bind `Utf8RegexProvider.Instance` explicitly when
constructing `LythonEngine`.

This immutable backend has no public CancellationToken-taking compile or matching
operations. The adapter reports a capability error for cancellable backend work;
it does not pretend that entry/exit checks interrupt matching, start background
work, or introduce a timeout/work quota. Already-cancelled operations cancel.

Compiled storage borrows the compilation/execution owner. Prepared working scopes
share that allowance and refund on disposal. Buffered iteration receives a coarse
estimate for possible captures and construction overlap before allocation. Large
subjects, many captures and replacements can therefore reject much earlier than
their eventual output size suggests. No independent allowance is imposed when the
supplied root is unbounded. Hosts retain acquired inputs until all users finish.

Buffered matching can fail to terminate on multi-scalar subjects containing
supplementary Unicode. Those buffered operations report a capability error
before entering the affected backend. One-shot
Unicode projection preserves the existing scalar-coordinate fallback and validates
it against the original UTF-8 subject. The native `.*?` cursor is unaffected.

Mixed named/unnamed numbering and nested `lastindex`/`lastgroup` retain the supplied
backend's known limitations. Scalar offsets and normal capture values are adapted
from UTF-8 ranges. The existing `.*?` progression workaround lives here, outside
the core provider contract. Backend safety and semantic qualification are separate
from core qualification; installing this adapter does not certify those internals.

Validation separates the [cooperating-provider contract controls](../../tests/Lokad.Lython.PublicApi.Tests/RegexProviderContractTests.cs)
for in-flight cancellation and funded unwind from [real-adapter capability controls](../../tests/Lokad.Lython.PublicApi.Tests/RegexAdapterCapabilityTests.cs).
The established [Release package gate](../../tests/README.md#delivery-checks) restores independent
core-only and adapter consumers. The [historical integration-cost diagnostic](../../benchmarks/results/2026-10-19-regex-boundary/README.md)
is unqualified timing, not backend cancellation or safety certification.
See [the regex guide](../../docs/REGEX.md) for the current provider and adapter contract.
