# Delivery qualification

B25 measured patch is committed as **4c3895c8**. B26 indentation correctness is
committed separately as **a5150449**. B25 raw observations remain unchanged;
no latency claim or new comparison follows the indentation correction.

Full B25 ordinary sweeps pass 3,942 native / 15,312 public on both actual runtimes.
Final B25 EOF/value/provider checks pass 113 each, with 491-source differential
acceptance and seven independently Python-confirmed diagnostic corrections.
B26 passes 798 focused public frontend/value/provider/batch cases per framework,
including its independent CPython success/error goldens and ownership checks.

Explicit Release pack and isolated installed consumers at a5150449 pass on actual
.NET 11 RC3 / .NET 10.0.8. Empty runtime dependency groups, clean consumer assets,
mediated file behavior, documentation/notices and symbols pass. Only the core is
packaged; published 0.10 artifacts/tag stay unchanged. No package is published.

Windows/Ubuntu ordinary/both-target/original-stress/package delivery checkpoint
**41262561230** is queued/running. Earlier B23 checkpoint **41258876567** is terminal
green on both platforms; it is not substituted for qualification of this source.
