# Independent regression coverage

After the prototype comparison, the new fixtures also pass against the exact
previously qualified `8f8628a4` reference binaries: **90 public and 8 internal
checks on each actual .NET11/10**. The public count includes existing counting-loop
cases and the new Unicode, identity, callback-observation and active-cancellation
cases. Internal checks include refusal after cased-word preparation with both
execution strategies and mirrored/private locals.

The test assemblies were compiled during the candidate's focused checks. Isolated
copies of their runtime trees load the verified frozen reference core; the actual
build outputs and production source were not restored or changed. Core hashes,
commands, logs and TRX files preserve that boundary. No new platform/package or
performance qualification is claimed. These are useful observable regressions
independent of whether the word-probing implementation is retained.

The original [prototype decision](../decision.json) remains an immutable record of
the unresolved control-regression checkpoint. This addition does not promote it.
