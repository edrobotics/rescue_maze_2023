import cv2
import numpy as np
from picamera.array import PiRGBArray
from picamera import PiCamera
import socket
import time
import random
import timeit
import threading 
import logging


ssh = True
showcolor = False
inRobot = True
connected = False
ttime = False


for i in range(10):
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
        time.sleep(2)
    else:
        print("Connected")
        connected = True
        break

E = 0
H = 0
S = 0
U = 0
GREEN = 0
YELLOW = 0
RED = 0

def log(image, name):
    global E
    global H
    global S
    global U
    global GREEN
    global YELLOW 
    global RED

    path = "./log/" + name 

    match name:
        case["E"]:
            E += 1
            path += path + str(E)
        case["H"]:
            H += 1
            path += path + str(H)
        case["S"]:
            S += 1
            path += path + str(S)
        case["U"]:
            U += 1
            path += path + str(U)
        case["GREEN"]:
            GREEN += 1
            path += path + str(GREEN)
        case["YELLOW"]:
            YELLOW += 1
            path += path + str(YELLOW)
        case["RED"]:
            RED += 1
            path += path + str(RED)
        case _ :
            print("smt wrong: ", name)
    cv2.imwrite(path,image)
    


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
pS = cv2.imread('S2.png')
pS = cv2.cvtColor(pS,cv2.COLOR_BGR2GRAY)

SampleVictims = (rU, lU, nH, rS, lS)


def identify_victim2(ivictim,victim):
  #  if ssh == False:
   #     cv2.imshow("victim", ivictim)
    found = False
    contours, hierarchy = cv2.findContours(ivictim,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    for cnt in contours:
        area = cv2.contourArea(cnt)
        para = cv2.arcLength(cnt,True)
        if para == 0:
            break
        approx = cv2.approxPolyDP(cnt, 0.003 * cv2.arcLength(cnt, True), True)
        apr = area/para
        print("apr: ",str(apr))
        print("area: ", str(area))
        print("para: ", str(para))
        print("approx: ", len(approx))
        if victim == "H":
            if apr > 11 and apr < 17 and len(approx) > 12 and len(approx) < 25:
                print("H detected")
                found = True
        if victim == "S":
            if apr > 9 and apr < 13 and len(approx) > 32 and len(approx) < 40:
                print("S detected")
                found = True
        if victim == "U":
            if apr > 10 and apr < 15 and len(approx) > 17 and len(approx) < 25:
                print("U detected")
                found = True
        if found: log(ivictim,victim)
        return found


def identify_victim(victim, side):
#    if not ssh: cv2.imshow("victim", victim)
    i = 0
    for sample in SampleVictims:
        negativ  = cv2.bitwise_and(victim, sample)
        if np.count_nonzero(negativ) < 100:
            if i == 0:
                print("rU")
                print(np.count_nonzero(negativ))
                if identify_victim2(victim, "U"):
                    sendMessage("k0"+side)
            elif i == 1:
                print("lU")
                if identify_victim2(victim, "U"):
                    sendMessage("k0"+side)
            elif i == 2:
                print("nH")
                if identify_victim2(victim, "H"):
                    sendMessage("k3"+side)
            elif i == 3:
#                print("rS")
                victiminv = np.invert(victim)
                positiv = cv2.bitwise_and(pS, victim)
                if np.count_nonzero(positiv) <  600: break
                if identify_victim2(victim, "S"):
                    sendMessage("k2"+side)
            elif i == 4:
                print("lS")
                if identify_victim2(victim, "S"):
                    sendMessage("k2"+side)
#                cv2.waitKey(0)
            else:
                print("smt wrong")
        i = i + 1

def find_visual_victim():
    img = image.copy
    gray = cv2.cvtColor(image,cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(gray, (7, 7), 0)
#    ret,binary = cv2.threshold(gray,125,255,0, cv2.THRESH_BINARY)
   # binary = cv2.adaptiveThreshold(blurred,255,cv2.ADAPTIVE_THRESH_GAUSSIAN_C,cv2.THRESH_BINARY,21,10)
    binary = cv2.adaptiveThreshold(blurred,255,cv2.ADAPTIVE_THRESH_MEAN_C, cv2.THRESH_BINARY,21,10)
    binary = np.invert(binary)
#    cv2.imshow("binary", binary)
    binary[:, 280:350] = (0)
    binary[:10, :] = (0)
    binary[:40, :320] = (0)
    binary[470:, :] = (0)
    binary[420:, :320] = (0)
    contours, hierarchy = cv2.findContours(binary,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
#    cv2.imshow("binary2", binary)
    cv2.drawContours(image, contours, -1, (0, 255, 0), 3)
#    print("looping")
#    print(len(contours))
    for cnt in contours:
        area = cv2.contourArea(cnt)
        image2 = image
        rect = cv2.minAreaRect(cnt)
        box = cv2.boxPoints(rect)
        box = np.int0(box)
        cv2.drawContours(image, [box], 0, (255, 0, 0), 3)
        if area>200:
            cv2.drawContours(image2, [box], 0, (0, 0, 255), 3)
 #           cv2.imshow("image2", image2)
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
            if maxx > 300: side ="r"
            else: side = "l"


            if width < 15 or height < 15:
#                print("to small area")
                continue
            h, v = imgCnt.shape
            dsize = (200,200)
            RImgCnt = cv2.resize(imgCnt, dsize)
            identify_victim(RImgCnt,side)

def ColVicP(mask,color):
    kernel = np.ones((5, 5), np.uint8) 
    mask = cv2.erode(mask,kernel, iterations=1)
    mask = cv2.dilate(mask,kernel, iterations=1) 
    if np.count_nonzero(mask) > 2000:
        ret,thresh = cv2.threshold(mask, 40, 255, 0)
        contours, hierarchy = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        c = max(contours, key = cv2.contourArea)
        if cv2.contourArea(c) > 2000:
            x,y,w,h = cv2.boundingRect(c)
            if x < 300: side = "l"
            else: side = "r"
            sendMessage("k1"+side)
            log(mask, color)
    if showcolor: cv2.imshow(color, mask)


def find_colour_victim():
    status = 0
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    hsv[:, 290:350] = (0,0,0)

    red_lower_range = np.array([100,100,100])
    red_upper_range = np.array([220,255,255])
    red_mask = cv2.inRange(hsv, red_lower_range, red_upper_range)
    ColVicP(red_mask, "RED")
    green_lower_range = np.array([50,40,40])
    green_upper_range = np.array([65,255,255])
    green_mask = cv2.inRange(hsv, green_lower_range, green_upper_range)
    ColVicP(green_mask, "GREEN")
    yellow_lower_range = np.array([15,100,100])
    yellow_upper_range = np.array([25,255,255])
    yellow_mask = cv2.inRange(hsv, yellow_lower_range, yellow_upper_range)
    ColVicP(yellow_mask, "YELLOW")




#camera starts here
camera = PiCamera()
camera.resolution = (640, 480)
camera.framerate = 10
#camera.shutter_speed = 10000
#camera.iso = 800
rawCapture = PiRGBArray(camera, size=(640, 480))
for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
    image = frame.array
    log(image, "E")
    rawCapture.truncate(0)
    find_colour_victim()
    find_visual_victim()
    try:
        if not ssh: cv2.imshow("frame", image)
        pass
    except:
        ssh = True
        showcolor = False
    #key = cv2.waitKey(1)
  #  if key == 27:
   #     break
