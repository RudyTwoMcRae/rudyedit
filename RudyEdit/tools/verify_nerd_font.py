#!/usr/bin/env python3
"""Audit the UI catalog against the shipped TTF, using only Python's stdlib."""
from pathlib import Path
import re
import struct
import argparse
import json

p=argparse.ArgumentParser();p.add_argument('--publish',type=Path);a=p.parse_args()
root = a.publish or Path(__file__).resolve().parents[1]
font = (root / 'assets/fonts/FiraCodeNerdFontMono-Regular.ttf').read_bytes()
def u16(offset): return struct.unpack_from('>H', font, offset)[0]
def u32(offset): return struct.unpack_from('>I', font, offset)[0]
tables = {}
for offset in range(12, 12 + u16(4) * 16, 16):
    name, _, start, length = struct.unpack_from('>4sIII', font, offset)
    tables[name.decode()] = start
cmap = tables['cmap']
subtables = []
for offset in range(cmap + 4, cmap + 4 + u16(cmap + 2) * 8, 8):
    platform, encoding, start = struct.unpack_from('>HHI', font, offset)
    sub = cmap + start
    if platform in (0, 3) and u16(sub) == 4:
        subtables.append(sub)
assert subtables, 'Missing Unicode BMP cmap'
def glyph(code):
    for sub in subtables:
        segments = u16(sub + 6) // 2
        ends = sub + 14
        starts = ends + segments * 2 + 2
        deltas = starts + segments * 2
        ranges = deltas + segments * 2
        for index in range(segments):
            if u16(starts + index * 2) <= code <= u16(ends + index * 2):
                delta = u16(deltas + index * 2)
                ro = ranges + index * 2
                distance = u16(ro)
                value = u16(ro + distance + (code-u16(starts+index*2))*2) if distance else code
                result = (value + delta) & 0xffff if value else 0
                if result: return result
    return 0
metrics = u16(tables['hhea'] + 34)
def width(gid): return u16(tables['hmtx'] + min(gid, metrics-1) * 4)
cell = width(glyph(ord('M')))
icons = [(i['name'],format(i['code'],'x')) for i in json.loads((root/'tests/nerd-icons.json').read_text())] if a.publish else re.findall(r'I\("([^"]+)",0x([0-9a-f]+)', (root/'src/RudyEdit/UiIcons.cs').read_text())
assert icons, 'Empty catalog'
for name, code in icons:
    code = int(code, 16)
    assert 0xe000 <= code <= 0xf8ff, f'{name}: not single-cell BMP private use'
    gid = glyph(code)
    assert gid, f'{name}: missing U+{code:04X}'
    assert width(gid) == cell, f'{name}: non-monospace advance'
print(f'PASS {len(icons)} Nerd Font icons: present, BMP private use, {cell}-unit monospace advance')
