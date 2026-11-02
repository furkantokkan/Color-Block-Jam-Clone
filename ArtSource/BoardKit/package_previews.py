"""Generate the board-kit contact sheet from isolated Unity preview renders."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import re

ROOT=Path(__file__).resolve().parents[2]
FOLDER=ROOT/'docs/art/board-kit'
source=(ROOT/'Assets/ColorBlockJam/Runtime/Board/BlockShapeType.cs').read_text()
names=re.findall(r'^\s+(\w+)\s*=\s*\d+',source,re.MULTILINE)
columns=6
sheet=Image.new('RGB',(columns*240,((len(names)+columns-1)//columns)*266),(23,29,43))
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',15)
draw=ImageDraw.Draw(sheet)
for i,name in enumerate(names):
    path=FOLDER/'shapes'/f'{name}.png'
    if not path.exists(): continue
    with Image.open(path) as image:
        sheet.paste(image.convert('RGB').resize((240,240),Image.Resampling.LANCZOS),((i%columns)*240,(i//columns)*266))
    draw.text(((i%columns)*240+120,(i//columns)*266+251),name,anchor='mm',font=font,fill=(240,243,250))
sheet.save(FOLDER/'ShapeLibrary.png')
print(f'Packaged {len(names)} original-mesh shapes.')

environment=list((FOLDER/'environment').glob('*.png'))
if environment:
    columns=6
    sheet=Image.new('RGB',(columns*240,((len(environment)+columns-1)//columns)*266),(23,29,43))
    draw=ImageDraw.Draw(sheet)
    for i,path in enumerate(sorted(environment)):
        with Image.open(path) as image:
            sheet.paste(image.convert('RGB').resize((240,240),Image.Resampling.LANCZOS),((i%columns)*240,(i//columns)*266))
        draw.text(((i%columns)*240+120,(i//columns)*266+251),path.stem,anchor='mm',font=font,fill=(240,243,250))
    sheet.save(FOLDER/'EnvironmentPalette.png')
    print(f'Packaged {len(environment)} environment/palette previews.')
