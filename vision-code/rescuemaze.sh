#!/bin/bash
chmod +x /home/theseus/rescue_maze_2023/maze/publish/SerialConsole

cd /home/theseus/rescue_maze_2023/maze-code/publish/
/home/theseus/rescue_maze_2023/maze-code/publish/SerialConsole &

cd /home/theseus/rescue_maze_2023/vision-code/
python3 /home/theseus/rescue_maze_2023/vision-code/control_class.py & 