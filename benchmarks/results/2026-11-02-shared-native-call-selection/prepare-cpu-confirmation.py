"""Fill only the missing CSV CPU observation using the same worker protocol."""
import json,pathlib
root=pathlib.Path(__file__).parent
text=(root/'collect-standalone.py').read_text()
text=text.replace("identity = json.loads((root / 'identity.json').read_text())", "identity = json.loads((root / 'identity-cpu.json').read_text())")
text=text.replace("out = root / 'confirmation'", "out = root / 'csv-cpu-confirmation'")
start=text.index('jobs = [(');end=text.index('env = os.environ.copy()',start)
text=text[:start]+"jobs = [('scripts.csv-summary-helpers.large','warm')]*2\n\n"+text[end:]
text=text.replace("if label == 'legacy' else", "if label.startswith('legacy') else")
text=text.replace("if case_id == 'scripts.csv-summary-helpers.large': labels.append('legacy')", "labels += ['legacy','legacy-control']")
text=text.replace('if job_index >= 5:', 'if job_index >= 1:').replace('block=job_index//5','block=job_index')
text=text.replace('targetBatchSeconds=.1','targetBatchSeconds=.2').replace('math.ceil(count*.1/','math.ceil(count*.2/')
text=text.replace('class Worker:', '''cpu_frequency = os.sysconf('SC_CLK_TCK')
def process_cpu_ticks(pid):
    text=Path(f'/proc/{pid}/stat').read_text()
    fields=text[text.rfind(')')+2:].split()
    return int(fields[11])+int(fields[12])

class Worker:''')
text=text.replace('protocol.write_frame(self.process.stdin, request)',
    "before_cpu=process_cpu_ticks(self.process.pid) if operation == 'batch' else 0\n        protocol.write_frame(self.process.stdin, request)")
text=text.replace('response = protocol.read_frame(self.process.stdout)',
    "response = protocol.read_frame(self.process.stdout)\n        if operation == 'batch': self.last_cpu_ticks=process_cpu_ticks(self.process.pid)-before_cpu")
old="samples.append(dict(round=round,engine=workers[i].label,response=workers[i].request('batch',counts[i])))"
new="response=workers[i].request('batch',counts[i])\n                    samples.append(dict(round=round,engine=workers[i].label,response=response,externalProcessCpuTicks=workers[i].last_cpu_ticks,externalCpuFrequency=cpu_frequency))"
assert old in text
text=text.replace(old,new)
text=text.replace("purpose='B116 shared native selector source pair; independent processes and two blocks; legacy CSV diagnostic'",
    "purpose='B116 CSV-only CPU gap confirmation; external TGID CPU through response, runtime/JIT included; sum9 batches, no guest-only or allocation claim'")
(root/'collect-csv-cpu.py').write_text(text,encoding='utf-8')
identity=json.loads((root/'identity.json').read_text())
identity['coreSHA256']['legacy-control']=identity['coreSHA256']['legacy']
identity['cpuMetric']='External Linux/proc TGID user+system ticks immediately before command/after response; includes runtime/JIT/response work, excludes idle intervals between batches. Report weighted9-batch means due clock tick granularity, plus unchanged controls for both baselines.'
(root/'identity-cpu.json').write_text(json.dumps(identity,indent=2),encoding='utf-8')
