# Compiled immutable tuple construction

R351 is under qualification. The[initial01fd prototype](initial/README.md) removes
repeated immutable tuple construction and reduces representative allocations, but
exposes derived-storage admission gaps. The corrected source keeps derived slices/
concatenations under the current execution and removes ZIP's invalid-input copy.
Current-owner refusal/cancellation, actual I/O and retained-result checks pass;
final corrected timing/compilation/platform/package qualification remains pending.
No final speedup, promotion, publication or broader objective completion is claimed.
