"""F1/F2 encodings and Ctrl+W panel-close regressions, in a real PTY."""
import os
import keyboard_smoke as harness

for vim in (False, True):
    e = harness.Editor()
    try:
        # Send exact wire sequences: omitted defaults were previously untested.
        for f1, f2 in (('\x1b[P', '\x1b[Q'), ('\x1b[1P', '\x1b[1Q'),
                       ('\x1bOP', '\x1bOQ'), ('\x1b[1;1P', '\x1b[1;1Q')):
            for mode in ('VIM NORMAL', 'Text | Code'):
                os.write(e.master, f1.encode())
                e.contains('Context help')
                e.escape()
                os.write(e.master, f2.encode())
                e.contains(mode)

        e.send('left buffer')
        e.send('\x1b[118;7u')  # Ctrl+Alt+V: second pane shares first document.
        e.wait(lambda: e.screen.display[2].count('▸') == 2, 'two panels')
        e.send('\x0e')  # Ctrl+N: independent tab in current panel.
        e.send('right buffer')
        if vim:
            e.send('\x1bOQ')
            e.contains('VIM NORMAL')
        e.send('\x1b[119;5u')
        e.contains('Unsaved:')
        e.send('\x1b[B\x1b[B\r')  # Cancel must retain panel and its text.
        e.wait(lambda: 'Unsaved:' not in e.display, 'cancel panel close')
        e.contains('right buffer')
        assert e.screen.display[2].count('▸') == 2

        e.send('\x1b[119;5u')
        e.contains('Unsaved:')
        if vim:
            e.send('\x1b[B\r')  # Discard.
        else:
            e.send('\r')  # Save.
            e.contains('Save As')
            target = e.root / 'right.txt'
            e.send('\x01' + str(target) + '\r')
            e.wait(target.exists, 'saved panel tab')
            assert target.read_text() == 'right buffer'
        e.wait(lambda: e.screen.display[2].count('▸') == 1 and 'Unsaved:' not in e.display,
               'only current panel closed')
        e.contains('left buffer')
        assert e.process.poll() is None

        # Closing the final panel checks unsaved work and leaves one blank panel.
        e.send('\x1b[119;5u')
        e.contains('Unsaved:')
        e.send('\x1b[B\r')
        e.wait(lambda: 'left buffer' not in e.display and 'Unsaved:' not in e.display,
               'last panel reset')
        assert e.process.poll() is None
        assert e.screen.display[2].count('▸') == 1
        e.send('\x11')
        e.exit()
    finally:
        e.close()

print(f'PASS {harness.checks} function-key/panel-close checks ({harness.protocol})')
