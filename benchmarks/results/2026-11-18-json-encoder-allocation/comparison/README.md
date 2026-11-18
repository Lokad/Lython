# Governed frames for JSON encoding

B974 on 2026-11-18 compared bdab88c54 (7170869d) with the isolated 33025cee
core. Only the core DLL differed between the two verified 32-file images.
The Azure F4as_v7 quiet VM used .NET 11 RC3, ordinary GC and tiering, and the
optional 0.4.0 adapter. Both images matched three complete stdout and file
goldens before timing.

One admitted value-frame array replaces a heap frame per container. Builtin
sequences advance by their live positions, removing their separate iterators.
Pending operations keep a frame index; active traversal continues to reject
reentrant advances. Sorted pairs and dictionary mutation checks keep their
existing semantics. Growth admits replacement overlap and cycle markers.
Completed or failed encoders release discarded frame storage after cleanup.
Small allowances can refuse earlier because the reuse capacity stays charged.

| Complete script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Scaled manifest, warm | −0.61 to +0.94% | −1.00 to 0.00% | 0.38 / 1.16% |
| Scaled manifest, compile-to-result | −1.36 to −1.00% | −1.62 to −1.21% | 0.02 / 0.35% |
| Small manifest, warm | −0.46 to +0.38% | −1.41 to +2.13% | 0.78 / 0.06% |
| Small manifest, compile-to-result | −1.09 to +0.53% | −2.11 to +1.71% | 0.98 / 0.65% |
| Positional calls, warm control | −3.42 to +0.92% | −3.25 to −0.34% | 2.78 / 1.66% |

Ranges cover all pairings of two process medians per revision. Negative
favors the candidate. Each worker used eight seconds of rotated warmup and
five measured rounds. Warm includes mediated RAM I/O, full output checks and
prior-result cleanup. Compile-to-result adds compilation and compiled-owner
disposal. Wider process CPU also includes reset and protocol.

Local allocation fell 4.15% for the scaled manifest and 1.96% for the small
manifest. Both accounted peaks rose by 608 bytes and balances returned to
baseline. The positional control matched in allocation and accounted peak.
Keep the change for simpler lifetimes, lower allocation and the modest
scaled compile-to-result improvement. Warm changes and the positional
elapsed control remain mixed.

Windows passed 217 white-box and 888 public tests per actual .NET target.
Six direct ownership, cancellation and refusal regressions also passed on
each target in WSL. The initial two white-box failures inspected the old
stack field. The corrected tests verify discarded storage and published
chunks across the replacement boundary.

The first launch stopped before sampling because it lacked purpose metadata.
Its journal and initial state are archived. The corrected run used the same
images, inputs and measurement settings. All five jobs completed in 232.73
seconds with both quiet endpoints, worker joins, fixture release and archive
SHA checks. Staging retired one exact older core replica and two archived
observation replicas. Full combined runtime qualification is separate.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations and first failure](observations.tgz) · [Checks and sources](checks-and-procedures.tar.xz)
