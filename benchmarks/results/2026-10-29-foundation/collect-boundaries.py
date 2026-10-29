"""Fixed six compilation rows, one startup control, then offline-phase diagnostics."""
import fcntl
import json
import os
import pathlib
import subprocess
import sys
import time

root = pathlib.Path.home() / "lython-bench-checks"
tree = root / "measurement-3386baf8"
dotnet = pathlib.Path.home() / "lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet"
python = pathlib.Path.home() / "lython-bench-toolchains/cpython/3.13.16/bin/python3.13"
worker = tree / "benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0/Lokad.Lython.Benchmarks.dll"
old = root / "published-source-aff8965f/benchmarks/Lokad.Lython.Benchmarks/bin/Release/net10.0/Lokad.Lython.Benchmarks.dll"
catalog = root / "release-0.11-baseline-20261029/representative.json"
output = pathlib.Path(sys.argv[1]).resolve()
env = os.environ.copy()
env["DOTNET_ROOT"] = str(dotnet.parent)
cases = ["scripts." + name + size for name, size in [
    ("csv-summary-helpers", ".large"), ("text-summary-helpers", ".large"),
    ("json-summary-helpers", ".large"), ("config-edit", ".small"),
    ("file-manifest-helpers", ".large"), ("file-search", ".large")]]

with (root / "comparison-vm.lock").open("a") as lock:
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    output.mkdir(exist_ok=False)
    started = time.monotonic()
    def run(arguments, name, executable=worker, supervisor_override=True):
        if time.monotonic() - started >= 90:
            raise TimeoutError("Reserve cleanup within the two-minute diagnostic boundary.")
        command = ["sudo", "-n", "systemd-run", "--quiet", "--wait", "--pipe", "--collect",
            "--unit=lython-boundary-" + name, "--uid=lokad", "--working-directory=" + str(tree),
            "-p", "RuntimeMaxSec=30", "-p", "TimeoutStopSec=0", "-p", "KillMode=control-group",
            "--setenv=DOTNET_ROOT=" + str(dotnet.parent),
            *(["--setenv=DOTNET_TieredCompilation=0"] if supervisor_override else []),
            str(dotnet), str(executable), *arguments]
        with (output / (name + ".log")).open("w") as log:
            result = subprocess.run(command, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=35)
        print(name, "exit", result.returncode, flush=True)
        if result.returncode:
            raise RuntimeError((output / (name + ".log")).read_text())

    run(["--compare", "check-machine", "--out", str(output / "machine-before.json")], "before")
    for index, case in enumerate(cases):
        run(["--compare", "micro", "--catalog", str(catalog), "--dotnet", str(dotnet),
            "--baseline-worker", str(old), "--python", str(python), "--python-worker", str(tree / "benchmarks/cpython-worker.py"),
            "--out", str(output / (case + ".json")), "--case", case, "--lane", "compile"], "compile-" + str(index))
    control = json.loads((root / "release-0.11-baseline-20261029/full.json").read_text())
    control["cases"] = [c for c in control["cases"] if c["id"] == "control.empty.control"]
    (output / "startup-catalog.json").write_text(json.dumps(control, indent=2) + "\n")
    run(["--compare", "smoke-fresh", "--catalog", str(output / "startup-catalog.json"), "--dotnet", str(dotnet),
        "--python", str(python), "--python-worker", str(tree / "benchmarks/cpython-worker.py"),
        "--out", str(output / "startup.json")], "startup", supervisor_override=False)
    run(["--compare", "check-machine", "--out", str(output / "machine-after.json")], "after")
    (output / "collection.json").write_text(json.dumps(dict(performanceQualified=False,
        revision="3386baf8", cases=cases, elapsedSeconds=time.monotonic()-started), indent=2) + "\n")
    # Diagnostics run after timed collection; explicitly remove supervisor tiering override.
    env.pop("DOTNET_TieredCompilation", None)
    diagnostic = root / "foundation-diagnostics/bin/Release/net11.0/Diagnostics.dll"
    with (output / "phases.log").open("w") as log:
        result = subprocess.run([str(dotnet), str(diagnostic), "phases", str(worker.parent / "Lokad.Lython.dll"),
            str(catalog), str(output / "phases.json")], env=env, stdout=log, stderr=subprocess.STDOUT, timeout=30)
    print("phases exit", result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError((output / "phases.log").read_text())
