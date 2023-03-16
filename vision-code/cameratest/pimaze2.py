import cv2
import numpy as np
from picamera.array import PiRGBArray
from picamera import PiCamera


def find_colour_victim():
    status = 0
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)

    red_lower_range = np.array([150,200,40])
    red_upper_range = np.array([220,255,255])
    red_mask = cv2.inRange(hsv, red_lower_range, red_upper_range)
    
    if np.sum(red_mask) > 0:
        print("has red") 

    green_lower_range = np.array([30,40,40])
    green_upper_range = np.array([70,255,255])
    green_mask = cv2.inRange(hsv, green_lower_range, green_upper_range)
   # cv2.imshow("green", green_mask)
    if np.sum(green_mask) > 0: 
        print("has green")
    yellow_lower_range = np.array([15,100,100])
    yellow_upper_range = np.array([20,255,255])
    yellow_mask = cv2.inRange(hsv, yellow_lower_range, yellow_upper_range)
    if np.sum(yellow_mask) > 0:
        print("has yellow")
    ##cv2.imshow("yellow", yellow_mask)
    return status


 
camera = PiCamera()
camera.resolution = (640, 480)
camera.framerate = 30

rawCapture = PiRGBArray(camera, size=(640, 480))

for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
    image = frame.array
    find_colour_victim()
  #  cv2.imshow("frame", image)
    rawCapture.truncate(0)
    cv2.waitKey(2)


