import cv2
import numpy as np
from picamera.array import PiRGBArray
from picamera import PiCamera
import socket
import time
import random

HEADER = 16
PORT = 4242
SERVER = socket.gethostbyname(socket.gethostname())
ADDR = (SERVER, PORT)
FORMAT = 'utf-8'
DISCONNECT_MESSAGE = "!DISCONNECT"

def sendMessage(msg):
    message = msg.encode(FORMAT)
    msg_length = len(message).to_bytes(HEADER, "big")
    client.send(msg_length)
    client.send(message)


client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
client.connect(ADDR)

def identify_victim(victim): 
#    victim = np.invert(victim)
#    cv2.imshow("victim", victim)
    pixels = np.count_nonzero(victim)
    print("pixels: ", pixels)
    imgContour, contours, hierarchy = cv2.findContours(victim,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    for cnt in contours:
        
        area = cv2.contourArea(cnt)
        para = cv2.arcLength(cnt,True)
        if para == 0:
            break
        approx = cv2.approxPolyDP(cnt, 0.003 * cv2.arcLength(cnt, True), True)
        apr = area/para
        print("apr: ",str(apr))
   #     print("area: ", str(area))
  #      print("para: ", str(para))
        print("approx: ", len(approx))
        if apr > 11 and apr < 15 and len(approx) > 12 and len(approx) < 17:
            print("H detected")
            sendMessage("K3")
        if apr > 9 and apr < 13 and len(approx) > 32 and len(approx) < 40:
            print("S detected")
            sendMessage("K2")
        if apr > 10 and apr < 13 and len(approx) > 17 and len(approx) < 23:
            print("U detected")
            sendMessage("K0")


def find_visual_victim():
    img = image.copy
    gray = cv2.cvtColor(image,cv2.COLOR_BGR2GRAY)
    ret,binary = cv2.threshold(gray,70,255,0, cv2.THRESH_BINARY)

    binary = np.invert(binary)
#    cv2.imshow("binary", binary)
#    binary[:, 300:360] = (0)
#    cv2.imshow("binary2", binary)
    imgContour, contours, hierarchy = cv2.findContours(binary,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    cv2.drawContours(image, contours, -1, (0, 255, 0), 3)

    for cnt in contours:
        area = cv2.contourArea(cnt)
        
        if area>10:
            rect = cv2.minAreaRect(cnt)
            box = cv2.boxPoints(rect)
            box = np.int0(box)
            cv2.drawContours(image, [box], 0, (0, 0, 255), 3)
            para = cv2.arcLength(cnt,True)
            approx = cv2.approxPolyDP(cnt, 0.009 * cv2.arcLength(cnt, True), True)
            n = approx.ravel() 
            i = 0
            minx = 640
            maxx = 0
            miny = 480
            maxy = 0
            if len(approx) < 5:
  #              print("break len: ", str(len(approx)))
                break
            
            while i < len(n):     
                if(i % 2 == 0):
                    x = n[i]
                    y = n[i + 1]
#                    print(x, y)
                    if x > maxx:
                        maxx = x
                    if x < minx:
                        minx = x
                    if y > maxy:
                        maxy = y
                    if y < miny: 
                        miny = y
                i = i+1
            imgCnt = binary[miny:maxy, minx:maxx]
            width = maxx - minx
            height = maxy - miny 
#            print(width, height)

            if width < 10 or height < 10:
 #               print("to small area")
                break
            h, v = imgCnt.shape
            dsize = (200,200)
            RImgCnt = cv2.resize(imgCnt, dsize)  
            identify_victim(RImgCnt)
            
def find_colour_victim():
    status = 0
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)

    red_lower_range = np.array([150,200,40])
    red_upper_range = np.array([220,255,255])
    red_mask = cv2.inRange(hsv, red_lower_range, red_upper_range)
    if np.count_nonzero(red_mask) > 0:
        print("has red") 
        print(np.count_nonzero(red_mask))
        sendMessage("K1")
    green_lower_range = np.array([30,40,40])
    green_upper_range = np.array([70,255,255])
    green_mask = cv2.inRange(hsv, green_lower_range, green_upper_range)
   # cv2.imshow("green", green_mask)
    if np.count_nonzero(green_mask) > 0: 
        print(np.count_nonzero(green_mask))
        print("has green")
        sendMessage("K0")
    yellow_lower_range = np.array([15,100,100])
    yellow_upper_range = np.array([20,255,255])
    yellow_mask = cv2.inRange(hsv, yellow_lower_range, yellow_upper_range)
    if np.count_nonzero(yellow_mask) > 1000:
        print("has yellow", )
        print(np.count_nonzero(yellow_mask))
        sendMessage("K1")
    ##cv2.imshow("yellow", yellow_mask)
    return status




#camera starts here
camera = PiCamera()
camera.resolution = (640, 480)
camera.framerate = 3

rawCapture = PiRGBArray(camera, size=(640, 480))

for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
    image = frame.array
    find_colour_victim()
    find_visual_victim()
    cv2.imshow("frame", image)
    rawCapture.truncate(0)
    key = cv2.waitKey(2)
    if key == 27: 
        break

