"""Keyboard regressions via a real PTY; Windows ConsoleKeyInfo decoding is unit tested.

Run with RUDYEDIT_TEST_EXE pointing at a Linux build (requires pyte).
Set RUDYEDIT_KEY_PROTOCOL=kitty to send the disambiguated key sequences used
by Ghostty after RudyEdit requests Kitty keyboard flag 1.
"""
import codecs
import json
import fcntl
import os
from pathlib import Path
import pty
import re
import select
import struct
import subprocess
import tempfile
import termios
import time
import pyte

project = Path(__file__).resolve().parents[1]
app = os.environ.get('RUDYEDIT_TEST_EXE', str(project / 'dist/linux-x64/rudyedit'))
checks = 0
protocol = os.environ.get('RUDYEDIT_KEY_PROTOCOL', 'legacy')
assert protocol in ('legacy', 'kitty')


def encode_keys(data):
    if protocol == 'legacy':
        return data

    def encode(match):
        key = match[0]
        if key == '\x1b':
            return '\x1b[27u'
        if len(key) == 1:
            return key if key in '\r\n\t\b' else f'\x1b[{ord(key) + 96};5u'
        if len(key) == 2:
            return f'\x1b[{ord(key[1].lower())};{4 if key[1].isupper() else 3}u'
        if key in ('\x1b[A', '\x1b[B', '\x1b[C', '\x1b[D'):
            return '\x1b[1;1' + key[-1]
        if key in ('\x1bOP', '\x1bOQ', '\x1bOR', '\x1bOS'):
            return '\x1b[13~' if key[-1] == 'R' else '\x1b[' + key[-1]
        return key

    return re.sub(r'\x1b(?:\[[0-9;]*[A-Za-z~]|O[P-S])|[\x01-\x1a]|\x1b[A-Za-z]|\x1b', encode, data)


class Editor:
    def __init__(self):
        self.temp = tempfile.TemporaryDirectory(prefix='rudyedit-keys-')
        self.root = Path(self.temp.name)
        self.master, self.slave = pty.openpty()
        fcntl.ioctl(self.slave, termios.TIOCSWINSZ, struct.pack('HHHH', 36, 120, 0, 0))
        self.original = termios.tcgetattr(self.slave)
        self.raw = bytearray()
        self.screen = pyte.Screen(120, 36)
        self.stream = pyte.Stream(self.screen)
        self.decoder = codecs.getincrementaldecoder("utf-8")("replace")
        env = dict(os.environ, RUDYEDIT_CONFIG_HOME=str(self.root / 'config'),
                   TERM='xterm-ghostty' if protocol == 'kitty' else 'xterm-256color')
        self.process = subprocess.Popen([app], cwd=self.root, stdin=self.slave, stdout=self.slave,
                                        stderr=self.slave, env=env, start_new_session=True)
        self.wait(lambda: 'Text | Code' in self.display, 'startup', 15)
        assert b'\x1b[>1u' in self.raw, 'Kitty keyboard mode was not requested'
        assert not termios.tcgetattr(self.slave)[0] & termios.IXON, 'Ctrl+Q captured by flow control'

    @property
    def display(self):
        return '\n'.join(self.screen.display)

    def drain(self, seconds=.06):
        end = time.monotonic() + seconds
        while time.monotonic() < end:
            if select.select([self.master], [], [], .01)[0]:
                data = os.read(self.master, 262144)
                self.raw.extend(data)
                self.stream.feed(self.decoder.decode(data))

    def send(self, data):
        os.write(self.master, encode_keys(data).encode())
        self.drain()

    def wait(self, predicate, label, timeout=5):
        global checks
        end = time.monotonic() + timeout
        while not predicate() and time.monotonic() < end:
            self.drain()
        passed = bool(predicate())
        checkpoint = os.environ.get('RUDYEDIT_CHECKPOINT_LOG')
        if checkpoint:
            with open(checkpoint, 'a', encoding='utf-8') as log:
                log.write(json.dumps(dict(name=label,status='PASS' if passed else 'FAIL',error='' if passed else self.display))+'\n')
        assert passed, label + '\n' + self.display
        checks += 1

    def contains(self, value):
        self.wait(lambda: value in self.display, value)

    def position(self, row, column):
        self.wait(lambda: f'| {row}:{column} |' in self.screen.display[-2], f'cursor {row}:{column}')

    def escape(self):
        self.send('\x1b')
        self.drain(.12)

    def exit(self):
        self.wait(lambda: self.process.poll() is not None, 'exit')
        self.drain()
        assert self.process.returncode == 0
        assert b'\x1b[?1049l' in self.raw and b'\x1b[?25h' in self.raw
        assert b'\x1b[<u' in self.raw, 'Kitty keyboard mode was not restored'
        assert termios.tcgetattr(self.slave) == self.original, 'terminal settings not restored'

    def close(self):
        if self.process.poll() is None:
            self.process.kill()
            self.process.wait()
        os.close(self.master)
        os.close(self.slave)
        self.temp.cleanup()


