R33 bounded correction. Require the active context through runtime structural/
builtin hashes and native hash members, preserving context-free CLR comparers.
Native str/bytes scans use the existing current-token/chunked UTF8 byte hash;
bytes replaces its whole-buffer AddBytes call with the identical streaming hash
at4096-byte boundaries. Preserve normalized equality/bucket hashes, protocol/
compound ordering, cache caller independence, first memory refusal and cleanup.
No token captured by cached values. No new owner, public option, hash framework,
watchdog, work counter or background abandonment. Tests need native in-flight
interruption rather than VM exit checks, cancelled cold retries/cache behavior,
current caller priority and eventual shared funding reuse. A short affected hash
comparison separates correctness/allocations/diagnostic clocks; no CPU forecast.
Full R30–R32 CI remains independent; batch full correction qualification later.
