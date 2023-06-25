#!/usr/bin/bash
git pull 
rsync -a --progress --exclude 'log' ./ pi@raspberrypi.local://home/pi/rescue_maze_2023/vision-code/
rsync -a --progress ../maze-code/ pi@raspberrypi.local://home/pi/rescue_maze_2023/maze-code/
