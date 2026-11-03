import importlib.util,json,os,shutil,statistics,tempfile,time
from pathlib import Path
root=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('worker',root/'cpython-worker.py')
w=importlib.util.module_from_spec(spec);spec.loader.exec_module(w)
assert __import__('sys').flags.isolated and __import__('sys').flags.no_site and __import__('gc').isenabled()
_,cases=w.load_manifest(root/'catalog.json')
record=[]
for case in cases.values():
 folder=Path(tempfile.mkdtemp(prefix='lython-comparison-files-'));original=os.getcwd();os.chdir(folder)
 counters={'reset':0,'checking':0}
 reset,check=w.reset_files,w.read_file_outputs
 def reset_wrapped(case):
  start=time.perf_counter_ns()
  try:return reset(case)
  finally:counters['reset']+=time.perf_counter_ns()-start
 def check_wrapped(case,stage):
  start=time.perf_counter_ns()
  try:return check(case,stage)
  finally:counters['checking']+=time.perf_counter_ns()-start
 w.reset_files,w.read_file_outputs=reset_wrapped,check_wrapped
 try:
  code=w.compile_case(case);until=time.monotonic()+2
  while time.monotonic()<until:w.invoke(code,case)
  samples=[]
  for rnd in range(9):
   counters.update(reset=0,checking=0);cpu=time.process_time_ns();start=time.perf_counter_ns()
   for _ in range(32):w.invoke(code,case)
   total=time.perf_counter_ns()-start;cpu=time.process_time_ns()-cpu
   samples.append(dict(round=rnd,jobs=32,elapsedMilliseconds=total/1e6,cpuMilliseconds=cpu/1e6,
    resetMilliseconds=counters['reset']/1e6,checkingMilliseconds=counters['checking']/1e6,
    remainingMilliseconds=(total-counters['reset']-counters['checking'])/1e6))
  record.append(dict(caseId=case['id'],samples=samples,fullGoldensPassed=True))
 finally:
  w.reset_files,w.read_file_outputs=reset,check;os.chdir(original)
  assert folder.parent==Path(tempfile.gettempdir()) and folder.name.startswith('lython-comparison-files-')
  shutil.rmtree(folder)
(root/'results/cpython-components.json').write_text(json.dumps(dict(observations=record,fixturesRemoved=True,normalGc=True,allGoldensPassed=True),indent=2)+'\n')
