"""Package generated Unity pose frames into shareable animation evidence."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'docs/art/boosters/motion'
NAMES = [('Hammer', 'HAMMER'), ('RocketPurple', 'ROCKET'), ('Vacuum', 'VACUUM')]
font = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 20)
frames = []
for index in range(24):
    sheet = Image.new('RGB', (1152, 424), (26, 33, 48))
    draw = ImageDraw.Draw(sheet)
    for column, (name, label) in enumerate(NAMES):
        with Image.open(OUTPUT / (name + '_' + str(index).zfill(2) + '.png')) as frame:
            sheet.paste(frame.convert('RGB'), (column*384, 0))
        draw.text((column*384+192, 401), label, font=font, anchor='mm', fill=(235,241,249))
    frames.append(sheet)
# 24 sampled poses represent .75 s; the hold separates activations for inspection.
sequence = frames + [frames[-1]]*16
frames[17].save(OUTPUT/'MotionOverview.png')
writer = imageio_ffmpeg.write_frames(str(OUTPUT/'BoosterMotion.mp4'), (1152,424),
    fps=32, codec='libx264', quality=8, macro_block_size=8,
    output_params=['-movflags','+faststart'])
writer.send(None)
for frame in sequence*3:
    writer.send(frame.tobytes())
writer.close()
gif = [frame.quantize(colors=192, method=Image.Quantize.MEDIANCUT) for frame in sequence]
gif[0].save(OUTPUT/'BoosterMotion.gif', save_all=True, append_images=gif[1:],
    duration=31, loop=0, disposal=2, optimize=False)
print('Packaged 3.75-second MP4 and looping GIF from Unity prefab pose samples.')

# Blender study shows baked turning and hose deformation. Unity owns scale.
study = []
for index in range(32):
    frame = Image.new('RGB', (320, 308), (26, 33, 48))
    with Image.open(OUTPUT/('VacuumFrame'+str(index).zfill(2)+'.png')) as source:
        source=source.resize((256,256),Image.Resampling.LANCZOS)
        frame.paste(source, (32, 4), source)
    draw = ImageDraw.Draw(frame)
    draw.text((160, 284), 'TURN + SUCTION / 0.5x', font=font, anchor='mm', fill=(235,241,249))
    study.append(frame)
study_frames = [f.quantize(colors=192,method=Image.Quantize.MEDIANCUT) for f in study]
study_frames[0].save(OUTPUT/'VacuumHoseStudy.gif', save_all=True,
    append_images=study_frames[1:], duration=[200]+[47]*30+[200], loop=0, disposal=2)
sheet=Image.new('RGB',(960,308),(26,33,48))
for column,index in enumerate((0,16,31)):
    sheet.paste(study[index],(column*320,0))
sheet.save(OUTPUT/'VacuumHosePoses.png')
print('Packaged half-speed Blender hose study and rest/bulge/recovery comparison.')
