#!/usr/bin/bash
git pull 
rsync -a --progress --exclude 'log' ./ theseus@thebeast.local://home/theseus/rescue_maze_2023/vision-code/
rsync -a --progress ../maze-code/ theseus@thebeast.local://home/theseus/rescue_maze_2023/maze-code/
