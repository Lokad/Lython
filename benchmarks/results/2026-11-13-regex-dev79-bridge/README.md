# Isolated dev.79 regex bridge — R299

Consumerd413392c, producer immutable0.5.0-dev.79 packages at891cabbf. Core SHA256
e2293dad0e496fa43c845e899b1f858d2752b72ba68d26d3c3e83aed4afa353c;
PythonRe1e48d685719879a7679043aeb61a2775b1789200832f48707f2291f95d1cb5cc.
Exact copies are preserved in the local feed archive. PythonRe API code is unchanged
from the prior bridge; the producer Core matching path changes. This independently
checks the new binary, not an unchanged dev.78 qualification replay.

**88 internal /534 public checks pass per actual.NET11/10**: Python regex semantics,
frontend/runtime/import/regex-backed helpers, cancellation forwarding, caller-funded
ownership, cursor admission and cleanup. Loaded producer assembly hashes match the
immutable packages.29 normal project/pin/output files and571 core sources remain
unchanged. Normal optional adapter stays0.4/nonpackable; core remains decoupled.

Reuse the exact previously qualified adapter-local native-cursor/current-token
bridge, with conservative compilation admission and AssumeValid through the working
unfunded compile overload. No new core contract, adapter abstraction, compatibility
switch or memory policy is introduced. Namespace/cursor conversions remain confined
to this bridge. This is development semantic/ownership/cancellation validation,
not final adapter qualification or a backend performance claim.

Still missing for the intended final native route: separate one-match work/result
owners, native UTF8 group-name enumeration, and token-independent cursor creation
with cancellable preparation. No correction to funded AssumeValid forwarding is
visible in the new source; this check exercises the working bridge overload, not
a new funded-overload capability probe. Missing producer APIs defer only affected
final qualification. Producer release qualification and publication remain separate;
Lython development continues independently.

[Qualification/loaded libraries](qualification.json), [package identity](identity.json),
[isolation](isolation.json), [exact bridge/scripts/logs/packages](source-checks.tar.xz).
