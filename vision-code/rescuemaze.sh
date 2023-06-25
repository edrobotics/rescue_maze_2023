#!/bin/bash
/home/pi/rescue_maze_2023/maze-code/publish/SerialConsole &
sleep 5
python3 /home/pi/rescue_maze_2023/vision-code/control.py & 
