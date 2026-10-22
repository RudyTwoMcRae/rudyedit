"""Regenerate Windows ICO and Linux PNG from the SVG master. Needs Inkscape and Pillow."""
from pathlib import Path
import subprocess
import tempfile
from PIL import Image

root = Path(__file__).resolve().parents[1]
assets = root / 'assets'
with tempfile.TemporaryDirectory(prefix='rudyedit-icons-') as directory:
    raster = Path(directory) / 'master.png'
    subprocess.run([
        'inkscape', str(assets / 'rudyedit.svg'), '--export-type=png',
        '--export-filename=' + str(raster), '--export-width=1024',
        '--export-height=1024', '--export-background-opacity=0',
    ], check=True)
    with Image.open(raster) as image:
        rgba = image.convert('RGBA')
        rgba.save(assets / 'rudyedit.ico', format='ICO', bitmap_format='bmp',
                  sizes=[(n, n) for n in (16, 24, 32, 48, 64, 128, 256)])
        rgba.resize((256, 256), Image.Resampling.LANCZOS).save(assets / 'rudyedit.png')
print('Generated assets/rudyedit.ico and assets/rudyedit.png from assets/rudyedit.svg')
