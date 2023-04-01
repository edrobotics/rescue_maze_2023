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
ttime = False
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
pS = cv2.imread('S2.png')
pS = cv2.cvtColor(pS,cv2.COLOR_BGR2GRAY)

SampleVictims = (rU, lU, nH, rS, lS)


def identify_victim2(ivictim,victim):
#    victim = np.invert(victim)
    if ssh == False:
        cv2.imshow("victim", ivictim)
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
                cv2.imwrite("H.png", ivictim)
                found = True
        if victim == "S":
            if apr > 9 and apr < 13 and len(approx) > 32 and len(approx) < 40:
                print("S detected")
                cv2.imwrite("s.png", ivictim)
                found = True
        if victim == "U":
            if apr > 10 and apr < 15 and len(approx) > 17 and len(approx) < 25:
                print("U detected")
                cv2.imwrite("U.png", ivictim)
                found = True
        return found


def identify_victim(victim, side):
#    if not ssh: cv2.imshow("victim", victim)
    i = 0
    for sample in SampleVictims:
        negativ  = cv2.bitwise_and(victim, sample)
        if np.count_nonzero(negativ) < 200:
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
    cv2.imshow("binary", binary)
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
        ret,thresh = cv2.threshold(red_mask, 40, 255, 0)
        contours, hierarchy = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        c = max(contours, key = cv2.contourArea)
        x,y,w,h = cv2.boundingRect(c)
        if x < 300: side = "l"
        else: side = "r"
        sendMessage("k1"+side)
    green_lower_range = np.array([50,40,40])
    green_upper_range = np.array([65,255,255])
    green_mask = cv2.inRange(hsv, green_lower_range, green_upper_range)
    green_mask[:, 290:370] = (0)
    if showcolor: cv2.imshow("green", green_mask)
    if np.count_nonzero(green_mask) > 2000:
        print("has green")
        print(np.count_nonzero(green_mask))
        ret,thresh = cv2.threshold(green_mask, 40, 255, 0)
        contours, hierarchy = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        c = max(contours, key = cv2.contourArea)
        x,y,w,h = cv2.boundingRect(c)
        if x < 300: side = "l"
        else: side = "r"
        sendMessage("k0"+side)
    yellow_lower_range = np.array([15,100,100])
    yellow_upper_range = np.array([20,255,255])
    yellow_mask = cv2.inRange(hsv, yellow_lower_range, yellow_upper_range)
    if np.count_nonzero(yellow_mask) > 4000:
        print("has yellow")
        print(np.count_nonzero(yellow_mask))
        ret,thresh = cv2.threshold(yellow_mask, 40, 255, 0)
        contours, hierarchy = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        c = max(contours, key = cv2.contourArea)
        x,y,w,h = cv2.boundingRect(c)
        if x < 300: side = "l"
        else: side = "r"
        #sendMessage("k1"+side)
    if showcolor: cv2.imshow("yellow", yellow_mask)
    return status




#camera starts here
camera = PiCamera()
camera.resolution = (640, 480)
camera.framerate = 10
camera.shutter_speed = 10000
camera.iso = 1600
if ttime: start_etime = time.time()
if ttime: frame_time = time.time()
rawCapture = PiRGBArray(camera, size=(640, 480))
for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
    if ttime: print("frame time", time.time() - frame_time)
    if ttime: print("entire time", time.time() - start_etime)
    if ttime: start_etime = time.time()
    image = frame.array
    cv2.imshow("image", image)
    rawCapture.truncate(0)
    if ttime: start_time = time.time()
    find_colour_victim()
    if ttime: print("Color_victim time", time.time() - start_time)
    if ttime: start_time = time.time()
    find_visual_victim()
    if ttime: print("visual_victim time", time.time() - start_time)
    if ttime: test_time = time.time()
    try:
        if not ssh: cv2.imshow("frame", image)
        pass
    except:
        ssh = True
        showcolor = False
    key = cv2.waitKey(1)
    if key == 27:
        break
    if ttime: print("test", time.time() - test_time)
    if ttime: frame_time = time.time()
