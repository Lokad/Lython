# Bind module JSON output options directly

On 2026-11-18 UTC, B972 compared 227d80b47 (f2824fea0) with the isolated
7170869d core. The two verified 32-file workers differed only in the core DLL.
The quiet Azure F4as_v7 VM used .NET 11 RC3, default GC and tiering, and the
optional 0.4.0 adapter. Both images matched CPython's complete file outputs
and stdout for three unchanged catalog programs.

The exact builtin module path binds serializer options directly. It removes
the private mutable encoder, dictionary writes and reads, and the bound
`iterencode` call for `json.dump`. Sync and async share option selection.
Explicit encoders and subclasses retain their observable attribute and method
dispatch. The managed call frame, separator enumeration before encoding, flag
effects, callback suspension, UTF-8 storage and consumer order remain covered.

| Complete script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Config edit, warm | −1.34 to −0.82% | −1.40 to −0.16% | 0.36 / 0.17% |
| Config manifest, warm | −2.44 to −0.60% | −2.19 to +0.11% | 0.69 / 1.18% |
| Config edit, compile-to-result | −3.46 to +1.09% | −2.27 to −0.21% | 2.52 / 2.14% |
| Config manifest, compile-to-result | −2.07 to +0.07% | −0.20 to +0.82% | 0.60 / 1.57% |
| Positional calls, warm control | +0.22 to +2.79% | −0.81 to +3.33% | 0.12 / 2.44% |

Ranges include all pairings of two process medians per revision. Negative
favors the candidate. Each process had eight seconds of rotated warmup and
five measured rounds. Warm includes mediated RAM I/O, complete output
checks and prior-result cleanup. Compile-to-result adds compilation and
compiled-owner disposal. Process CPU includes wider worker activity.

Separate local allocation measurements found 36,616 to 35,152 bytes per
configuration edit (−4.00%) and 109,224 to 107,352 per manifest (−1.71%).
Both accounted peaks and the positional control's allocation matched.
The supplied allowance returned to baseline. The warm edit improved with
supporting CPU. Fresh elapsed is unresolved, and the positional control's
movement remains visible. Retain the direct binding and allocation savings.

Each actual .NET target passed 213 white-box and 888 public focused tests.
Ten probes matched the previous Lython's stdout, error outcomes and reported
peaks through both APIs on both targets. Seven also matched CPython 3.13.2.
Three preserve prior custom-writer chunk boundaries, including the prefix
observed when a writer throws on its third call. An initial probe compared
human report formatting to raw stdout. The structured rerun isolates guest
output and preserves the original evidence.

All five jobs completed in 232.48 seconds. Quiet endpoints, worker join,
fixture cleanup and archive SHA checks passed. Staging retired only an exact
archived older generator core replica and two committed raw replicas.
Full platform and package-consumer validation is a later combined delivery.

[Source patch](source.patch) · [Timing](summary.json) ·
[Allocation](allocation-summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz)
