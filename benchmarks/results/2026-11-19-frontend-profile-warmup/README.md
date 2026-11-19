# B1066 frontend attribution stopped before sampling

The `12fabffb1` image passed all 32 file checks and the three CPython
goldens. The manifest reached 7.310 seconds in 128 warmup batches of
256 compilations. The collector required eight guest seconds and stopped
before enabling the sampler. The validator and annotated rule did not run.
There is no CPU attribution from this attempt.

Both quiet gates passed. The worker joined after the collector stopped it,
and the failed warmup records and maps were archived and verified under
the VM lease before removing the guest fixtures. The first staging attempt
also stopped before launch at its disk preflight. Both failures are retained.

The measured manifest rate supports a larger compile batch while preserving
the eight-second warmup target and the same source, cases and sampling window.

[Raw observations](observations.tgz) · [Terminal state](terminal-status.json) ·
[Procedures and initial preflight](procedures.tar.xz).
