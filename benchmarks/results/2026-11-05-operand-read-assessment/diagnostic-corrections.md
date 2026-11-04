# Diagnostic conditions preserved

The private census adapts the existing B132 reflection tool; it compiles five
maintained scripts and does not execute guests. Initial dotnet run omitted -f;
repository multi-target props rejected that launch before compilation. Corrected
to -f net11.0. After a nested jobs directory was added, default compile globs also
included its Program/obj files (CS8802/CS0579). The census now explicitly compiles
only Program.cs. These source-tool corrections did not alter benchmark workers,
timing protocol or guest code. Full console output remains in the tool transcript;
these are the exact failed conditions, not invented performance observations.

The initial white-box run had995pass/one expected-shape failure: constants2→3 and
an eliminated LoadLocal in the old exact instruction sequence. Numeric constants
and independent semantics remain checked. The direct-span shape restores the
original two constants; only the eliminated instruction expectation changes.
Original failed/passing/final/refined logs are archived separately.

The reused derived assess.py purpose string names eight jobs/qualifiedf6 in the
getter-only confirmation, although its actual C# raw purpose correctly identifies
one job versus initial0d67. Loaded hashes, snapshots and observed one job are
authoritative. Original labels remain preserved. That confirmation's old roles
differ2.45%; no claimed getter benefit or CPU attribution follows.
