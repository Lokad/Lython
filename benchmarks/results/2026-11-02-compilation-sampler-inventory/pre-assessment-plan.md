# B85 existing-trace CPU inventory, before analysis

Reuse original B80 nettrace/ETLX and its fixed record window; no guest execution,
new capture, retiming or modified trace. Inventory provider/event names first.
If CPU samples exist, attribute only resolved actual compiler stacks and actual
thread identities, preserving unresolved/other-thread data and sampler limits.
Allocation ticks are never CPU samples. This gate selects a coherent remaining
compile-to-result redesign only with demonstrated cost plus source evidence.
If CPU samples are absent/ambiguous, explicitly close this inventory without
inventing attribution or automatically collecting another profile. No core code
or dependencies change. Exact old trace identity remains B80's archive.
