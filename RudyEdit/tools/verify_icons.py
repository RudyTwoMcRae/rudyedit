"""Verify that Windows PE icon resources match the checked-in ICO, frame by frame.

Usage: python3 tools/verify_icons.py dist/win-x64/rudyedit.exe [setup.exe ...]
Uses only Python's standard library and works on Linux or Windows.
"""
from pathlib import Path
import struct
import sys

root = Path(__file__).resolve().parents[1]


def ico_frames(path):
    data = Path(path).read_bytes()
    reserved, kind, count = struct.unpack_from('<HHH', data)
    assert reserved == 0 and kind == 1 and count > 0, 'Invalid ICO directory'
    frames = {}
    for i in range(count):
        w, h, _, _, _, _, length, offset = struct.unpack_from('<BBBBHHII', data, 6 + i * 16)
        frames[(w or 256, h or 256)] = data[offset:offset + length]
    return frames


def pe_resources(path):
    data = Path(path).read_bytes()
    assert data[:2] == b'MZ', 'Not a Windows executable'
    pe = struct.unpack_from('<I', data, 60)[0]
    assert data[pe:pe + 4] == b'PE\0\0', 'Invalid PE signature'
    count = struct.unpack_from('<H', data, pe + 6)[0]
    size = struct.unpack_from('<H', data, pe + 20)[0]
    optional = pe + 24
    magic = struct.unpack_from('<H', data, optional)[0]
    assert magic in (0x10b, 0x20b), 'Unknown PE format'
    directory = optional + (96 if magic == 0x10b else 112)
    resource_rva, resource_size = struct.unpack_from('<II', data, directory + 16)
    assert resource_rva and resource_size, 'No PE resources'
    sections = []
    for i in range(count):
        section = optional + size + i * 40
        virtual_size, virtual_address, raw_size, raw_pointer = struct.unpack_from('<IIII', data, section + 8)
        sections.append((virtual_address, max(virtual_size, raw_size), raw_pointer))

    def offset(rva):
        for address, length, pointer in sections:
            if address <= rva < address + length:
                return pointer + rva - address
        raise ValueError('Resource RVA is outside the PE sections')

    base = offset(resource_rva)
    result = []

    def walk(relative, trail):
        at = base + relative
        named, ids = struct.unpack_from('<HH', data, at + 12)
        for i in range(named + ids):
            name, target = struct.unpack_from('<II', data, at + 16 + i * 8)
            if name & 0x80000000:
                start = base + (name & 0x7fffffff)
                length = struct.unpack_from('<H', data, start)[0]
                name = data[start + 2:start + 2 + 2 * length].decode('utf-16le')
            if target & 0x80000000:
                walk(target & 0x7fffffff, trail + (name,))
            else:
                rva, length = struct.unpack_from('<II', data, base + target)
                start = offset(rva)
                result.append((trail + (name,), data[start:start + length]))
    walk(0, ())
    return result


def verify(path):
    expected = ico_frames(root / 'assets/rudyedit.ico')
    resources = pe_resources(path)
    icons = {}
    for key, blob in resources:
        if key[0] == 3:
            icons.setdefault(key[1], []).append(blob)
    for key, blob in resources:
        if key[0] != 14:
            continue
        reserved, kind, count = struct.unpack_from('<HHH', blob)
        if reserved != 0 or kind != 1:
            continue
        matches = set()
        for i in range(count):
            w, h, _, _, _, _, length, icon_id = struct.unpack_from('<BBBBHHIH', blob, 6 + i * 14)
            size = (w or 256, h or 256)
            if size in expected and len(expected[size]) == length and expected[size] in icons.get(icon_id, []):
                matches.add(size)
        if matches == set(expected):
            print(f'PASS {Path(path).name}: embedded binder-and-pen icon, {sorted(w for w, _ in matches)} px')
            return
    raise ValueError(f'{path}: expected ICO frames were not embedded as a complete icon group')


if __name__ == '__main__':
    if len(sys.argv) < 2:
        raise SystemExit('Pass at least one Windows PE executable to verify.')
    for path in sys.argv[1:]:
        verify(path)
