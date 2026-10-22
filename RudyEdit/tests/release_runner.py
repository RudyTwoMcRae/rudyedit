#!/usr/bin/env python3
"""Run diagnostics through a published RudyEdit executable; write a complete release log.

No tests edit user documents/settings: each test uses an isolated temporary folder.
The JSON companion is consumed by the mandatory pre-installer gate.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import tempfile
import time
import traceback

SCHEMA=1
PTY_SCRIPTS=('keyboard_smoke','function_panel_smoke','mouse_smoke','text_bars_smoke','icons_smoke','config_folder_smoke','terminal_smoke')

def fingerprint(folder):
    entries=[]
    for path in sorted(folder.rglob('*'),key=lambda p:p.relative_to(folder).as_posix()):
        relative=path.relative_to(folder)
        if not path.is_file() or relative.parts[:2]==('tests','results') or '__pycache__' in relative.parts:continue
        entries.append((relative.as_posix(),hashlib.sha256(path.read_bytes()).hexdigest()))
    return hashlib.sha256(json.dumps(entries,separators=(',',':')).encode()).hexdigest()

def required_suites(rid):
    common={'cli-version','cli-help','cli-doctor','cli-invalid','catalog','managed','font-audit','gate-self-tests'}
    if rid=='win-x64':return common|{'native-windows','windows-icons'}
    return common|{f'pty-{p}-{s}' for p in ('legacy','kitty') for s in PTY_SCRIPTS}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--executable',type=Path,default=Path(__file__).resolve().parents[1]/('rudyedit.exe' if os.name=='nt' else 'rudyedit'))
    parser.add_argument('--report',type=Path,help='Text log path; JSON is written beside it. Default: tests/results/release-tests.log in the publish folder.')
    a=parser.parse_args();exe=a.executable.resolve();folder=exe.parent;scripts=folder/'tests'
    report=(a.report or folder/'tests/results/release-tests.log').resolve();report.parent.mkdir(parents=True,exist_ok=True)
    report.with_suffix('.json').unlink(missing_ok=True)
    rid='win-x64' if exe.suffix.lower()=='.exe' else 'linux-x64'
    started=datetime.now(timezone.utc).isoformat();rows=[];suites=[];catalog=[];coverage=[];version='unknown';payload='';finish_payload=''
    def add(name,status='PASS',error='',**kw):rows.append(dict(name=name,status=status,error=error,**kw))
    def run(name,cmd,env,timeout=180,success=None):
        begin=time.monotonic()
        try:
            result=subprocess.run([str(x) for x in cmd],capture_output=True,text=True,encoding='utf-8',errors='replace',env=env,cwd=temp,timeout=timeout)
            output=result.stdout+'\n'+result.stderr
            (report.parent/(name+'.output.txt')).write_text(output,encoding='utf-8')
            if result.returncode!=0:raise RuntimeError(f'Exit code {result.returncode}\n{output[-16000:]}')
            if success is not None:success(result)
            add(name,duration_ms=round((time.monotonic()-begin)*1000));suites.append(name)
            return result
        except Exception:
            add(name,'FAIL',traceback.format_exc(),duration_ms=round((time.monotonic()-begin)*1000));return None
    try:
        if not exe.is_file():raise FileNotFoundError(exe)
        payload=fingerprint(folder)
        compatible=(os.name=='nt') == (rid=='win-x64')
        if not compatible:
            add('native-executable-platform','BLOCKED',f'{rid} executable cannot run natively on {platform.system()}. Run these packaged scripts on the target OS. No installer is allowed for this payload yet.')
        else:
            with tempfile.TemporaryDirectory(prefix='rudyedit-release-') as temp:
                env=dict(os.environ,RUDYEDIT_CONFIG_HOME=str(Path(temp)/'config'),PYTHONDONTWRITEBYTECODE='1',PYTHONUTF8='1')
                result=run('cli-version',[exe,'--version'],env,success=lambda r:require(r.stdout.startswith('RudyEdit '),'Missing version'))
                if result:version=result.stdout.strip()
                run('cli-help',[exe,'--help'],env,success=lambda r:require('Usage:' in r.stdout and 'Ctrl+Q' in r.stdout and '--self-test' in r.stdout,'Incomplete CLI help'))
                run('cli-doctor',[exe,'--doctor'],env,success=lambda r:require('Toolchains' in r.stdout and 'Config:' in r.stdout,'Doctor output incomplete'))
                def invalid_check(r):
                    require(r.returncode==1 and 'Unknown argument:' in r.stderr,'Invalid argument was not rejected')
                    log=Path(env['RUDYEDIT_CONFIG_HOME'])/'RudyEdit-errors.log'
                    require(log.is_file() and 'Unknown argument:' in log.read_text(encoding='utf-8'),'Fatal error not logged')
                # The expected failure is itself a passing negative test.
                bad=subprocess.run([str(exe),'--invalid-release-test-option'],capture_output=True,text=True,encoding='utf-8',env=env)
                try:invalid_check(bad);add('cli-invalid');suites.append('cli-invalid')
                except Exception:add('cli-invalid','FAIL',traceback.format_exc())
                result=run('catalog',[exe,'--test-catalog'],env,success=lambda r:require(bool(json.loads(r.stdout)),'Empty catalog'))
                if result:catalog=[c['Id'] for c in json.loads(result.stdout)]
                core=Path(temp)/'managed.json'
                result=run('managed',[exe,'--self-test','--report',core],env,timeout=900)
                if core.is_file():
                    data=json.loads(core.read_text(encoding='utf-8'));coverage=data.get('command_coverage',[])
                    for row in data['results']:rows.append(dict(row,layer='managed/executable'))
                else:add('managed-result-file','FAIL','The executable did not write its managed test results')
                if catalog:
                    missing=sorted(set(catalog)-set(coverage))
                    add('every-command-verified','FAIL' if missing else 'PASS','Commands without a passing outcome test: '+', '.join(missing) if missing else '')
                run('gate-self-tests',[sys.executable,scripts/'test_release_gate.py'],env)
                audit=scripts/'verify_nerd_font.py'
                run('font-audit',[sys.executable,audit,'--publish',folder],env)
                if os.name=='nt':
                    native=Path(temp)/'native.json'
                    try:
                        process=subprocess.Popen([str(exe),'--self-test','--windows-input','--report',str(native)],env=env,creationflags=subprocess.CREATE_NEW_CONSOLE)
                        code=process.wait(timeout=90)
                        require(code==0,f'Native Windows diagnostics exited {code}')
                        data=json.loads(native.read_text(encoding='utf-8'))
                        require(bool(data['results']),'Empty native test report')
                        rows.extend(dict(row,layer='native-windows') for row in data['results']);add('native-windows');suites.append('native-windows')
                    except Exception:
                        if 'process' in locals() and process.poll() is None:process.kill();process.wait()
                        add('native-windows','FAIL',traceback.format_exc())
                    run('windows-icons',[sys.executable,scripts/'verify_icons.py',exe],env)
                else:
                    for protocol in ('legacy','kitty'):
                        for script in PTY_SCRIPTS:
                            name=f'pty-{protocol}-{script}'
                            checkpoints=Path(temp)/(name+'.jsonl')
                            child_env=dict(env,RUDYEDIT_TEST_EXE=str(exe),RUDYEDIT_KEY_PROTOCOL=protocol,RUDYEDIT_CHECKPOINT_LOG=str(checkpoints))
                            run(name,[sys.executable,scripts/(script+'.py')],child_env,timeout=240)
                            if checkpoints.exists():
                                for line in checkpoints.read_text(encoding='utf-8').splitlines():rows.append(dict(json.loads(line),layer=name))
        finish_payload=fingerprint(folder)
        if compatible:add('tested-payload-unchanged','PASS' if finish_payload==payload else 'FAIL','Published files changed during the test run' if finish_payload!=payload else '')
    except Exception:add('release-test-infrastructure','ERROR',traceback.format_exc())
    # Missing suites and platform blocks are never counted as passes.
    missing=sorted(required_suites(rid)-set(suites))
    if missing:add('required-release-suites','BLOCKED','Required suites did not pass: '+', '.join(missing))
    summary={'performed':sum(r['status']!='BLOCKED' for r in rows),'passed':sum(r['status']=='PASS' for r in rows),'errors':sum(r['status'] in ('FAIL','ERROR') for r in rows),'blocked':sum(r['status']=='BLOCKED' for r in rows)}
    ok=summary['errors']==0 and summary['blocked']==0
    data=dict(schema=SCHEMA,started_utc=started,completed_utc=datetime.now(timezone.utc).isoformat(),version=version,rid=rid,host=platform.platform(),executable=exe.name,payload_sha256=payload,final_payload_sha256=finish_payload,suites=suites,command_catalog=catalog,command_coverage=coverage,summary=summary,passed=ok,results=rows)
    report.with_suffix('.json').write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf-8')
    lines=[f"Tests performed: {summary['performed']}",f"Passed: {summary['passed']}",f"Errors: {summary['errors']}",f"Blocked: {summary['blocked']}",f"Result: {'PASS' if ok else 'FAIL / NOT RELEASE-READY'}",'',f'Version: {version}',f'Target: {rid}',f'Host: {data["host"]}',f'Started UTC: {started}',f'Completed UTC: {data["completed_utc"]}',f'Payload SHA-256: {payload}','','ERRORS FOUND']
    failures=[r for r in rows if r['status']!='PASS']
    if not failures:lines.append('None.')
    for row in failures:lines.extend([f'[{row["status"]}] {row["name"]}',row.get('error',''),''])
    lines.extend(['','ALL TESTS PERFORMED (named cases/checkpoints; each may contain multiple assertions)'])
    for index,row in enumerate(rows,1):lines.append(f'{index:04d}. [{row["status"]}] {row.get("layer","release")} / {row["name"]}')
    lines.extend(['','Scope: managed cases execute inside the published app; terminal suites exercise actual terminal I/O.','External LSP/DAP fixtures validate protocol behavior; they do not certify every third-party server/debugger.','Expected negative-test errors use temporary app logs and are not reported as product failures.'])
    report.write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print('\n'.join(lines[:5]));print('Report:',report)
    return 0 if ok else 1

def require(ok,message):
    if not ok:raise AssertionError(message)

if __name__=='__main__':sys.exit(main())
