"""Real terminal shortcut and error-log integration; the desktop opener is stubbed."""
import os
from pathlib import Path
import subprocess
import tempfile
import keyboard_smoke as h

with tempfile.TemporaryDirectory(prefix='rudyedit-folder-host-') as host:
    folder=Path(host)
    opener=folder/'xdg-open'
    opener.write_text('#!/bin/sh\nprintf "%s" "$1" > "$RUDYEDIT_OPENER_CAPTURE"\nprintf "config-folder-opener-fixture-error\\n" >&2\nexit 3\n')
    opener.chmod(0o755)
    capture=folder/'opened-path.txt'
    original_path=os.environ.get('PATH','')
    original_capture=os.environ.get('RUDYEDIT_OPENER_CAPTURE')
    os.environ['PATH']=str(folder)+os.pathsep+original_path
    os.environ['RUDYEDIT_OPENER_CAPTURE']=str(capture)
    e=h.Editor()
    try:
        e.send('\x1b[99;7u')  # Ctrl+Alt+C
        e.contains('Configuration folder:')
        e.contains('Config.cs')
        e.contains('RudyEdit-errors.log')
        e.wait(lambda:capture.exists() and capture.read_text()==str(e.root/'config'),'folder opener receives exact config path')
        log=e.root/'config'/'RudyEdit-errors.log'
        e.wait(lambda:log.exists() and 'xdg-open exited with code 3' in log.read_text(),'desktop opener failure is logged')
        assert 'config-folder-opener-fixture-error' in log.read_text()
        assert e.process.poll() is None
        e.send('\x1bh')
        e.send('persistent\r')
        e.contains('Error log —')
        e.contains('config-folder-opener-fixture-error')
        e.escape()
        e.send('\x11')
        e.exit()
    finally:
        e.close()
        os.environ['PATH']=original_path
        if original_capture is None:os.environ.pop('RUDYEDIT_OPENER_CAPTURE',None)
        else:os.environ['RUDYEDIT_OPENER_CAPTURE']=original_capture

    config=folder/'fatal-config'
    result=subprocess.run([h.app,'--invalid-option-log-fixture'],env=dict(os.environ,RUDYEDIT_CONFIG_HOME=str(config)),capture_output=True,text=True)
    assert result.returncode==1 and 'Error log:' in result.stderr
    saved=(config/'RudyEdit-errors.log').read_text()
    assert 'Unknown argument: --invalid-option-log-fixture' in saved and 'Application startup or fatal error' in saved

print(f'PASS {h.checks} config-folder/log PTY checks plus fatal-startup logging ({h.protocol})')
