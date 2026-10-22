"""Text-mode bars exercised through a real PTY and SGR mouse packets."""
import os
import xml.etree.ElementTree as ET
import keyboard_smoke as h

e = h.Editor()
def click(text):
    for y, line in enumerate(e.screen.display):
        if text in line:
            x=line.index(text)
            os.write(e.master, f'\x1b[<0;{x+1};{y+1}M\x1b[<0;{x+1};{y+1}m'.encode())
            e.drain()
            return
    raise AssertionError('Missing '+text+'\n'+e.display)
try:
    e.contains('Add tags:')
    e.send('\x1b[116;6u')  # Ctrl+Shift+T
    e.wait(lambda: 'Add tags:' not in e.display, 'tag bar hidden')
    e.send('\x1b[116;6u')
    e.contains('Add tags:')
    click('comma-separated')
    e.send('work, notes, WORK\r')
    e.contains('Tags: work, notes')
    e.escape()
    click('Tags:')
    e.contains('Tags — comma-separated')
    e.escape()
    click('Size ▾')
    e.contains('Font size')
    click('18 pt')
    e.contains('font-size:18pt')
    e.send('\x1b[116;7u')  # Ctrl+Alt+T
    e.send('project\r')
    e.contains('Tags: work, notes, project')
    e.escape()
    path=e.root / 'tagged.md'
    e.send('\x13')
    e.contains('Save As')
    e.send('\x01'+str(path)+'\r')
    e.contains('Saved ')
    raw=path.read_text()
    assert 'rudyedit-id:' in raw and '<!-- tags:' not in raw
    assert 'work' not in raw and 'notes' not in raw and 'project' not in raw
    assert 'rudyedit-id:' not in e.display
    registry=ET.parse(e.root / 'config' / 'Tags.xml')
    assert [t.text for t in registry.findall('.//Tag')]==['work','notes','project']
    e.send('\x1b[103;7u')  # Ctrl+Alt+G
    e.contains('Tag?')
    e.send('WORK, project\r')
    e.contains('1 files match all tags')
    e.send('\x1b[103;7u')
    e.send('\x01nonexistent\r')
    e.contains('0 files match all tags')
    click('×')
    e.contains('Tag filter cleared.')
    e.send('\x1bOR')
    e.contains('Development |')
    assert 'Add tags:' not in e.display and 'Format ▾' not in e.display
    e.send('\x1bOR')
    e.contains('Add tags:')
    e.send('\x11')
    e.exit()
finally:
    e.close()
print(f'PASS {h.checks} Text-bar PTY checks ({h.protocol})')
