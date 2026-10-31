"""Two fixed CSV captures with real session readiness and OS thread CPU deltas."""
import datetime
import fcntl
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

home = pathlib.Path.home()
root = home / "lython-bench-checks"
candidate = root / "architecture-20261031-07c150a6"
baseline = root / "published-source-aff8965f"
dotnet = home / "lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet"
catalog = root / "release-0.11-baseline-20261029/representative.json"
collector = root / "foundation-diagnostics/bin/Release/net11.0/Diagnostics.dll"
output = pathlib.Path(sys.argv[1]).resolve()
perf_mode = len(sys.argv) > 2 and sys.argv[2] == "perf"
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
    for name, case_id, lane in [
        ("csv-warm", "scripts.csv-summary-helpers.large", "warm"),
        ("text-warm", "scripts.text-summary-helpers.large", "warm"),
        ("json-warm", "scripts.json-summary-helpers.large", "warm"),
        ("text-compile", "scripts.text-summary-helpers.large", "compile")]:
        tree, tfm = candidate, "net11.0"
        case = next(c for c in cases if c["id"] == case_id)
        folder = output / name
        folder.mkdir()
        worker = tree / "worker/Lokad.Lython.Benchmarks.dll"
        log = (folder / "worker.stderr").open("w")
        fixture = tempfile.TemporaryDirectory(prefix="lython-comparison-files-")
        process = subprocess.Popen([str(dotnet), str(worker), "--compare", "worker", "--catalog", str(catalog)],
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log, env=env, cwd=fixture.name)
        profiler = None
        profiler_log = None
        request_id = 0
        count = (4096 if perf_mode else 512) if lane == "compile" else (256 if perf_mode else 64)
        record = {"workload": case["id"], "lane": lane, "count": count, "performanceQualified": False,
                  "diagnosticOverrides": {"DOTNET_PerfMapEnabled": "1"} if perf_mode else {},
                  "collector": "perf cpu-clock" if perf_mode else "EventPipe"}
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
                    assert response["actualFilesSha256"] == case["expectedFilesSha256"], response
            return response
        try:
            record["identity"] = frame()
            record["verificationBefore"] = request("verify")
            elapsed = 0.0
            for _ in range(16):
                result = request("batch", 512 if lane == "compile" else 32)
                elapsed += result["elapsedTicks"] / result["clockFrequency"]
                if elapsed >= 1:break
            else:raise RuntimeError("bounded warmup did not finish")
            record["warmupSeconds"] = elapsed
            time.sleep(2)
            profiler_log = (folder / "collector.log").open("w")
            if perf_mode:
                for fifo in [folder / "control", folder / "ack"]:os.mkfifo(fifo)
                control_fd = os.open(folder / "control", os.O_RDWR | os.O_NONBLOCK)
                ack_fd = os.open(folder / "ack", os.O_RDWR | os.O_NONBLOCK)
                profiler = subprocess.Popen(['sudo','-n','perf','record','-e','cpu-clock','-F','199',
                                             '--call-graph','dwarf,16384','--pid',str(process.pid),'--delay=-1',
                                             '--control',f'fifo:{folder}/control,{folder}/ack','-o',str(folder/'perf.data')],
                                            stdout=profiler_log, stderr=subprocess.STDOUT, env=env)
                control("enable")
            else:
                profiler = subprocess.Popen([str(dotnet), str(collector), "collect", str(process.pid),
                                             str(folder / "profile.nettrace"), str(folder / "ready"), str(folder / "stop")],
                                            stdout=profiler_log, stderr=subprocess.STDOUT, env=env)
                deadline = time.monotonic() + 5
                while not (folder / "ready").exists():
                    if profiler.poll() is not None or time.monotonic() >= deadline:raise RuntimeError("collector not ready")
                    time.sleep(.01)
            before = threads(process.pid)
            record["startedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
            record["response"] = request("batch", count)
            record["stoppedUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
            after = threads(process.pid)
            record["threadCpuSeconds"] = [{"tid": key, "name": value["name"],
                                           "seconds": value["cpu"] - before.get(key, {"cpu": 0})["cpu"]}
                                          for key, value in after.items()]
            if perf_mode:
                control("disable")
                control("stop", wait=False)
            else:(folder / "stop").touch()
            profiler.wait(timeout=10)
            assert profiler.returncode == 0
            if perf_mode:
                perf_map = pathlib.Path(f"/tmp/perf-{process.pid}.map")
                if perf_map.exists():shutil.copyfile(perf_map, folder/'perf.map')
                active_tid = max(record['threadCpuSeconds'], key=lambda row:row['seconds'])['tid']
                with (folder/'perf-report.txt').open('w') as report:
                    subprocess.run(['sudo','-n','perf','report','-i',str(folder/'perf.data'),'--stdio',
                                    '--tid',active_tid,'--no-children','--sort','comm,dso,symbol','--percent-limit','1'],
                                   stdout=report,stderr=subprocess.STDOUT,check=True,timeout=15)
            record["verificationAfter"] = request("verify")
            record["closed"] = request("quit")
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
        print(json.dumps({"name": name, "seconds": record["response"]["elapsedTicks"] / record["response"]["clockFrequency"],
                          "threadCpuSeconds": record["threadCpuSeconds"]}), flush=True)
