#!/usr/bin/env python3
"""Refuse packaging unless the exact published payload passed on its target OS."""
import argparse
import json
from pathlib import Path
import sys
from release_runner import fingerprint,required_suites

def check(folder, allow_test_failures=False):
    folder=Path(folder).resolve();path=folder/'tests/results/release-tests.json'
    if not path.is_file():raise RuntimeError(f'Missing native executable test report: {path}. Run tests/release_runner.py --executable <published executable> on its target OS before packaging.')
    data=json.loads(path.read_text(encoding='utf-8'))
    if allow_test_failures:
        return check_completed_windows(folder, data)
    if data.get('schema')!=1 or data.get('passed') is not True:raise RuntimeError('Release tests failed or are blocked; inspect '+str(path.with_suffix('.log')))
    if not data.get('results') or any(r.get('status')!='PASS' for r in data['results']):raise RuntimeError('Incomplete/failing test results')
    expected='win-x64' if (folder/'rudyedit.exe').is_file() else 'linux-x64'
    if data.get('rid')!=expected:raise RuntimeError('Test report target does not match publish directory')
    if set(required_suites(expected))-set(data.get('suites',[])):raise RuntimeError('Missing required executable/terminal suites')
    if not data.get('command_catalog') or set(data['command_catalog'])-set(data.get('command_coverage',[])):raise RuntimeError('Not every command has a passing outcome test')
    digest=fingerprint(folder)
    if digest!=data.get('payload_sha256') or digest!=data.get('final_payload_sha256'):raise RuntimeError('Published files changed after testing; rebuild/retest before creating an installer')
    print('PASS pre-installer gate:',expected,dict(data['summary']))

def check_completed_windows(folder, data):
    """Allow failed Windows tests without accepting untested or changed bytes."""
    if data.get('schema') != 1 or data.get('rid') != 'win-x64' or not (folder/'rudyedit.exe').is_file():
        raise RuntimeError('Failed-test packaging policy applies only to Windows payloads')
    if not data.get('host', '').startswith('Windows-') or not data.get('completed_utc'):
        raise RuntimeError('A completed native Windows test attempt is required')
    rows = data.get('results', [])
    if not rows or any(row.get('status') not in ('PASS', 'FAIL', 'ERROR', 'BLOCKED') for row in rows):
        raise RuntimeError('Incomplete/failing test report structure')
    attempted = {row.get('name') for row in rows if row.get('status') != 'BLOCKED'}
    if required_suites('win-x64') - attempted:
        raise RuntimeError('Missing required native Windows test attempts')
    actual = dict(performed=sum(row['status'] != 'BLOCKED' for row in rows),
                  passed=sum(row['status'] == 'PASS' for row in rows),
                  errors=sum(row['status'] in ('FAIL', 'ERROR') for row in rows),
                  blocked=sum(row['status'] == 'BLOCKED' for row in rows))
    passed = actual['errors'] == 0 and actual['blocked'] == 0
    if data.get('summary') != actual or data.get('passed') is not passed:
        raise RuntimeError('Test totals/readiness disagree with named results')
    digest = fingerprint(folder)
    if digest != data.get('payload_sha256') or digest != data.get('final_payload_sha256'):
        raise RuntimeError('Published files changed after testing; rebuild/retest before creating an installer')
    print(('PASS' if passed else 'WARNING: FAIL / NOT RELEASE-READY') +
          ' - packaging completed Windows test attempt:', actual)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder')
    parser.add_argument('--allow-test-failures', action='store_true',
                        help='Package a completed, unchanged native Windows attempt; preserve its failure status.')
    args=parser.parse_args()
    try:check(args.folder, args.allow_test_failures)
    except Exception as error:print('PACKAGING BLOCKED:',error,file=sys.stderr);sys.exit(1)