def run():
    for vim in (False, True):
        e = Editor()
        try:
            e.send('\x1b[200~abcd\nefgh\x1b[201~')
            if vim:
                e.send('\x1bOQ')
                e.contains('VIM NORMAL')
            e.position(2, 5)
            for key, row, column in [('D', 2, 4), ('A', 1, 4), ('C', 1, 5), ('B', 2, 5)]:
                e.send('\x1b[' + key)
                e.position(row, column)
            if vim:
                e.send('d\x1b[D')  # A pending Vim operator must not swallow navigation.
                e.position(2, 4)
                e.send('i\x1b[D')
                e.contains('VIM INSERT')
                e.position(2, 3)
                e.escape()
                e.send('v\x1b[D')
                e.contains('VIM VISUAL')
                e.position(2, 2)
                e.send('y')
            else:
                e.send('\x1b[1;2D')
                e.position(2, 4)
                e.escape()

            e.send('\x1bOR')
            e.contains('Development |')
            for letter, title in [('f', 'File'), ('e', 'Edit'), ('m', 'Mode'), ('t', 'Text'),
                                  ('v', 'View'), ('b', 'Build'), ('h', 'Help')]:
                e.send('\x1b' + letter)
                e.wait(lambda title=title: f' {title} ' in e.screen.display[2], 'menu ' + title)
            e.escape()
            e.send('\x0b')  # Pending Ctrl+K chord.
            e.send('\x1bf')
            e.wait(lambda: ' File ' in e.screen.display[2], 'menu interrupts chord')
            e.send('\x11')  # Quit must work from a menu.
            e.contains('Unsaved:')
            e.send('\x11\x1bf')  # Repeated quit/menu cannot replace the safety dialog.
            e.contains('Unsaved:')
            e.send('\x1b[B\x1b[B\r')  # Cancel.
            e.wait(lambda: 'Unsaved:' not in e.display, 'cancel quit')
            assert e.process.poll() is None

            # Quit must unwind a synchronous Save As and preserve the edited buffer.
            e.send('\x13')
            e.contains('Save As')
            e.send('\x11')
            e.contains('Unsaved:')
            e.send('\r')
            e.contains('Save As')
            target = e.root / 'saved.txt'
            e.send('\x01' + str(target) + '\r')
            e.exit()
            assert target.read_text() == 'abcd\nefgh'
        finally:
            e.close()

    # Exercise different focus and transient states in both modes. Each is a fresh
    # unsaved document; discard must return cleanly to the same terminal.
    for vim in (False, True):
        for state in ('chord', 'files', 'console', 'command' if vim else 'help', 'save_as'):
            e = Editor()
            try:
                e.send('keep me')
                if vim:
                    e.send('\x1bOQ')
                    e.contains('VIM NORMAL')
                if state == 'chord':
                    e.send('\x0b')
                elif state == 'files':
                    e.send('\x1b[19~')
                elif state == 'console':
                    e.send('\x1b[96;5u')
                elif state == 'command':
                    e.send(':write')
                elif state == 'help':
                    e.send('\x1bOP')
                elif state == 'save_as':
                    e.send('\x13')
                    e.contains('Save As')
                    e.send('\x1be')  # Menu must cancel the suspended Save command.
                    e.wait(lambda: ' Edit ' in e.screen.display[2], 'menu interrupts Save As')
                    e.escape()
                e.send('\x11')
                e.contains('Unsaved:')
                e.send('\x1b[B\r')
                e.exit()
                assert not (e.root / 'Untitled').exists()
            finally:
                e.close()

    print(f'PASS {checks} keyboard/quit checks across 12 terminal sessions ({protocol})')


if __name__ == '__main__':
    run()
