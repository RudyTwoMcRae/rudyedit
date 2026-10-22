"""Create accurate release notes without modifying the native-tested payload."""
import hashlib
import json
import os
from pathlib import Path

root = Path(__file__).resolve().parents[1]
dist = root/'dist'
data = json.loads((dist/'win-x64/tests/results/release-tests.json').read_text(encoding='utf-8'))
setup, = dist.glob('*-Windows-Setup.exe')
(dist/'SHA256SUMS.txt').write_text(hashlib.sha256(setup.read_bytes()).hexdigest()+'  '+setup.name+'\n')
repo = os.environ['GITHUB_REPOSITORY']
sha = os.environ['GITHUB_SHA']
run = os.environ['GITHUB_RUN_ID']
lines = [f"{data['version']} — Windows x64 Setup", '',
         f"Test result: **{'PASS' if data['passed'] else 'FAIL / NOT RELEASE-READY'}**.", '',
         f"Performed: {data['summary']['performed']}; passed: {data['summary']['passed']}; "
         f"errors: {data['summary']['errors']}; blocked: {data['summary']['blocked']}.", '',
         'The single Setup EXE embeds the internal MSI, runtime, font, exact tested payload and full test reports.',
         'Packaging proceeds regardless of test failures. Failed and blocked results are retained; the Windows test job remains failed.', '',
         f'[Source commit](https://github.com/{repo}/commit/{sha})',
         f'[Native Windows tests and packaging run](https://github.com/{repo}/actions/runs/{run})', '', 'Failures and blocked requirements:']
failures = [row for row in data['results'] if row['status'] != 'PASS']
lines.extend(f"- [{row['status']}] {row['name']}" for row in failures)
if not failures:
    lines.append('None.')
lines.extend(['', 'Download release-tests.log and release-tests.json for all named tests and full error details.',
              'The *.output.txt attachments contain raw suite logs.',
              'Native diagnostics run on Windows Server 2022. Interactive Windows 11 installation, upgrade and uninstall require separate validation.',
              'Setup is unsigned.', ''])
(dist/'release-notes.md').write_text('\n'.join(lines), encoding='utf-8')
