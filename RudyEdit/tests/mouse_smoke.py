"""Exercise actual SGR mouse packets through the terminal event loop."""
import os
import keyboard_smoke as h


def mouse(e, code, x, y, release=False):
    os.write(e.master, f'\x1b[<{code};{x+1};{y+1}{"m" if release else "M"}'.encode())
    e.drain()


def click(e, x, y, shift=False):
    mouse(e, 4 if shift else 0, x, y)
    mouse(e, 4 if shift else 0, x, y, True)


def item(e, text):
    for y, line in enumerate(e.screen.display[3:-2], 3):
        if text in line:
            click(e, line.index(text), y)
            return
    raise AssertionError('Missing menu item ' + text + '\n' + e.display)


for vim in (False, True):
    e = h.Editor()
    try:
        assert b'\x1b[?1002h' in e.raw and b'\x1b[?1006h' in e.raw
        assert '[Alt+letter]' not in e.screen.display[0]
        for name in ('File', 'Edit', 'Mode', 'Text', 'View', 'Help'):
            x = e.screen.display[0].index(name)
            assert e.screen.buffer[0][x].underscore
            assert not e.screen.buffer[0][x+1].underscore
            click(e, x, 0)
            e.wait(lambda name=name: f' {name} ' in e.screen.display[2], 'clicked ' + name)
        # Close popup using the visible mouse target.
        x = e.screen.display[2].index('×')
        click(e, x, 2)
        e.wait(lambda: 'Search:' not in e.display, 'popup mouse close')
        e.send('\x1bOR')
        e.contains('Development |')
        x = e.screen.display[0].index('Build')
        assert e.screen.buffer[0][x].underscore
        click(e, x, 0)
        e.wait(lambda: ' Build ' in e.screen.display[2], 'Build menu click')
        e.escape()
        e.send('\x1bOR')

        # Create a tab by clicking a menu command, then distinguish tab contents.
        e.send('first tab')
        click(e, e.screen.display[0].index('File'), 0)
        item(e, 'New File')
        e.send('second tab')
        e.send('\x1b[1;7D')
        e.contains('first tab')
        e.send('\x1b[1;7C')
        e.contains('second tab')
        labels = e.screen.display[2]
        first = labels.index('Untitled.md')
        second = labels.index('Untitled.md', first+1)
        click(e, first, 2)
        e.contains('first tab')
        click(e, second, 2)
        e.contains('second tab')

        e.send('\x01\x1b[200~alpha beta\n0123456789\nthird line\x1b[201~')
        if vim:
            e.send('\x1bOQ')
            e.contains('VIM NORMAL')
        # Body starts at row 3, text at column 4 with the line-number gutter.
        click(e, 4, 3)
        e.position(1, 1)
        mouse(e, 0, 4, 3)
        mouse(e, 32, 9, 3)
        mouse(e, 0, 9, 3, True)
        e.position(1, 6)
        assert all(e.screen.buffer[3][x].bg == '435e80' for x in range(4, 9))
        e.send('\x03')  # Copy selected "alpha".
        click(e, 4, 5)
        e.send('\x16')
        e.contains('alphathird line')

        # Shift-click extends an existing caret to a complete selection.
        click(e, 4, 4)
        click(e, 7, 4, True)
        e.position(2, 4)
        e.send('\x03')
        click(e, 9, 5)
        e.send('\x16')
        e.contains('alpha012third line')

        # Click across a tab, wide glyph and combining sequence without splitting them.
        e.send('\x0e')
        if vim:
            e.send('i')
        e.send('\x1b[200~a\t你😀éz\x1b[201~')
        if vim:
            e.escape()
        click(e, 10, 3)  # Tab width 5, then the second cell of the CJK glyph.
        e.position(1, 3)
        click(e, 12, 3)  # Second cell of the emoji.
        e.position(1, 4)

        # Wheel scrolling changes the viewport without rewriting/moving the document.
        e.send('\x01\x1b[200~' + '\n'.join(f'line {n:03}' for n in range(80)) + '\x1b[201~')
        e.send('\x1b[1;5H')
        e.position(1, 1)
        mouse(e, 65, 12, 11)
        e.wait(lambda: 'line 003' in e.screen.display[3], 'wheel down')
        e.position(1, 1)
        click(e, 4, 3)
        e.position(4, 1)
        mouse(e, 64, 12, 11)
        e.wait(lambda: 'line 000' in e.screen.display[3], 'wheel up')

        # Click focus within a split and close that pane using its keyboard shortcut.
        e.send('\x1b[118;7u')
        click(e, 4, 4)
        e.position(2, 1)
        click(e, 65, 4)
        e.position(2, 1)
        e.send('\x1b[119;5u')
        e.wait(lambda: e.screen.display[2].count('▸') == 1, 'clicked pane closed')

        # Mouse interaction with the unsaved quit dialog must preserve Cancel.
        e.send('\x11')
        e.contains('Unsaved:')
        item(e, 'Cancel')
        e.wait(lambda: 'Unsaved:' not in e.display, 'quit canceled by mouse')
        assert e.process.poll() is None
        e.send('\x11')
        # Three dirty, unshared tabs remain; discard each using the mouse.
        for _ in range(3):
            e.contains('Unsaved:')
            item(e, 'Discard changes')
        e.exit()
        assert b'\x1b[?1002l' in e.raw and b'\x1b[?1006l' in e.raw
    finally:
        e.close()

print(f'PASS {h.checks} mouse/menu/tab checks ({h.protocol})')
