#!/usr/bin/env python3
"""Refuse packaging unless the exact published payload passed on its target OS."""
import json
from pathlib import Path
import sys
from release_runner import fingerprint,required_suites

def check(folder):
    folder=Path(folder).resolve();path=folder/'tests/results/release-tests.json'
    if not path.is_file():raise RuntimeError(f'Missing native executable test report: {path}. Run tests/release_runner.py --executable <published executable> on its target OS before packaging.')
    data=json.loads(path.read_text(encoding='utf-8'))
    if data.get('schema')!=1 or data.get('passed') is not True:raise RuntimeError('Release tests failed or are blocked; inspect '+str(path.with_suffix('.log')))
    if not data.get('results') or any(r.get('status')!='PASS' for r in data['results']):raise RuntimeError('Incomplete/failing test results')
    expected='win-x64' if (folder/'rudyedit.exe').is_file() else 'linux-x64'
    if data.get('rid')!=expected:raise RuntimeError('Test report target does not match publish directory')
    if set(required_suites(expected))-set(data.get('suites',[])):raise RuntimeError('Missing required executable/terminal suites')
    if not data.get('command_catalog') or set(data['command_catalog'])-set(data.get('command_coverage',[])):raise RuntimeError('Not every command has a passing outcome test')
    digest=fingerprint(folder)
    if digest!=data.get('payload_sha256') or digest!=data.get('final_payload_sha256'):raise RuntimeError('Published files changed after testing; rebuild/retest before creating an installer')
    print('PASS pre-installer gate:',expected,dict(data['summary']))

if __name__=='__main__':
    try:check(sys.argv[1])
    except Exception as error:print('PACKAGING BLOCKED:',error,file=sys.stderr);sys.exit(1)
