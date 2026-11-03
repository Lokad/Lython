# B141 source gate, 2026-11-03

At802fb318, FnMatchModule.MatchSimple's only production call to PyString.GetRunes
materializes both entire strings. Non-ASCII creates and caches one PyString and
backing per scalar; ASCII returns a fresh reference array. Match class/ranges then
call RuneScalarValue on each one-character string, decoding CLR strings again.
The current iterative algorithm already bounds CLR stack and checks cancellation.
GetRunes has no other production callers (two white-box accounting tests and
cancellation prewarming are architecture consumers). Offset tables are separate.

Replace only the intermediate representation with original-subject UTF-8 cursor
positions. Decode scalar values as needed, preserving class grammar and backtracking
control. Result lists/selected input aliases use existing capacity admission; no
matching array/cache remains. No new memory infrastructure or provider/backend.
Compiled literals cease accumulating non-ASCII rune caches through this matcher.

The rejected compiled-literal JSON proposal requires coordinated pool sharing,
codegen roots and compiler admission, rather than a cheap existing UTF8 index.
B138's unique-input algorithmic tradeoff is documented and retained; no new hash
factory/literal cache is added. This is a separate substantial bulk-storage target,
with complete file-list workloads and existing controls required before retention.
