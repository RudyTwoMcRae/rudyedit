"""End-to-end smoke tests through a real Linux pseudo-terminal (requires pyte)."""
import fcntl, json, os, pty, select, struct, subprocess, tempfile, time
from pathlib import Path
import pyte

project=Path(__file__).resolve().parents[1]
app=Path(os.environ.get('RUDYEDIT_TEST_EXE',str(project/'dist/linux-x64/rudyedit')))
root=Path(tempfile.mkdtemp(prefix='rudyedit-terminal-'))
master,slave=pty.openpty()
fcntl.ioctl(slave,0x5414,struct.pack('HHHH',36,120,0,0))
env=dict(os.environ,RUDYEDIT_CONFIG_HOME=str(root/'config'),TERM='xterm-256color')
p=subprocess.Popen([str(app),'-p',str(root)],stdin=slave,stdout=slave,stderr=slave,env=env,start_new_session=True)
os.close(slave)
screen=pyte.Screen(120,36);stream=pyte.Stream(screen);raw=bytearray()
def drain(seconds=.25):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  if select.select([master],[],[],.025)[0]:
   try: chunk=os.read(master,262144)
   except OSError: break
   raw.extend(chunk);stream.feed(chunk.decode('utf-8','replace'))
  if p.poll() is not None: break
def send(data,seconds=.25):
 os.write(master,data.encode('utf-8'));drain(seconds)
def contains(text):
 deadline=time.monotonic()+5
 while time.monotonic()<deadline and not any(text in line for line in screen.display):drain(.1)
 assert any(text in line for line in screen.display),text+' missing\n'+'\n'.join(screen.display)
def snapshot(name):
 (root/(name+'.txt')).write_text('\n'.join(screen.display))
 try:
  from PIL import Image,ImageDraw,ImageFont
  font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf',16)
  image=Image.new('RGB',(120*10,36*22),'#151c28');draw=ImageDraw.Draw(image)
  for y in range(36):
   for x in range(120):
    cell=screen.buffer[y][x];fg='#'+cell.fg if len(cell.fg)==6 else '#d8dee9';bg='#'+cell.bg if len(cell.bg)==6 else '#151c28'
    draw.rectangle((x*10,y*22,(x+1)*10,(y+1)*22),fill=bg)
    draw.text((x*10,y*22),cell.data,font=font,fill=fg)
  image.save(root/(name+'.png'))
 except ImportError: pass
try:
 deadline=time.monotonic()+10
 while time.monotonic()<deadline and not any('RUDYEDIT' in line for line in screen.display):drain(.15)
 contains('RUDYEDIT');contains('Text | Code')
 drain(.1);idle_size=len(raw);drain(.3);assert len(raw)==idle_size,'Idle editor should not repaint'
 send('\x1b[200~# My notes\n\nHello, Rudy! 😀\x1b[201~');contains('Hello, Rudy!')
 send('\x13');contains('Save As');send('\x01');send(str(root/'notes.md'));send('\r')
 deadline=time.monotonic()+5
 while time.monotonic()<deadline and not (root/'notes.md').exists():drain(.1)
 assert (root/'notes.md').read_text()=='# My notes\n\nHello, Rudy! 😀'
 send('\x1b[118;7u');contains('notes.md') # Ctrl+Alt+V splits the same document.
 send('\x1b[1;3D');send('\x1bOQ');contains('VIM NORMAL')
 send('i\x1b[200~Welcome. \x1b[201~');send('\x1b',.15);send('u');send('\x13')
 assert 'Welcome.' not in (root/'notes.md').read_text()
 send('\x1bOQ');contains('Text | Code')
 send('\x1b[32;2u'); # Shift+Space closes file manager, enhanced keyboard protocol.
 snapshot('editor')
 send('\x1b[1;5Q');contains('Config.cs');send('\x1bOP');contains('Config.cs reference');snapshot('config-help');send('\x1b',.15)
 send('\x1b[1;2Q');contains('KeyboardShortcuts.xml');send('\x1bOP');contains('KEYBINDING XML');send('\x1b',.15)
 send('\x1bOR');contains('Development | Code')
 send('\x1b[21~');contains('RudyEdit menu');send('Build');contains('Build');send('\x1b',.15)
 send('\x11',.5)
 assert p.wait(timeout=5)==0
 assert b'\x1b[?1049l' in raw
 print(json.dumps({'result':'PASS','checks':16,'artifacts':str(root)}))
except BaseException:
 snapshot('failure');print('\n'.join(screen.display));print('Failure artifacts:',root);raise
finally:
 if p.poll() is None:p.kill()
 os.close(master)
