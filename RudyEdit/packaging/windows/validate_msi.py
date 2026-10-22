"""Check authored MSI schemas, references and the font-registration SQL query.

Requires msitools. This checks package structure; it is not a substitute for
Windows Installer ICE validation or a native Windows install/uninstall test.
"""
from pathlib import Path
import os
import shutil
import subprocess
import sys
import tempfile

MSIINFO = os.environ.get('MSIINFO', 'msiinfo')
MSIBUILD = os.environ.get('MSIBUILD', 'msibuild')


def validate(msi):
    msi = Path(msi).resolve()

    def table(name):
        text = subprocess.check_output([MSIINFO, 'export', str(msi), name], text=True)
        lines = text.splitlines()
        columns = lines[0].split('\t')
        types = lines[1].split('\t')
        key = lines[2].split('\t')[1:]
        rows = [dict(zip(columns, row.split('\t'))) for row in lines[3:] if row]
        return columns, types, key, rows

    font_columns, font_types, font_key, fonts = table('Font')
    if font_columns != ['File_', 'FontTitle']:
        raise ValueError(f'Invalid Font columns {font_columns}; Windows Installer requires File_, FontTitle. This can cause error 2235.')
    if font_types != ['s72', 'L128'] or font_key != ['File_']:
        raise ValueError('Invalid Font types or primary key.')

    env_columns, _, env_key, environment = table('Environment')
    if env_columns != ['Environment', 'Name', 'Value', 'Component_'] or env_key != ['Environment']:
        raise ValueError('Invalid Environment schema.')

    icons = {row['Name'] for row in table('Icon')[3]}
    properties = {row['Property']: row['Value'] for row in table('Property')[3]}
    icon_name = 'RudyEditIcon.ico'
    if icon_name not in icons or properties.get('ARPPRODUCTICON') != icon_name:
        raise ValueError('Missing Installed Apps icon registration.')
    shortcuts = table('Shortcut')[3]
    if len(shortcuts) != 2 or any(row['Icon_'] != icon_name or row['IconIndex'] != '0' for row in shortcuts):
        raise ValueError('Start Menu and Desktop shortcuts must use the application icon.')
    icon = subprocess.check_output([MSIINFO, 'extract', str(msi), 'Icon.' + icon_name])
    source_icon = Path(__file__).resolve().parents[2] / 'assets/rudyedit.ico'
    if icon != source_icon.read_bytes():
        raise ValueError('Embedded MSI icon differs from the application ICO.')

    files = {row['File']: row for row in table('File')[3]}
    components = {row['Component']: row for row in table('Component')[3]}
    directories = {row['Directory'] for row in table('Directory')[3]}
    actions = {row['Action'] for row in table('InstallExecuteSequence')[3]}
    for row in fonts:
        if row['File_'] not in files:
            raise ValueError('Font references a missing File row.')
        file = files[row['File_']]
        component = components.get(file['Component_'])
        if not component or component['Directory_'] != 'FontsFolder':
            raise ValueError('Font component must install under FontsFolder.')
        if file['Language']:
            raise ValueError('Font files must not have a language ID.')
        # Microsoft recommends null for TrueType so the actual embedded title is read.
        if file['FileName'].lower().endswith('.ttf') and row['FontTitle']:
            raise ValueError('TrueType font registration should read its embedded name.')

    if not fonts or 'FontsFolder' not in directories:
        raise ValueError('Missing font registration or font directory.')
    if not {'RegisterFonts', 'UnregisterFonts', 'WriteEnvironmentStrings', 'RemoveEnvironmentStrings'} <= actions:
        raise ValueError('Missing font/environment install or uninstall action.')
    for row in environment:
        if row['Component_'] not in components:
            raise ValueError('Environment entry references a missing component.')
        if row['Name'].endswith('PATH') and row['Value'] != '[~];[INSTALLDIR]':
            raise ValueError('PATH entry must append only the application directory.')

    # Exercise the column lookup used by registration, rather than only looking
    # for a font identifier in exported text. Work on a copy: msibuild commits.
    with tempfile.TemporaryDirectory(prefix='rudyedit-msi-query-') as folder:
        copy = Path(folder) / 'query.msi'
        shutil.copyfile(msi, copy)
        queries = [
            'SELECT `File_`, `FontTitle` FROM `Font`',
            'SELECT `Environment`, `Name`, `Value`, `Component_` FROM `Environment`',
        ]
        for query in queries:
            result = subprocess.run([MSIBUILD, str(copy), '-q', query], capture_output=True, text=True)
            if result.returncode:
                raise ValueError(f'MSI query failed: {query}\n{result.stderr}')
    return {'font_rows': len(fonts), 'environment_rows': len(environment), 'files': len(files), 'registration_queries': len(queries), 'icon_rows': len(icons), 'icon_shortcuts': len(shortcuts)}


if __name__ == '__main__':
    if len(sys.argv) != 2:
        raise SystemExit('Usage: python3 validate_msi.py path/to/package.msi')
    try:
        print(validate(sys.argv[1]))
    except (ValueError, subprocess.CalledProcessError) as error:
        raise SystemExit(str(error))
