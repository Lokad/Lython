## Next checkpoint — B121 affected report CPython refresh

Run only the materially affected JSON-to-Markdown report at committedc3fa26cc:
fresh compile-run, warm and compile-only versus CPython3.13.16 with an identical
Lython control. Reuse existing workers,4s timed warmup/9x~200ms batches, one quiet
VM lease/owned90s run, full goldens and joined cleanup. Compile-only must accept
null output/file hashes after separate full semantic verification (existing
protocol); do not repeat the old supervisor error. Capture exact source/loaded
bits and preserve adverse/variable controls. Update docs/BENCHMARKS.md's report
rows and named revision, keeping unaffected historical rows labelled accurately.
No broad benchmark campaign or inference of source speedup from independent
medians. Full delivery proceeds independently; no publication.
