# JSON integer construction source gate — R46

2026-11-07, baseline8c9fd0d7/frozen d3a6c778, prototype on metadataad74fa41.
**Withdrawn. No runtime change is delivered.** This bounded decision prevents
extending the standalone int kernel into a materially more efficient decimal
parser solely to remove source duplication.

JSON already parses Int64 tokens from UTF-8 and gives hooks independently funded
raw text. Its large default still reserves magnitudes and decoded working text,
uses CLR BigInteger.Parse, then funds its retained result. The prototype replaced
that with the R45 native accumulator/radix chunks, deleting decoded copies, a
sole-production-caller preflight helper and its old infrastructure-only test use.
Small JSON and all Python int forms shared one admission/commit path. JSON grammar
remained validated separately. No collector, quota, new allowance or unsafe code.

Focused Release208internal/986public checks per actual.NET11/10 pass, including
JSON grammar/raw_decode, hooks/suspension, cancellation/refusal/ownership and
standalone integer semantics. An initial pair of new funding assertions incorrectly
compared balances across execution-frame cleanup (expected59B, observed−133B).
The fixture now observes result funding before frame cleanup; both failures and
the original test source remain. This was a test correction, not a runtime fix.

Reuse the existing unchanged five-case reflection driver/normal GC/tiering:
three separate ALCs sharing CoreLib/process GC/hash witness,32warmups/image,
five rotated rounds*4complete jobs. Two explicit numeric JSON fixtures keep the
maintained source unchanged:4096records of40-digit values and16records of4096-digit
values, plus ordinary CSV/text/JSON controls. Isolated CPython3.13.2 independently
validates full output/files. Separate sync/async diagnostics include mediated I/O,
goldens, result disposal and fixture handling; compilation/startup excluded.

| Input | Sync allocation | Async allocation | Diagnostic elapsed vs two baseline roles |
| --- | ---: | ---: | --- |
|40-digit JSON values|+10.515%|+10.511%|sync−5.11to+2.19%; async+4.95to+12.39%|
|4096-digit JSON values|+2753.905%|+2748.787%|sync+6.98to+11.67%; async+15.44to+21.95%|

Ordinary allocation is within0.007%; unchanged desktop elapsed controls move up
to9%. Clocks are diagnostic, not a CPU/VM improvement claim. The precise large
allocation regression is sufficient to withdraw this representation, rather than
repeat quiet timing or hide the adverse condition behind the40-digit case. The
CLR parser uses materially fewer arithmetic intermediates than radix-chunk
accumulation here. R45's separate comparison against its old per-digit Python int
parser remains valid; it is not evidence against CLR's decimal parser.

Supported.NET10/11 reference assemblies expose BigInteger.Parse for char spans,
not a direct public byte-span overload. A generic numeric-interface bridge is not
adopted as proof of native parsing or removal of its internal conversion. Future
work may revisit this boundary with a materially different bulk algorithm or
supported backend capability; this is not a permanent prohibition or fixed-percent
veto. Do not introduce a thresholded second parser merely to rescue this attempt.

All goldens, funding, image isolation and cleanup gates pass. Original/core/test
snapshots, failed assertions, scripts, precise allocation and raw adverse clocks
are preserved. Exact production/source restoration checked against B400's560-file
inventory. Restored local binaries need a nonincremental build before reuse.
No new VM/platform/package campaign or publication. The broader text/report/call/
collection/frontend performance objective remains active.

[Raw development/source/images](development.tar.xz), [decision](decision.json),
[complete-job summary](local-summary.json), [focused checks](focused.json),
[original failed assertions](failed-focused.json), [source gate](source-gate.json),
[independent fixture goldens](fixture-validation.json) and [identity](identity.json).
