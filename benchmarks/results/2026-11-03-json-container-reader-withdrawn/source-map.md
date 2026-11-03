# B136 current-source construction map

Source c31d639d, runtime fd7bc7e4; clean tracked worktree at entry.
- JSON scalar exact reader performs string/literal decoding synchronously, preserves
  numeric frame through a pending callback. Plain UTF-8/string and numeric paths
  already delivered; no universal text bridge assumed.
- ReadJsonObjectAsync has duplicated key/colon/value/comma/end grammar for dictionary
  versus object_pairs_hook. Both await recursively parsed values; object_hook runs
  only after the native dictionary is complete, pairs-hook takes precedence.
- ReadJsonArrayAsync repeats container continuation work, storing result/end and
  awaiting ReadJsonValueAsync recursively even for nested native containers.
- Scalar/string/key decoding and Python-shaped errors are shared and retained.
- Native default options are immutable. A key memo is new parse/cache state; flat
  JSON-lines would pay setup without repeated names across objects. Not selected.
- Call activation already binds into final local arrays, keeps namespaces lazy and
  omits closed native helper contexts. No additional array-copy dominance is proved.
- B134 report JSON inclusive CPU ~30%, unknown leaves22.8%; this supports a bounded
  container-transport redesign, not an exact removable fraction or promised gain.

Select B137: single iterative/resumable container reader, one current value frame,
only a funded parent array for nesting, existing scalar/hook paths. Remove recursive
container readers/duplicate grammar rather than add a default-only parser. Flat
root/scalar documents must introduce no new heap owner/buffer. No new measurement
or broad ownership inventory required for this source gate.
