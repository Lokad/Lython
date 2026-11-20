from pathlib import Path
import datetime,json,shlex,subprocess
r=Path(__file__).parent/'quiet';assert not(r/'launch.log').exists()
ssh=['C:/Windows/System32/OpenSSH/ssh.exe','-i','C:/Users/JoannesVermorel/.ssh/id_lython-bench.pem','-o','BatchMode=yes','-o','ConnectTimeout=15','lokad@20.166.121.73']
code=(r/'remote-launch.py').read_bytes()
bootstrap="import sys; n=int(sys.stdin.buffer.readline()); code=sys.stdin.buffer.read(n); exec(compile(code,'owned-stage','exec'))"
p=subprocess.run(ssh+['python3 -c '+shlex.quote(bootstrap)],input=str(len(code)).encode()+b'\n'+code+(r/'payload.tgz').read_bytes(),capture_output=True,timeout=75)
(r/'launch.log').write_bytes(p.stdout+p.stderr);assert p.returncode==0,(p.stdout+p.stderr).decode()
now=datetime.datetime.now(datetime.timezone.utc)
(r/'observe-not-before.json').write_text(json.dumps(dict(launchedAtUTC=now.isoformat(),doNotObserveBeforeUTC=(now+datetime.timedelta(seconds=250)).isoformat()),indent=2)+'\n',encoding='utf-8',newline='\n')
print(p.stdout.decode());print('B1248 validator and shipment delivery comparison launched; no observation before 250 seconds.')
