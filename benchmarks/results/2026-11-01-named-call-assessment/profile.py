"""One fixed complete CSV/text positional/named-call attribution; no runtime candidate."""
import datetime
import json
import os
import pathlib
import select
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import signal
signal.signal(signal.SIGTERM, lambda *_: sys.exit(124))

candidate = pathlib.Path(__file__).resolve().parent
root = candidate.parent
task_home = root.parent
dotnet = task_home / "lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet"
catalog = candidate / "catalog.json"
output = pathlib.Path(sys.argv[1]).resolve()
perf_mode = True # This fixed assessment uses only perf; no EventPipe mode.
cases = json.loads(catalog.read_text())["cases"]
env = os.environ.copy()
env["DOTNET_ROOT"] = str(dotnet.parent)
env.pop("DOTNET_TieredCompilation", None)
if perf_mode:env["DOTNET_PerfMapEnabled"] = "1"
ticks = os.sysconf("SC_CLK_TCK")

def threads(pid):
    result = {}
    for task in pathlib.Path(f"/proc/{pid}/task").iterdir():
        try:
            text = (task / "stat").read_text()
            fields = text[text.rfind(")") + 2:].split()
            result[task.name] = {"name": (task / "comm").read_text().strip(),
                                 "cpu": (int(fields[11]) + int(fields[12])) / ticks}
        except OSError:
            pass
    return result

