from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b596';q.mkdir();old=r.parent/'current-value-cost-r246/b591'
sha=lambda data:hashlib.sha256(data).hexdigest()
name='word-control-profile-b596-20261111';unit='lython-b596-word-control-profile-20261111'
def replace(s):return s.replace('current-value-profile-b591-20261111',name).replace('lython-b591-current-value-profile-20261111',unit).replace('current-value-profile-b591-launch.py','word-control-profile-b596-launch.py').replace('lython-b591-fixtures','lython-b596-fixtures').replace('B591','B596')
identity=json.loads((r/'b595/identity.json').read_text(encoding='utf-8'))
identity['sourceRevision']='R250 control correction: two6s diagnostic CPU windows, reference and candidate async keyword-default search, after B595278.387s confirms searchAsync elapsed+6.97..+11.60%/CPU+9.32..+17.04%. Matching Release/SDK/target and common structural IL; no JIT-cause or timing claim. Exact source/core pair from364d9852; runtime prototype uncommitted.'
identity['catalogSHA256']=sha((r/'catalog.json').read_bytes())
(q/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'catalog.json',q/'catalog.json');shutil.copyfile(r/'b595/source-identity.json',q/'source-identity.json')
for filename in ['ops-profile.py','analyze-cpu-guest.py','run.sh','launch.py','retrieve-original.py','remote-launch.py']:
 s=replace((old/filename).read_text(encoding='utf-8'))
 if filename=='ops-profile.py':
  s=s.replace('for name, case_id, lane in [("text-warm", "scripts.text-summary-helpers.nonlocal-counter.large", "warm"), ("report-async", "scripts.report-render.large", "warm-async")]:','for name, case_id, lane in [("reference", "scripts.file-search.large", "warm-async"), ("candidate", "scripts.file-search.large", "warm-async")]:')
  s=s.replace('tree / "candidate/Lokad.Lython.Benchmarks.dll"','tree / name / "Lokad.Lython.Benchmarks.dll"').replace("expected['coreSHA256']['candidate']","expected['coreSHA256'][name]")
  s=s.replace('1024 if "search" in name else 64','1024 if "search" in case_id else 64')
  s=s.replace('TryExecuteDictionaryLoop EvaluateNativeReturn GuardNativeReturn TryReadRecordValue','TryExecuteInstruction TryExecuteCallInstruction TryExecuteWithoutFrame EvaluateNativeReturn GuardNativeReturn')
 if filename=='run.sh':
  s=s.replace('"$root/profiles"','"$root/guest-fixtures/profiles"').replace('text-warm report-async','reference candidate').replace('"$root/profiles/$case"','"$root/guest-fixtures/profiles/$case"').replace('"$root/profiles/$case/perf.data"','"$root/guest-fixtures/profiles/$case/perf.data"')
  a=s.index("python3 - <<'PY'");b=s.index('python3 "$root/archive.py"',a)
  s=s[:a]+s[b:]
 if filename=='remote-launch.py':
  s=s.replace("source=base/'dictionary-checkpoint-b590-20261111/candidate'","source=base/'word-materialization-b595-20261111/reference'")
  a=s.index('protected=');b=s.index("with(base/'comparison-vm.lock')",a)
  s=s[:a]+"protected={base/'word-materialization-b595-20261111'/role/'Lokad.Lython.dll':config['identity']['coreSHA256'][role]for role in ['reference','candidate']}\n"+s[b:]
  a=s.index(" os.link(source/'Lokad.Lython.dll'");b=s.index(' shutil.copyfile(protocol',a)
  s=s[:a]+" for role in ['reference','candidate']:\n  shutil.copytree(base/'word-materialization-b595-20261111'/role,root/role,copy_function=os.link)\n  assert digest(root/role/'Lokad.Lython.dll')==config['identity']['coreSHA256'][role]\n os.link(root/'candidate/Lokad.Lython.dll',root/'current-core.dll')\n"+s[b:]
 (q/filename).write_text(s,encoding='utf-8',newline='\n')
 if filename.endswith('.py')and filename!='remote-launch.py':ast.parse(s)
assert 'tree / name /'in(q/'ops-profile.py').read_text(encoding='utf-8')
assert 'guest-fixtures/profiles'in(q/'run.sh').read_text(encoding='utf-8')
print('Prepared paired current-image CPU scripts; custom under-lease RAM archive must be supplied before staging.')
