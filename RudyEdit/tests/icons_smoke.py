"""Nerd Font rendering, icon hit targets and fallback through real PTY input."""
import fcntl
import json
import os
import struct
import termios
from pathlib import Path
import keyboard_smoke as h

def click(e,x,y):
    os.write(e.master,f'\x1b[<0;{x+1};{y+1}M\x1b[<0;{x+1};{y+1}m'.encode())
    e.drain()

def click_text(e,text):
    for y,line in enumerate(e.screen.display):
        if text in line:
            click(e,line.index(text),y)
            return
    raise AssertionError('Missing '+text+'\n'+e.display)

def private_glyphs(e):
    return any(0xe000<=ord(c)<=0xf8ff for c in e.display)

for vim in (False, True):
    e=h.Editor()
    try:
        for folder in ('.git','src','node_modules'):
            (e.root/folder).mkdir()
        (e.root/'src'/'nested.txt').write_text('nested document')
        for name in ('code.cs','data.json','image.png','notes.md','script.py','unknown.xyz'):
            (e.root/name).write_text('contents of '+name)
        if vim:
            e.send('\x1bOQ')
        e.send('\x1b[19~\x06\r') # F8, refresh through an empty filename filter
        e.contains('\ue7b2 code.cs')
        for label in ('\ue73c script.py','\ue80b data.json','\ue60d image.png','\ue73e notes.md','\uf15b unknown.xyz','\ue5fb .git','\ue5fa node_modules','\uf114 src'):
            e.contains(label)
        # Folder icon/name hit targets share the same row; Enter expands it.
        click_text(e,'\uf114 src')
        e.send('\r')
        e.contains('\uf15c nested.txt')
        click_text(e,'\uf062 ..')
        e.send('\r')
        e.contains('\ue73c script.py')
        click_text(e,'\ue73c script.py')
        e.send('\r')
        e.contains('contents of script.py')
        # Icon itself opens its main menu and initials stay underlined.
        for name,icon in (('File','\uf07b'),('Edit','\uf044'),('View','\uf06e'),('Help','\uf059')):
            x=e.screen.display[0].index(name)
            assert e.screen.buffer[0][x].underscore and not e.screen.buffer[0][x-2].underscore
            click(e,x-2,0)
            e.contains(' '+name+' ')
            if name=='File': e.contains('\uf0c7 Save')
            e.escape()
        e.escape()
        e.send('\x01')
        click_text(e,'\uf032') # Bold graphic control preserves current selection.
        e.contains('**contents of script.py**')
        e.send('\x1a')
        e.send('\x1b[19~')
        e.contains('\ue7b2 code.cs')
        if not vim and os.environ.get('RUDYEDIT_ICON_CAPTURE'):
            Path(os.environ['RUDYEDIT_ICON_CAPTURE']).write_text(json.dumps([
                [dict(data=c.data,fg=c.fg,bg=c.bg,bold=c.bold,underscore=c.underscore) for c in [e.screen.buffer[y][x] for x in range(120)]]
                for y in range(36)]))
        e.send('\x1bv')
        e.send('Toggle Graphic Icons\r')
        e.contains('[PY] script.py')
        assert not private_glyphs(e)
        e.send('\x1bh')
        e.send('Nerd Font & Icons\r')
        e.contains('FiraCode Nerd Font Mono')
        e.escape()
        e.send('\x1bv')
        e.send('Toggle Graphic Icons\r')
        e.contains('\ue73c script.py')
        for width in (80,40,20,120):
            e.screen.resize(36,width)
            fcntl.ioctl(e.slave,termios.TIOCSWINSZ,struct.pack('HHHH',36,width,0,0))
            e.drain(.2)
            e.send('\x1b[109;7u') # format focus
            e.contains('Format')
            e.send('\x1b[C\r') # size dropdown through keyboard
            e.contains('Font size')
            e.escape()
            e.send('\x1b[109;7u\x1b[D') # reset focused index for next width
            e.escape()
        e.send('\x11')
        # Undo leaves the document clean; quitting must restore the terminal.
        e.exit()
    finally:
        e.close()
print(f'PASS {h.checks} graphic-icon PTY checks ({h.protocol})')
