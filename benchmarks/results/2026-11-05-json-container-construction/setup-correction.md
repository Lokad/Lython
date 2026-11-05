# Setup correction, before any final-source sample

The first upload/extraction completed, but setup's unchanged 64 MiB free-space
preflight failed. No system unit, started.utc or comparison samples existed.
An initial observation mistakenly interpreted systemctl's default inactive/success
properties for a missing unit as completion; LoadState=not-found and absent output
established the correction. Re-running the non-idempotent setup reported the
already-created baseline symlink. Neither condition is an elapsed measurement.

Only the B235/B236 duplicate uploaded payload.tgz transport files were removed,
after matching their SHA256 values against preserved local payloads:

- B235: 66ec9edc09fc9dac74821e03876bb4b7d2845ecc59e9975db07e2bc257bf553e
- B236: 2004ae0423167bf1c5e3a1989913bd234b7a17e5f1ed388c760809baf3d55dac

Extracted workers, evidence, auxiliary binaries and all prior observations remain.
Free space then passed the original preflight. The same frozen final source and
six-job protocol were launched once under the planned owned 240-second system
unit, invocation ab778f809d6a41b99218345fcf728f4a. No failed samples were discarded
or tuning applied.
