# Native Call assembly launcher failure

R530/B959,2026-11-18 UTC. The frozen current2c6b304b image/custom helper golden
passed local preflight, but the profiler's diagnostic setup accidentally changed
the DOTNET_PerfMapEnabled environment key into a tuple. Python refused the
subprocess environment before the measured worker started. There are no CPU
samples, elapsed results or design conclusions from this failed collection.

Failure, generated launcher/config, machine gates and empty partial traces are
preserved. All users joined, evidence SHA-archived before RAM teardown, no fixture
remains. The corrected collection changes only the accidental key edit and keeps
the original image, catalog and passed golden; no unchanged semantic qualification
is repeated. No runtime source change, hidden mode or extra performance framework.

[Failure/custody](archive-receipt.json),[terminal](terminal-status.json),
[raw](observations.tgz),[procedures](procedures.tar.xz).
