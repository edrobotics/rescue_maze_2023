import pathlib
import os
import shutil


log = pathlib.Path("./log")
list = []
for file in log.glob(f"log*"):
    print(f"file: {file}")
    list.append(file)
    
print(len(list))
new_dir = f"./log/log{len(list)}"
os.mkdir(new_dir)

src_dir = './log'

for filename in os.listdir(src_dir):
    if filename.endswith('.png'):
        full_filename = os.path.join(src_dir, filename)
        print("Moving file:", full_filename)
        shutil.move(full_filename, new_dir)
