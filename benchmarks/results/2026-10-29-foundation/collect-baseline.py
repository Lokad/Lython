"""Frozen M3 diagnostic matrix; invokes the existing comparison CLI on Linux."""
import fcntl
import json
import os
import pathlib
import subprocess
import sys
import time

home = pathlib.Path.home()
root = home / "lython-bench-checks"
candidate = root / "foundation-bed371f6"
baseline = root / "published-source-aff8965f"
tools = home / "lython-bench-toolchains"
dotnet = tools / "dotnet/11.0.100-rc.3.26522.148/dotnet"
python = tools / "cpython/3.13.16/bin/python3.13"
worker = candidate / "benchmarks/Lokad.Lython.Benchmarks/bin/Release/net11.0/Lokad.Lython.Benchmarks.dll"
old_worker = baseline / "benchmarks/Lokad.Lython.Benchmarks/bin/Release/net10.0/Lokad.Lython.Benchmarks.dll"
output = pathlib.Path(sys.argv[1]).resolve()
names = ["csv-summary-helpers", "text-summary-helpers", "json-summary-helpers",
         "config-edit", "file-manifest-helpers", "file-search"]
jobs = [("compile-run", f"scripts.{name}.{size}", "representative")
        for name in names for size in ["small", "large"]]
jobs += [(lane, f"scripts.{name}.large", "representative")
         for lane in ["warm", "warm-async"] for name in names]
jobs += [("warm", case, "full") for case in
         ["loops.integer.large", "calls.method.large", "generators.drain.large"]]
env = os.environ.copy()
env["DOTNET_ROOT"] = str(dotnet.parent)
env["PATH"] = str(dotnet.parent) + ":" + str(python.parent) + ":" + env["PATH"]
env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
env["DOTNET_NOLOGO"] = "1"

with (root / "comparison-vm.lock").open("a") as lease:
    fcntl.flock(lease, fcntl.LOCK_EX | fcntl.LOCK_NB)
    output.mkdir(parents=True, exist_ok=False)
    record = {"purpose": "fixed diagnostic baseline; no campaign qualification",
              "performanceQualified": False, "wallBudgetSeconds": 300,
              "candidateRevision": subprocess.check_output(
                  ["git", "rev-parse", "HEAD"], cwd=candidate, text=True).strip(),
              "baselineRevision": subprocess.check_output(
                  ["git", "rev-parse", "HEAD"], cwd=baseline, text=True).strip(),
              "plannedJobs": jobs, "jobs": []}

    def save():
        (output / "collection.json").write_text(json.dumps(record, indent=2) + "\n")

    def run(arguments, log, timeout=40):
        unit = "lython-m3-" + str(os.getpid()) + "-" + str(time.monotonic_ns())
        command = ["sudo", "-n", "systemd-run", "--quiet", "--wait", "--pipe", "--collect",
                   "--unit=" + unit, "--uid=lokad", "--working-directory=" + str(candidate),
                   "--property=RuntimeMaxSec=30", "--property=TimeoutStopSec=0",
                   "--property=KillMode=control-group", "--setenv=DOTNET_ROOT=" + str(dotnet.parent),
                   "--setenv=DOTNET_TieredCompilation=0", *arguments]
        with log.open("w") as stream:
            return subprocess.run(command, cwd=candidate, env=env, stdout=stream,
                                  stderr=subprocess.STDOUT, timeout=timeout).returncode

    for profile in ["representative", "full"]:
        subprocess.run([str(dotnet), str(worker), "--compare", "list", "--profile", profile,
                        "--out", str(output / (profile + ".json"))], cwd=candidate, env=env,
                       check=True, stdout=subprocess.DEVNULL, timeout=20)
    gate = run([str(dotnet), str(worker), "--compare", "check-machine", "--out",
                str(output / "machine-before.json")], output / "machine-before.log")
    record["initialGateExit"] = gate
    save()
    if gate:
        raise SystemExit("Initial quiet gate failed; fixed collection not started.")
    started = time.monotonic()
    for index, (lane, case, profile) in enumerate(jobs):
        if time.monotonic() - started >= 270:
            record["stoppingReason"] = "Reserve cleanup within the declared five-minute budget."
            break
        stem = f"{index:02d}-{lane}-{case}"
        receipt = output / (stem + ".json")
        code = run([str(dotnet), str(worker), "--compare", "micro", "--catalog",
                    str(output / (profile + ".json")), "--dotnet", str(dotnet),
                    "--baseline-worker", str(old_worker), "--python", str(python),
                    "--python-worker", str(candidate / "benchmarks/cpython-worker.py"),
                    "--out", str(receipt), "--case", case, "--lane", lane], output / (stem + ".log"))
        row = {"case": case, "lane": lane, "exitCode": code, "receipt": receipt.name}
        if receipt.exists():
            data = json.loads(receipt.read_text())
            row.update({key: data.get(key) for key in ["state", "error", "summaries"]})
        record["jobs"].append(row)
        save()
        print(json.dumps(row), flush=True)
    record["elapsedSeconds"] = time.monotonic() - started
    record["finalGateExit"] = run([str(dotnet), str(worker), "--compare", "check-machine",
                                   "--out", str(output / "machine-after.json")], output / "machine-after.log")
    save()
    print("Fixed diagnostic collection closed; shared lease released.", flush=True)
