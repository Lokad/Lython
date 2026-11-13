# Overlapping-entry expectation correction

CI43461763537 on976f9439/frozen97bbeff3 passes4684white-box and17533/17534public
cases on each platform. One old test expects a duplicate physical header to be
readable through its last-name lookup. CPython instead selects the overlapping
duplicate and raises BadZipFile. The first explicit info remains readable.
Later.NET10/stress/installed-consumer gates are skipped; full delivery is incomplete.

Correct the test to preserve duplicate listings, first-info access, Python-shaped
rejection of the overlapping name lookup, append rejection and unchanged source
bytes. Do not weaken the runtime's overlap protection. Independent CPython3.13.2
golden and120focused public cases pass per actual.NET11/10. Runtime sources and
the measured97bbimage are unchanged. Original completed job logs, corrected test,
focused checks and exact575source matches are retained. Corrected delivery is pending.
