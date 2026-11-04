"""Offline diagnostic call-chain attribution; no runtime instrumentation."""
import bisect
import collections
import json
from pathlib import Path
import re

root = Path(__file__).resolve().parent
groups = {
    "call_selection": ("::BeginPendingCall(", "::AdvanceCallFrames("),
    "frame_construction": ("::EnterInvocationFrame(", "::CreateExecutableInterpreter(", "ExecutionContext::.ctor(", "ExecutableFrameInterpreter::.ctor("),
    "binding_storage": ("::BindPositionalFunctionArguments(", "::RentFunctionArgumentStorage(", "::ReturnBoundArguments("),
    "native_execution": ("::TryExecuteNativeModuleFunction(", "::EvaluateNativeReturn(", "::GuardNativeReturn("),
    "string_lower_split": ("StringMembers::Lower(", "::InvokeStringSplit(", "::SplitWhitespaceCore(", "::CollectWhitespaceForward("),
    "reservation": ("MemoryGovernor::Reserve(", "MemoryGovernor::Release(", "MemoryGovernor::ReleaseReserved(", "LythonMemoryBudget+Reservation::TryResize("),
}
output = {}
for shape in ("native", "guarded"):
    folder = root / "retrieved" / "perf" / shape
    record = json.loads((folder / "record.json").read_text(encoding="utf-8"))
    tids = set(record["guestTids"])
    ranges = []
    for line in (folder / "perf.map").read_text(encoding="utf-8").splitlines(keepends=True):
        fields = line.rstrip().split(" ", 2)
        if len(fields) != 3 or not line.endswith("\n"):
            continue
        start, size, name = fields
        ranges.append((int(start, 16), int(size, 16), name))
    ranges.sort()
    starts = [r[0] for r in ranges]
    chains = []
    chain = []
    active = False
    for line in (folder / "perf-stacks.txt").read_text(encoding="utf-8").splitlines() + [""]:
        header = re.fullmatch(r"\s*(\d+)/(\d+)\s*", line)
        frame = re.fullmatch(r"\s+([0-9a-f]+)\s+(.*)", line)
        if header:
            active = header[2] in tids
            chain = []
        elif frame and active:
            address = int(frame[1], 16)
            index = bisect.bisect_right(starts, address) - 1
            name = frame[2]
            if index >= 0 and address < ranges[index][0] + ranges[index][1]:
                name = ranges[index][2]
            chain.append(name)
        elif not line.strip() and active and chain:
            chains.append(chain)
            chain = []
    inclusive = collections.Counter(name for chain in chains for name in set(chain))
    output[shape] = {
        "samples": len(chains),
        "overlappingInclusiveGroups": {label: sum(any(any(needle in name for needle in needles) for name in chain) for chain in chains) for label, needles in groups.items()},
        "constructionAndBindingSymbols": {name: inclusive[name] for name in sorted(inclusive) if any(needle in name for label in ("frame_construction", "binding_storage") for needle in groups[label])},
        "topInclusive": sorted(inclusive.items(), key=lambda item: (-item[1], item[0]))[:35],
        "note": "Inlining and incomplete managed/native FP chains limit attribution. Groups overlap; do not sum or infer removable CPU fractions."
    }
(root / "call-chains.json").write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")
for shape, result in output.items():
    print(shape, result["samples"], result["overlappingInclusiveGroups"])
    print(result["constructionAndBindingSymbols"])
