import cv2
import numpy as np
from picamera.array import PiRGBArray
from picamera import PiCamera
import socket
import time
import random
import timeit

ssh = False
showcolor = False
inRobot = True
connected = True
while connected: 
    print("connecting...")
    try: 
        HEADER = 16
        PORT = 4242
        SERVER = socket.gethostbyname(socket.gethostname())
        ADDR = (SERVER, PORT)
        FORMAT = 'utf-8'
        DISCONNECT_MESSAGE = "!DISCONNECT"
        client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client.connect(ADDR)
    except: 
        print("failed")
    else: 
        break

def sendMessage(msg):
    if connected:
        message = msg.encode(FORMAT)
        msg_length = len(message).to_bytes(HEADER, "big")
        client.send(msg_length)
        client.send(message)
    else: 
        print("failed to send messsage")


nU = cv2.imread('U1.png')
rU = cv2.cvtColor(nU,cv2.COLOR_BGR2GRAY)
lU = cv2.rotate(rU, cv2.ROTATE_180)

nH = cv2.imread('H1.png')
nH = cv2.cvtColor(nH,cv2.COLOR_BGR2GRAY)

nS = cv2.imread('S1.png')
rS = cv2.cvtColor(nS,cv2.COLOR_BGR2GRAY)
lS = cv2.rotate(rS, cv2.ROTATE_180)

SampleVictims = (rU, lU, nH, rS, lS)

def identify_victim(victim, side): 
    if not ssh: cv2.imshow("victim", victim)
    contours, hierarchy = cv2.findContours(victim,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    for cnt in contours:
        area = cv2.contourArea(cnt)
        para = cv2.arcLength(cnt,True)
        if para == 0:
            break
        apr = area/para
        approx = cv2.approxPolyDP(cnt, 0.003 * cv2.arcLength(cnt, True), True)
    i = 0
    for sample in SampleVictims:
        negativ  = cv2.bitwise_and(victim, sample)
        if np.count_nonzero(negativ) < 100:
            if i == 0: 
                print("rU")
                sendMessage("k0"+side)
            elif i == 1: 
                print("lU")
                sendMessage("k0"+side)
            elif i == 2: 
                print("nH")
                sendMessage("k3"+side)
            elif i == 3: 
                print("rS")
                sendMessage("k2"+side)
            elif i == 4: 
                print("lS")
                sendMessage("k2"+side)
#                cv2.waitKey(0)
            else:     
                print("smt wrong")
        i = i + 1

def find_visual_victim():
    gray = cv2.cvtColor(image,cv2.COLOR_BGR2GRAY)
    ret,binary = cv2.threshold(gray,70,255,0, cv2.THRESH_BINARY)

    binary = np.invert(binary)
#    cv2.imshow("binary", binary)
    binary[:, 290:370] = (0)
    contours, hierarchy = cv2.findContours(binary,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
#    cv2.imshow("binary2", binary)
    cv2.drawContours(image, contours, -1, (0, 255, 0), 3)
#    print("looping")
#    print(len(contours))
    for cnt in contours:
        area = cv2.contourArea(cnt)
        rect = cv2.minAreaRect(cnt)
        box = cv2.boxPoints(rect)
        box = np.int0(box)
        cv2.drawContours(image, [box], 0, (255, 0, 0), 3)
        if area>50:
#            cv2.waitKey(0)
            para = cv2.arcLength(cnt,True)
            approx = cv2.approxPolyDP(cnt, 0.009 * cv2.arcLength(cnt, True), True)
            n = approx.ravel() 
            i = 0
            minx = 640
            maxx = 0
            miny = 480
            maxy = 0
            if len(approx) < 5:
#                print("break len: ", str(len(approx)))
                continue           
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
            if maxx < 300: side ="r"
            else: side = "l"


            if width < 10 or height < 10:
#                print("to small area")
                continue
            h, v = imgCnt.shape
            dsize = (200,200)
            RImgCnt = cv2.resize(imgCnt, dsize)  
            identify_victim(RImgCnt,side)
            
def find_colour_victim():
    status = 0
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)

    red_lower_range = np.array([150,200,40])
    red_upper_range = np.array([220,255,255])
    red_mask = cv2.inRange(hsv, red_lower_range, red_upper_range)
    if showcolor: cv2.imshow("red", red_mask)
    if np.count_nonzero(red_mask) > 2000:
        print("has red") 
        print(np.count_nonzero(red_mask))
        sendMessage("k1l")
    green_lower_range = np.array([30,40,40])
    green_upper_range = np.array([70,255,255])
    green_mask = cv2.inRange(hsv, green_lower_range, green_upper_range)
    if showcolor: cv2.imshow("green", green_mask)
    if np.count_nonzero(green_mask) > 1000: 
        print("has green")
        print(np.count_nonzero(green_mask))
        sendMessage("k0l")
    yellow_lower_range = np.array([15,100,100])
    yellow_upper_range = np.array([20,255,255])
    yellow_mask = cv2.inRange(hsv, yellow_lower_range, yellow_upper_range)
    if np.count_nonzero(yellow_mask) > 1000:
        print("has yellow", )
        print(np.count_nonzero(yellow_mask))
        sendMessage("k1l")
    if showcolor: cv2.imshow("yellow", yellow_mask)
    return status




#camera starts here
camera = PiCamera()
camera.resolution = (640, 480)
camera.framerate = 10  

#start_etime = time.time()
#frame_time = time.time() 
rawCapture = PiRGBArray(camera, size=(640, 480))
for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
#    print("frame time", time.time() - frame_time)
#    print("entire time", time.time() - start_etime)
#    start_etime = time.time()
    image = frame.array
    rawCapture.truncate(0)
    start_time = time.time()
    find_colour_victim()
#    print("Color_victim time", time.time() - start_time)
 #   start_time = time.time()
    find_visual_victim()
    #print("visual_victim time", time.time() - start_time)
   # test_time = time.time() 
    try: 
        if not ssh: cv2.imshow("frame", image)
        pass
    except: 
        ssh = True
        showcolor = False
    key = cv2.waitKey(1)
    if key == 27: 
        break
 #   print("test", time.time() - test_time)
  #  frame_time = time.time() 
