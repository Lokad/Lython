# Preserved setup corrections

The first catalog preparation selected the three-case B565 quiet catalog,
which lacks the CSV control; StopIteration stopped before image staging or timing.
The correction uses the existing two-case R209 CSV/text catalog unchanged.

The first driver launch supplied image directories instead of benchmark assembly
paths. LoadFromAssemblyPath failed before host/fixture creation or observations.
Its original stderr remains in initial-launch-error.txt. The same frozen images,
cases, timing settings and driver then launch with explicit benchmark DLL paths.
No adverse or successful measurement was discarded or restarted.

An added independent CPython window golden included (7,3), which the unchanged
0.4 adapter explicitly refuses. The original test source, CPython output and
failed focused TRX are retained. Existing core cooperating-provider reversed-window
and adapter-capability tests pass. Refined real-adapter goldens omit only that
unsupported pair and keep late, endpoint and original-prefix windows.
