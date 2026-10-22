"""Copy the test runner and scripts beside a published executable, before testing."""
from pathlib import Path
import argparse
import shutil
import re
import json

root=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('publish',type=Path);a=p.parse_args()
managed=a.publish/'tests/managed';managed.mkdir(parents=True,exist_ok=True)
dll=root/'tests/RudyEdit.Tests/bin/Release/net10.0/RudyEdit.Tests.dll'
if not dll.is_file():raise SystemExit('Build tests/RudyEdit.Tests -c Release first')
shutil.copy2(dll,managed/dll.name)
for source in (root/'tests').glob('*.py'):shutil.copy2(source,a.publish/'tests'/source.name)
for source in list((root/'tests').glob('*.sh'))+list((root/'tests').glob('*.cmd')):shutil.copy2(source,a.publish/'tests'/source.name)
for source in (root/'tests').glob('*.md'):shutil.copy2(source,a.publish/'tests'/source.name)
shutil.copy2(root/'tests/requirements.txt',a.publish/'tests/requirements.txt')
for source in (root/'tests/RudyEdit.Tests').glob('*.cs'):
    dest=a.publish/'tests/source'/source.name;dest.parent.mkdir(exist_ok=True);shutil.copy2(source,dest)
print('Staged executable tests:',a.publish)

for name in ('verify_nerd_font.py','verify_icons.py'):shutil.copy2(root/'tools'/name,a.publish/'tests'/name)
icons=re.findall(r'I\("([^"]+)",0x([0-9a-f]+)',(root/'src/RudyEdit/UiIcons.cs').read_text())
(a.publish/'tests/nerd-icons.json').write_text(json.dumps([dict(name=name,code=int(code,16)) for name,code in icons]),encoding='utf-8')
