# Immutable dev73 adapter input

2026-11-12. Retained Lython `2ec9d67f`, immutable producer **0.5.0-dev.73** from
`eae880c88f7a54ed4331715c2fd8f4b0fc6e6d63`. Packages are copied from the accepted
unpublished candidate feed, with package SHA, dual-target DLL and nuspec commit
verification. Moving sibling HEAD and live package outputs are not substituted.
[Identity](identity.json), [source/capability assessment](capability-assessment.json).

**86 internal and 532 public checks pass on each actual .NET 11/10**, including
semantics, UTF-8/scalar/window behavior, effective flags, current cancellation,
refusal, cursor lifetime and budget reuse. The unchanged development bridge uses
Validate, cursor-local working admission and per-operation cancellation forwarding.
Loaded backend hashes match exact supplied package bytes. All 29 normal-state
source/pin/lock/asset/output files remain byte-identical. [Qualification](qualification.json),
[isolation proof](normal-state-proof.json). Normal adapter dependency remains 0.4;
core stays independent, adapter nonpackable, and no sibling edits/builds/repack occur.

The initial public .NET 11 log has no terminal outcome. Process inspection proved
runner/test children absent before resuming unfinished checks against the same
frozen binaries. The partial log, resumed logs and terminal TRX remain preserved;
no runtime failure or cause is inferred from the missing original result.

Changed producer private parsing/capture/scalar/value paths justify qualifying a
new binary. Four relevant public API files are byte-identical to qualified dev68.
The same final producer gaps remain:

- Single-match working ownership separate from retained result ownership.
- Native UTF-8 group-name enumeration; native name lookup already exists.
- Token-independent cursor creation with cancellable preparation; per-move tokens
  alone do not establish interruption during preparation.

No new bridge/core abstraction is selected. Temporary conversions remain adapter-
local. This is development-input semantic/contract qualification, not backend CPU,
source-safety, AssumeValid, normal-pin promotion or final published-package
certification. Missing producer APIs defer only affected final adapter qualification.
Lython's separate exact-source full platform/stress/package delivery is green in
[43119174279](https://github.com/Lokad/Lython/actions/runs/43119174279).
Publication remains postponed. [Exact packages, bridge, commands and logs](preparation.tar.xz),
[archive identity](archive-identity.json).
