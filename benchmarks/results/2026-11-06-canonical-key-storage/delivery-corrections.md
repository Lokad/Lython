# Full-suite corrections after B308

Full internal .NET11:4192pass/15fail. Thirteen cases required old set per-numeric
+64 bookkeeping despite already funded capacity; update their scalar-kind/ownership
assertions to the new documented capacity contract. One growth-denial fixture filled
10 of16 physical slots and expected the next scalar charge to fail; fill the actual
16slots and apply genuine root pressure before testing replacement denial. Preserve
unchanged membership/aliases/first-failure and owner balances.

DecimalSignalValidationTests exposed a real transient-allocation regression, not an
invalid contract: QuerySpan fallback constructed a new immutable LythonSourceSpan on
every unspanned dictionary query. Replace that repeated allocation with one existing-
role immutable storage diagnostic sentinel shared by dict/set. Preserve real current
spans and first structured refusal. No cache, public option, new owner or token state.
The original allocation threshold remains unchanged. B308 timings predate this fix;
retain source identities and do not imply they measure later HEAD.

Broad public suite is still running. Do not rebuild while its current handle is live.
Re-run focused affected cases, then finish full actual11/10 at this coherent boundary.
The representative CPython refresh will use the final corrected source; no third
micro comparison solely for desired signs.
