"""Regression tests for fail-closed installer gating (Python stdlib only)."""
import hashlib
import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from release_gate import check
from release_runner import fingerprint,required_suites

class GateTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);(self.root/'rudyedit').write_bytes(b'fixture-executable')
        self.report=self.root/'tests/results/release-tests.json';self.report.parent.mkdir(parents=True)
        self.data=dict(schema=1,passed=True,rid='linux-x64',results=[dict(name='fixture',status='PASS')],suites=sorted(required_suites('linux-x64')),command_catalog=['fixture'],command_coverage=['fixture'],summary=dict(performed=1,passed=1,errors=0,blocked=0),payload_sha256=fingerprint(self.root),final_payload_sha256=fingerprint(self.root))
    def write(self):self.report.write_text(json.dumps(self.data))
    def accept(self):
        self.write()
        with contextlib.redirect_stdout(io.StringIO()):check(self.root)
    def test_payload_order_is_portable_between_windows_and_linux(self):
        # Windows Path sorting folds case; POSIX sorting does not. Receipts must
        # use the same explicit relative-name order when a Windows artifact is
        # later packaged on a Linux host.
        (self.root/'B.txt').write_bytes(b'upper');(self.root/'a.txt').write_bytes(b'lower')
        files=[p for p in self.root.rglob('*') if p.is_file() and p.relative_to(self.root).parts[:2]!=('tests','results')]
        entries=sorted([(p.relative_to(self.root).as_posix(),hashlib.sha256(p.read_bytes()).hexdigest()) for p in files],key=lambda x:x[0])
        expected=hashlib.sha256(json.dumps(entries,separators=(',',':')).encode()).hexdigest()
        self.assertEqual(fingerprint(self.root),expected)
    def test_valid_exact_payload_accepted(self):self.accept()
    def test_missing_report_blocks(self):
        with self.assertRaisesRegex(RuntimeError,'Missing'):check(self.root)
    def test_failure_blocks(self):
        self.data['passed']=False
        with self.assertRaisesRegex(RuntimeError,'failed'):self.accept()
    def test_changed_binary_blocks(self):
        (self.root/'rudyedit').write_bytes(b'changed')
        with self.assertRaisesRegex(RuntimeError,'changed'):self.accept()
    def test_changed_script_blocks(self):
        (self.root/'tests/new.py').write_text('changed')
        with self.assertRaisesRegex(RuntimeError,'changed'):self.accept()
    def test_missing_native_suite_blocks(self):
        self.data['suites'].remove('pty-kitty-keyboard_smoke')
        with self.assertRaisesRegex(RuntimeError,'Missing required'):self.accept()
    def test_uncovered_function_blocks(self):
        self.data['command_catalog'].append('new-command')
        with self.assertRaisesRegex(RuntimeError,'every command'):self.accept()
    def test_failed_case_with_success_flag_blocks(self):
        self.data['results'][0]['status']='FAIL'
        with self.assertRaisesRegex(RuntimeError,'failing'):self.accept()
    def test_foreign_platform_receipt_blocks(self):
        self.data['rid']='win-x64'
        with self.assertRaisesRegex(RuntimeError,'target'):self.accept()
    def test_empty_results_blocks(self):
        self.data['results']=[]
        with self.assertRaisesRegex(RuntimeError,'Incomplete'):self.accept()

if __name__=='__main__':unittest.main(verbosity=2)
