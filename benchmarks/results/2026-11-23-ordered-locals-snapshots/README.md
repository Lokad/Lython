# Ordered optimized scope snapshots

B1468 compares retained source `6e01ec965` (frozen candidate
`41a396d968`) with `d969c134f`. A single declaration sweep replaces
separate lambda and generator scans. A small admitted map projects
nonparameter cells and free variables in Python name order while
ordinary locals and parameters retain their binding order.

The new CPython regression fails before the change and passes through
both execution APIs. An additional BMP identifier probe matches
CPython. Supplementary identifiers remain a separate lexer limitation.
Executable inspection found 162 identical code objects and one closure
order difference. An origin inspection verified 18 links, including the
two reindexed forwarded links. Instructions, operands and native flags
match throughout the inspected corpus. Both frozen images passed eight
CPython fixture goldens. Windows .NET 11 and Ubuntu .NET 10 each passed
140 white-box and 895 public Release checks.

## Controlled comparison

Measurements use the dedicated Ubuntu VM, pinned .NET 10, the same
worker and adapter files, independent processes and RAM-backed fixture
files. Each mode has two processes per image and seven timed batches
per process after an eight-second warmup. All 196 batches and teardown
checks completed under the same quiet lease. Percent changes below
compare every candidate worker median with every reference worker
median. Negative values indicate lower cost. CPU spans the wider
process window. Compile-to-result includes compilation, execution and
joined cleanup.

| Fixture and boundary | Elapsed change | CPU change |
| --- | ---: | ---: |
| Dead-heavy validator compile | −2.97% to +3.06% | −5.62% to +3.77% |
| Sparse validator compile | −1.50% to +1.04% | −3.40% to +2.00% |
| Postponed factory compile | −0.41% to +0.25% | −2.70% to +0.89% |
| Read-only branches compile | −2.35% to −0.07% | −4.04% to −3.18% |
| Sparse validator compile-to-result | −1.97% to +0.97% | −1.18% to +3.31% |
| Inherited dataclass compile | +0.38% to +1.47% | +1.00% to +3.48% |
| Short nonlocal text warm | −7.83% to +0.77% | −7.56% to 0.00% |

Local compile allocation rose by at most 0.24%, or 192 bytes on the
nonlocal fixture. The largest absolute increase was 408 bytes (0.08%)
on the inherited dataclass.
Its admitted peak remained unchanged. The largest admitted peak
increase was 254 bytes on the nonlocal fixture. Retain the change for
Python semantics and the simpler scan. Preserve the adverse dataclass
control for the next compiler checkpoint.

[Frozen identity](identity.json) · [Full timing summary](summary.json) ·
[Compile allocations](compile-allocations.json) ·
[Executable parity](executable-parity.json) · [Capture origins](capture-origins.json) ·
[Terminal receipt](terminal-status.json) · [Raw observations](observations.tar.xz) ·
[Procedures and validation](procedures.tar.xz).
