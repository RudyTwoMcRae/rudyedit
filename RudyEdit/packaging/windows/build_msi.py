"""Build MSI using wixl/msitools, completing tables not supported by wixl 0.103."""
from pathlib import Path
import os, subprocess, tempfile, sys
root=Path(__file__).resolve().parents[2]
subprocess.run([sys.executable,str(root/'tests/release_gate.py'),str(root/'dist/win-x64'),'--allow-test-failures'],check=True)
wixl=os.environ.get('WIXL','wixl');info=os.environ.get('MSIINFO','msiinfo');build=os.environ.get('MSIBUILD','msibuild')
subprocess.run(['python3',str(root/'packaging/windows/generate_wxs.py')],check=True)
msi=root/'dist/windows-internal/RudyEdit.msi'
msi.parent.mkdir(parents=True,exist_ok=True)
subprocess.run([wixl,'-a','x64','-o',str(msi),str(root/'packaging/windows/RudyEdit.wxs')],check=True)
with tempfile.TemporaryDirectory(prefix='rudyedit-msi-') as folder:
 def export(table):return subprocess.check_output([info,'export',str(msi),table]).decode().replace('\r\n','\n')
 def put(table,text):
  path=Path(folder)/(table+'.idt');path.write_text(text)
  subprocess.run([build,str(msi),'-i',str(path)],check=True)
 put('Environment','Environment\tName\tValue\tComponent_\ns72\tl255\tL0\ts72\nEnvironment\tEnvironment\nRudyEditPath\t=-*PATH\t[~];[INSTALLDIR]\tPathComponent\n')
 # File_ is the standard foreign-key column; using Font here breaks registration.
 # Leave FontTitle empty for TrueType: Windows reads the font's embedded name.
 put('Font','File_\tFontTitle\ns72\tL128\nFont\tFile_\nFiraCodeFont\t\n')
 rows=export('Component').splitlines()
 columns=rows[0].split('\t');condition=columns.index('Condition');attributes=columns.index('Attributes')
 for i in range(3,len(rows)):
  values=rows[i].split('\t')
  if values[0]=='DesktopComponent':
   values[condition]='DESKTOP_SHORTCUT = 1'
   # Transitive component: condition is reevaluated on maintenance.
   values[attributes]=str(int(values[attributes])|64)
   rows[i]='\t'.join(values)
 put('Component','\n'.join(rows)+'\n')
 rows=export('InstallExecuteSequence').splitlines()
 for i in range(3,len(rows)):
  values=rows[i].split('\t')
  if values[0]=='RemoveExistingProducts':
   values[2]='1501'
   rows[i]='\t'.join(values)
 actions={x.split('\t')[0] for x in rows[3:]}
 for action,sequence in [('WriteEnvironmentStrings',5200),('RemoveEnvironmentStrings',3300),('RegisterFonts',5300),('UnregisterFonts',2500)]:
  if action not in actions:rows.append(f'{action}\t\t{sequence}')
 put('InstallExecuteSequence','\n'.join(rows)+'\n')
 # Verify the upgrade, optional Desktop, PATH and font records in the compiled artifact.
 assert 'UpgradeCode' in export('Property')
 assert 'Upgrade' in subprocess.check_output([info,'tables',str(msi)]).decode()
 assert 'DESKTOP_SHORTCUT = 1' in export('Component')
 assert 'RudyEditPath' in export('Environment')
 assert 'FiraCodeFont' in export('Font')
 assert 'RemoveExistingProducts' in export('InstallExecuteSequence')
subprocess.run(['python3',str(root/'packaging/windows/validate_msi.py'),str(msi)],check=True)
print(msi)
