from pathlib import Path
import ast,datetime,json,shlex,subprocess,sys
r=Path(__file__).parent;inventoryOnly=sys.argv[1:]==['inventory'];assert inventoryOnly or not(r/'observe-not-before.json').exists()
config=json.loads((r/'config.json').read_text(encoding='utf-8'))
script=(r/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly='+repr(inventoryOnly));ast.parse(script)
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73']
if inventoryOnly:
 done=subprocess.run(ssh+['python3 -'],input=script,text=True,encoding='utf-8',capture_output=True)
 (r/'inventory.stdout').write_text(done.stdout,encoding='utf-8');(r/'inventory.stderr').write_text(done.stderr,encoding='utf-8');assert done.returncode==0,done.stderr
 data=json.loads(done.stdout);(r/'inventory.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in data.items()if k!='proofs'}));sys.exit(0)
assert (r/'inventory.json').exists()
(r/'launch-command.py').write_text(script,encoding='utf-8',newline='\n');stage='/home/lokad/lython-bench-checks/native-inputs-b597-launch.py'
code='from pathlib import Path;import sys;p=Path('+repr(stage)+');assert not p.exists();p.write_bytes(sys.stdin.buffer.read())'
done=subprocess.run(ssh+['python3 -c '+shlex.quote(code)],input=script.encode('utf-8'),capture_output=True);assert done.returncode==0,done.stderr
done=subprocess.run(ssh+['python3 '+stage],input=(r/'payload.tgz').read_bytes(),capture_output=True)
(r/'launch.log').write_bytes(done.stdout+b'\n'+done.stderr);assert done.returncode==0,done.stderr.decode('utf-8')
assert b'ActiveState=active'in done.stdout and b'MainPID=0'not in done.stdout
now=datetime.datetime.now(datetime.timezone.utc);bound=dict(acknowledgedUTC=now.isoformat(),doNotObserveBeforeUTC=(now+datetime.timedelta(seconds=390)).isoformat(),ownerSeconds=330,stopSeconds=30,marginSeconds=30)
(r/'observe-not-before.json').write_text(json.dumps(bound,indent=2)+'\n',encoding='utf-8');print(done.stdout.decode('utf-8'));print(json.dumps(bound))
