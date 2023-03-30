#!/usr/bin/env python3

import cv2
import numpy as np
import time
import math

from camControl import *
#from lineProcessor import *

def processImage(image):
    width, height = image.shape
    print("x,y={},{}".format(width, height))

cam = camControl(showSource=True)
cam.registerCallback(processImage);

while True:

    time.sleep(5)
    print("running")