if True: # The parent collection owns the shared VM lease throughout.
    output.mkdir(parents=True, exist_ok=False)
    total_capture_seconds = 0.0
    for name, case_id, lane in [
        ("csv-positional", "scripts.csv-summary-helpers.large", "warm"),
        ("csv-named", "scripts.csv-summary-helpers.large.named", "warm"),
        ("text-positional", "scripts.text-summary-helpers.large", "warm"),
        ("text-named", "scripts.text-summary-helpers.large.named", "warm"),
]:
        tree, tfm = candidate, "net11.0"
        case = next(c for c in cases if c["id"] == case_id)
        folder = output / name
        folder.mkdir()
        worker = candidate / "worker/Lokad.Lython.Benchmarks.dll"
        log = (folder / "worker.stderr").open("w")
        fixture = tempfile.TemporaryDirectory(prefix="lython-comparison-files-")
        process = subprocess.Popen([str(dotnet), str(candidate/"wrapper/bin/Release/net11.0/owner-witness.dll"), "--worker", str(worker), str(catalog), str(folder/"closed-budget.json")],
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, env=env, cwd=fixture.name)
        profiler = None
        profiler_log = None
        request_id = 0
        count = 1
        record = {"workload": case["id"], "lane": lane, "batchJobs": 1, "plannedCaptureSeconds": 1.5, "performanceQualified": False, "completedResponses": [],
                  "diagnosticOverrides": {key:env[key] for key in ["DOTNET_PerfMapEnabled"]},
                  "collector": "perf cpu-clock/fp"}
        control_fd = ack_fd = None
        def control(command, wait=True):
            os.write(control_fd, (command + "\n").encode())
            if wait:
                if not select.select([ack_fd], [], [], 5)[0]:raise TimeoutError("perf control acknowledgment")
                acknowledgment = os.read(ack_fd, 64)
                assert acknowledgment.rstrip(b'\0\r\n') == b'ack', repr(acknowledgment)
        def read(count):
            data = bytearray()
            while len(data) < count:
                if not select.select([process.stdout], [], [], 20)[0]:raise TimeoutError("worker frame")
                part = os.read(process.stdout.fileno(), count - len(data))
                if not part:raise EOFError("worker frame")
                data.extend(part)
            return bytes(data)
        def frame():
            length = struct.unpack(">I", read(4))[0]
            if not 0 < length <= 4 * 1024 * 1024:raise ValueError("frame length")
            return json.loads(read(length))
        def request(operation, count=2):
            global request_id
            request_id += 1
            message = {"protocolVersion": 1, "requestId": request_id, "operation": operation,
                       "caseId": case["id"], "lane": lane, "iterations": count,
                       **{k: case[k] for k in ["sourceSha256", "fixtureSha256", "expectedOutputSha256"]}}
            data = json.dumps(message).encode()
            process.stdin.write(struct.pack(">I", len(data)) + data)
            process.stdin.flush()
            response = frame()
            if operation != "quit":
                assert response["status"] == ("Equivalent" if operation == "verify" else "Completed"), response
                if operation == "batch" and lane == "compile":
                    assert response.get("actualOutputSha256") is None, response
                else:
                    assert response["actualOutputSha256"] == case["expectedOutputSha256"], response
                    if case.get("expectedFilesSha256") is not None:
                        assert response["actualFilesSha256"] == case["expectedFilesSha256"], response
            return response
        try:
            record["identity"] = frame()
            record["verificationBefore"] = request("verify")
            result = request("batch", 128)
            record["warmupSeconds"] = result["elapsedTicks"] / result["clockFrequency"]
            record["warmupJobs"] = 128
            time.sleep(0.5)
            profiler_log = (folder / "collector.log").open("w")
            if perf_mode:
                for fifo in [folder / "control", folder / "ack"]:os.mkfifo(fifo)
                control_fd = os.open(folder / "control", os.O_RDWR | os.O_NONBLOCK)
                ack_fd = os.open(folder / "ack", os.O_RDWR | os.O_NONBLOCK)
                profiler = subprocess.Popen(['sudo','-n','perf','record','-e','cpu-clock','-F','199',
                                             '--call-graph','fp','--pid',str(process.pid),'--delay=-1',
                                             '--control',f'fifo:{folder}/control,{folder}/ack','-o',str(folder/'perf.data')],
                                            stdout=profiler_log, stderr=subprocess.STDOUT, env=env)
                capture_wall_start = time.monotonic()
                control("enable")
            before = threads(process.pid)
            record["startedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
            while time.monotonic() - capture_wall_start < 1.5:
                record["completedResponses"].append(request("batch", 1))
            record["completedJobs"] = len(record["completedResponses"])
            record["stoppedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()

            after = threads(process.pid)
            record["threadCpuSeconds"] = [{"tid": key, "name": value["name"],
                                           "seconds": value["cpu"] - before.get(key, {"cpu": 0})["cpu"]}
                                          for key, value in after.items()]
            if perf_mode:
                control("disable")
                record["captureWallSeconds"] = time.monotonic() - capture_wall_start
                total_capture_seconds += record["captureWallSeconds"]
                if total_capture_seconds > 8: raise TimeoutError("Fixed capture limit exceeded; no recapture.")
                control("stop", wait=False)
            profiler.wait(timeout=10)
            assert profiler.returncode == 0
            if perf_mode:
                perf_map = pathlib.Path(f"/tmp/perf-{process.pid}.map")
                if perf_map.exists():shutil.copyfile(perf_map, folder/'perf.map')
                guest = [row for row in record['threadCpuSeconds'] if row['name'].startswith('Lython executio') and row['seconds'] > 0]
                if len(guest) != 1: raise RuntimeError('Expected one actual active guest thread, not a max-CPU heuristic.')
                active_tid = guest[0]['tid']
                record['guestTid'] = active_tid
            record["verificationAfter"] = request("verify")
            record["closed"] = request("quit")
            process.wait(timeout=3)
            assert process.returncode == 0
            record["closedBudget"] = json.loads((folder/"closed-budget.json").read_text())
            assert record["closedBudget"]["finalReservedBytes"] == 0
        finally:
            (folder / "stop").touch()
            if profiler is not None:
                if profiler.poll() is None:
                    if perf_mode:control("stop", wait=False)
                    try:profiler.wait(timeout=5)
                    except subprocess.TimeoutExpired:profiler.kill();profiler.wait()
            if process.poll() is None:
                try:process.wait(timeout=3)
                except subprocess.TimeoutExpired:process.kill();process.wait()
            record["exitCode"] = process.returncode
            (folder / "record.json").write_text(json.dumps(record, indent=2) + "\n")
            log.close()
            if profiler_log is not None:profiler_log.close()
            if control_fd is not None:os.close(control_fd)
            if ack_fd is not None:os.close(ack_fd)
            fixture.cleanup()
            record["ownedFixtureRemoved"] = not pathlib.Path(fixture.name).exists()
            (folder / "record.json").write_text(json.dumps(record, indent=2) + "\n")
        print(json.dumps({"name": name, "captureWallSeconds": record["captureWallSeconds"], "completedJobs": record["completedJobs"],
                          "threadCpuSeconds": record["threadCpuSeconds"]}), flush=True)
