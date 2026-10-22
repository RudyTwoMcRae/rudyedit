"""Create a pacman package from the published linux-x64 directory. Requires zstd."""
from pathlib import Path
import io, os, shutil, subprocess, tarfile, time, sys
root=Path(__file__).resolve().parents[2]
version='2.0.6'
release=1
published=root/'dist/linux-x64'
subprocess.run([sys.executable,str(root/'tests/release_gate.py'),str(published)],check=True)
if not (published/'rudyedit').is_file(): raise SystemExit('Publish linux-x64 first.')
output=root/f'dist/rudyedit-{version}-{release}-x86_64.pkg.tar.zst'
launcher=b'#!/bin/sh\nexec /opt/rudyedit/rudyedit "$@"\n'
desktop=b'[Desktop Entry]\nType=Application\nName=RudyEdit\nComment=Terminal text and development editor\nExec=rudyedit %F\nTerminal=true\nIcon=rudyedit\nCategories=Utility;TextEditor;Development;\nMimeType=text/plain;text/markdown;\n'
icon=(root/'assets/rudyedit.svg').read_bytes()
size=sum(p.stat().st_size for p in published.rglob('*') if p.is_file())
metadata=f'''pkgname = rudyedit
pkgbase = rudyedit
pkgver = {version}-{release}
pkgdesc = Terminal Markdown and development editor
builddate = {int(time.time())}
packager = RudyEdit local build
size = {size}
arch = x86_64
license = MIT
license = custom:Microsoft.NET
license = OFL-1.1
license = Unlicense
depend = glibc
depend = gcc-libs
depend = icu
depend = openssl
depend = zlib
depend = fontconfig
optdepend = dotnet-sdk: C# build/run
optdepend = gcc: C build/run
optdepend = python: Python build/run/debug
optdepend = rust: Rust build/run
optdepend = gdb: C and Rust debug
optdepend = xdg-utils: open the configuration folder in the desktop file manager
'''.encode()
cmd=subprocess.Popen(['zstd','-T0','-q','-f','-o',str(output)],stdin=subprocess.PIPE)
with tarfile.open(fileobj=cmd.stdin,mode='w|',format=tarfile.PAX_FORMAT) as archive:
 def add_bytes(name,data,mode=0o644):
  info=tarfile.TarInfo(name);info.size=len(data);info.mode=mode;info.uid=info.gid=0;info.uname=info.gname='root';info.mtime=int(time.time());archive.addfile(info,io.BytesIO(data))
 add_bytes('.PKGINFO',metadata)
 for p in sorted(published.rglob('*')):
  if p.is_file():
   info=archive.gettarinfo(str(p),'opt/rudyedit/'+p.relative_to(published).as_posix());info.uid=info.gid=0;info.uname=info.gname='root'
   with p.open('rb') as f:archive.addfile(info,f)
 add_bytes('usr/bin/rudyedit',launcher,0o755)
 add_bytes('usr/share/applications/rudyedit.desktop',desktop)
 add_bytes('usr/share/icons/hicolor/scalable/apps/rudyedit.svg',icon)
 add_bytes('usr/share/icons/hicolor/256x256/apps/rudyedit.png',(root/'assets/rudyedit.png').read_bytes())
 add_bytes('usr/share/fonts/TTF/RudyEdit-FiraCodeNerdFontMono-Regular.ttf',(root/'assets/fonts/FiraCodeNerdFontMono-Regular.ttf').read_bytes())
 add_bytes('usr/share/licenses/rudyedit/LICENSE',(root/'LICENSE').read_bytes())
 add_bytes('usr/share/licenses/rudyedit/OFL.txt',(root/'assets/fonts/OFL.txt').read_bytes())
 add_bytes('usr/share/licenses/rudyedit/NERD-FONTS-LICENSE.txt',(root/'assets/fonts/NERD-FONTS-LICENSE.txt').read_bytes())
cmd.stdin.close()
if cmd.wait(): raise SystemExit('Compression failed.')
print(output)
