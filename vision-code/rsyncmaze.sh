#!/bin/sh
git pull 
rsync -a --progress --exclude 'log' "main-code"  /Users/lukas/GitHub/rescue_maze_2023/vision-code/ pi@raspberrypi.local://home/pi/rescue_maze_2023/vision-code/
rsync -a --progress --exclude 'log' "main-code"  /Users/lukas/GitHub/rescue_maze_2023/maze-code/ pi@raspberrypi.local://home/pi/rescue_maze_2023/maze-code/
