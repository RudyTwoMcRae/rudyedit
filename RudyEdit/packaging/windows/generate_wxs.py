"""Generate a WiX 3 source accepted by wixl; stable component and upgrade identities."""
from pathlib import Path
import hashlib, uuid
import xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[2]
ns='http://schemas.microsoft.com/wix/2006/wi';E.register_namespace('',ns)
def node(parent,tag,**attrs):return E.SubElement(parent,'{'+ns+'}'+tag,{k:str(v) for k,v in attrs.items()})
def ident(path,prefix='F'):return prefix+hashlib.sha256(path.encode()).hexdigest()[:24]
def guid(path):return str(uuid.uuid5(uuid.UUID('d4984166-4052-47d1-90b0-8f5c320b086c'),path)).upper()
wix=E.Element('{'+ns+'}Wix')
product=node(wix,'Product',Id='*',Name='RudyEdit',Language='1033',Version='2.0.6',Manufacturer='RudyEdit',UpgradeCode='D4984166-4052-47D1-90B0-8F5C320B086C')
node(product,'Package',InstallerVersion='500',Compressed='yes',InstallScope='perMachine')
node(product,'Media',Id='1',Cabinet='rudyedit.cab',EmbedCab='yes')
node(product,'Property',Id='DESKTOP_SHORTCUT',Value='0',Secure='yes')
node(product,'Icon',Id='RudyEditIcon.ico',SourceFile='../../assets/rudyedit.ico')
node(product,'Property',Id='ARPPRODUCTICON',Value='RudyEditIcon.ico')
node(product,'Property',Id='ARPCOMMENTS',Value='Terminal Markdown and development editor')
node(product,'MajorUpgrade',AllowSameVersionUpgrades='yes',DowngradeErrorMessage='A newer version of RudyEdit is already installed.')
target=node(product,'Directory',Id='TARGETDIR',Name='SourceDir');pf=node(target,'Directory',Id='ProgramFiles64Folder');install=node(pf,'Directory',Id='INSTALLDIR',Name='RudyEdit')
programs=node(target,'Directory',Id='ProgramMenuFolder');menu=node(programs,'Directory',Id='ProgramMenuDir',Name='RudyEdit')
desktop=node(target,'Directory',Id='DesktopFolder');fonts=node(target,'Directory',Id='FontsFolder')
feature=node(product,'Feature',Id='Complete',Title='RudyEdit',Level='1')
dirs={'':install};exe_id=None
for file in sorted((root/'dist/win-x64').rglob('*')):
 if not file.is_file():continue
 rel=file.relative_to(root/'dist/win-x64').as_posix();parts=Path(rel).parts[:-1];path=''
 for part in parts:
  parent=path;path=(path+'/'+part).lstrip('/')
  if path not in dirs:dirs[path]=node(dirs[parent],'Directory',Id=ident(path,'D'),Name=part)
 c=node(dirs[path],'Component',Id=ident(rel,'C'),Guid=guid(rel),Win64='yes')
 # Sources are relative to the generated .wxs file, so it is relocatable with the project.
 f=node(c,'File',Id=ident(rel),Source='../../dist/win-x64/'+rel,Name=file.name,KeyPath='yes')
 if rel=='rudyedit.exe':exe_id=ident(rel)
 node(feature,'ComponentRef',Id=ident(rel,'C'))
c=node(install,'Component',Id='PathComponent',Guid=guid('system-path'),Win64='yes')
node(c,'RegistryValue',Root='HKLM',Key='Software\\RudyEdit',Name='Installed',Type='integer',Value='1',KeyPath='yes')
node(feature,'ComponentRef',Id='PathComponent')
c=node(menu,'Component',Id='StartMenuComponent',Guid=guid('start-menu'),Win64='yes')
node(c,'Shortcut',Id='StartMenuShortcut',Name='RudyEdit',Target='[#'+exe_id+']',WorkingDirectory='INSTALLDIR',Icon='RudyEditIcon.ico',IconIndex='0')
node(c,'RemoveFolder',Id='RemoveStartMenu',On='uninstall')
node(c,'RegistryValue',Root='HKLM',Key='Software\\RudyEdit',Name='StartMenu',Type='integer',Value='1',KeyPath='yes');node(feature,'ComponentRef',Id='StartMenuComponent')
c=node(desktop,'Component',Id='DesktopComponent',Guid=guid('desktop'),Win64='yes')
node(c,'Shortcut',Id='DesktopShortcut',Name='RudyEdit',Target='[#'+exe_id+']',WorkingDirectory='INSTALLDIR',Icon='RudyEditIcon.ico',IconIndex='0')
node(c,'RegistryValue',Root='HKLM',Key='Software\\RudyEdit',Name='Desktop',Type='integer',Value='1',KeyPath='yes');node(feature,'ComponentRef',Id='DesktopComponent')
c=node(fonts,'Component',Id='FontComponent',Guid=guid('firacode-font'),Win64='yes')
node(c,'File',Id='FiraCodeFont',Name='RudyEdit-FiraCodeNerdFontMono-Regular.ttf',Source='../../assets/fonts/FiraCodeNerdFontMono-Regular.ttf',KeyPath='yes');node(feature,'ComponentRef',Id='FontComponent')
E.indent(wix);output=root/'packaging/windows/RudyEdit.wxs';E.ElementTree(wix).write(output,encoding='utf-8',xml_declaration=True)
print(output)
