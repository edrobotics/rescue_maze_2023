#!/usr/bin/env python3

import cv2
import numpy as np
import time
import math

from camControl import *
#from lineProcessor import *

cam = camControl(showSource=True)

while True:

    time.sleep(5)
    print("running")
