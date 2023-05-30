#!/usr/bin/env python3
import cv2
import numpy as np
import socket
import time
import logging


ssh = False
showcolor = True
inRobot = True
connected = False
ttime = False


for i in range(1):
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


def log(image, name, frame):
    try: 
        path = f'./log/{name}{frame}.png'
        cv2.imwrite(path,image)
    except:
        pass
    


def sendMessage(msg):
    print(f"sending message: {msg}")
    try:
        message = msg.encode(FORMAT)
        msg_length = len(message).to_bytes(HEADER, "big")
        client.send(msg_length)
        client.send(message)
    except:
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

Hand = cv2.imread('./samples/Hsample_and.png')
Hand = cv2.cvtColor(Hand,cv2.COLOR_BGR2GRAY)

Hor = cv2.imread('./samples/Hsample_or.png')
Hor = cv2.cvtColor(Hor,cv2.COLOR_BGR2GRAY)

Sand = cv2.imread('./samples/Ssample_and.png')
Sand = cv2.cvtColor(Sand,cv2.COLOR_BGR2GRAY)

Sor = cv2.imread('./samples/Ssample_or.png')
Sor = cv2.cvtColor(Sor,cv2.COLOR_BGR2GRAY)

Uand = cv2.imread('./samples/Usample_and.png')
Uand = cv2.cvtColor(Uand,cv2.COLOR_BGR2GRAY)

Uor = cv2.imread('./samples/Usample_or.png')
Uor = cv2.cvtColor(Uor,cv2.COLOR_BGR2GRAY)




SampleVictims = (rU, lU, nH, rS, lS)
And_Victim = (Hand,Sand,Uand)
OR_victim = (Hor,Sor,Uor)


def identify_victim2(ivictim,victim,n):
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
#        print("apr: ",str(apr))
#        print("para: ", str(para))
#        print("approx: ", len(approx))
        if victim == "H":
            if apr > 11 and apr < 17 and len(approx) > 12 and len(approx) < 25:
                print("H detected")
                found = True
        elif victim == "S":
            if apr > 9 and apr < 13 and len(approx) > 32 and len(approx) < 40:
                print("S detected")
                found = True
        elif victim == "U":
            if apr > 10 and apr < 15 and len(approx) > 17 and len(approx) < 25:
                print("U detected")
                found = True
        if found: log(ivictim,victim,n)

        return found
    


def identify_victimI(ivictim):
    x = -1
    identified = False
    victim = None
    kits = None
    for sample in And_Victim:
        x = x +1 
        for i in range(2):
            
            if i == 1: ivictim = cv2.rotate(ivictim,cv2.ROTATE_180)
            M_AND = cv2.bitwise_and(sample, ivictim)
            M_AND_C = np.count_nonzero(M_AND)
            M_OR = cv2.bitwise_and(OR_victim[x], ivictim)
            M_OR_C = np.count_nonzero(M_OR)
            MIN_and = np.count_nonzero(sample) -100
            MIN_or = np.count_nonzero(ivictim) - 100
          #  print(f"victim size: {M_AND_C}")
            if MIN_and < M_AND_C and M_OR_C > MIN_or:
                if x == 0: 
                    victim = "H"
                    kits = 3
                elif x == 1: 
                    victim = "S"
                    kits = 2
                elif x == 2: 
                    victim = "U"
                    kits = 0
               # if i == 1: side = "left"
                print(f"identified: {victim}")
                logging.info(f"identified victim: {victim}")
                identified = True
                break
    return (identified,kits)





def identify_victim(victim, side,n):
#    if not ssh: cv2.imshow("victim", victim)
    i = 0
    for sample in SampleVictims:
        negativ  = cv2.bitwise_and(victim, sample)
        if np.count_nonzero(negativ) < 100:
            if i == 0:
                print("rU")
                print(np.count_nonzero(negativ))
                if identify_victim2(victim, "U",n):
                    sendMessage("k0"+side)
            elif i == 1:
                print("lU")
                if identify_victim2(victim, "U",n):
                    sendMessage("k0"+side)
            elif i == 2:
                print("nH")
                if identify_victim2(victim, "H",n):
                    sendMessage("k3"+side)
            elif i == 3:
#                print("rS")
                victiminv = np.invert(victim)
                positiv = cv2.bitwise_and(pS, victim)
                if np.count_nonzero(positiv) <  600: break
                if identify_victim2(victim, "S",n):
                    sendMessage("k2"+side)
            elif i == 4:
                print("lS")
                if identify_victim2(victim, "S",n):
                    sendMessage("k2"+side)
#                cv2.waitKey(0)
            else:
                print("smt wrong")
        i = i + 1

def find_visual_victim(image,framenum):
    img = image.copy
    #cv2.imshow("test",image)
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
            if maxx < 300: side ="r"
            else: side = "l"


            if width < 15 or height < 15:
#                print("to small area")
                continue
            h, v = imgCnt.shape
            dsize = (200,200)
            RImgCnt = cv2.resize(imgCnt, dsize)
            identify_victim(RImgCnt,side,framenum)
            result = identify_victimI(RImgCnt)
            if result[0] == True: 
                sendMessage(f"k{result[1]}{side}")

def ColVicP(mask,color,n,image):
    kernel = np.ones((9, 9), np.uint8) 
    mask = cv2.erode(mask,kernel, iterations=1)
    mask = cv2.dilate(mask,kernel, iterations=1) 
    if np.count_nonzero(mask) > 5000 and np.count_nonzero(mask < 30000):
        print(np.count_nonzero(mask))
        ret,thresh = cv2.threshold(mask, 40, 255, 0)
        contours, hierarchy = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        c = max(contours, key = cv2.contourArea)
        if cv2.contourArea(c) > 5000:
            print(cv2.contourArea(c))
            x,y,w,h = cv2.boundingRect(c)
            if x > 300: side = "l"
            else: side = "r"
            sendMessage("k1"+side)
            mask = cv2.bitwise_and(image, image, mask=mask)
            log(mask, color,n)
            logging.info(f"found {color}, image {n}")
    if showcolor: cv2.imshow(color, mask)


def find_colour_victim(image,n):
#    image = image.copy
    status = 0
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
#    hsv[:, 290:350] = (0,0,0)

    red_lower_range = np.array([100,100,100])
    red_upper_range = np.array([220,255,255])
    red_mask = cv2.inRange(hsv, red_lower_range, red_upper_range)
    ColVicP(red_mask, "RED",n,image)
    green_lower_range = np.array([50,40,40])
    green_upper_range = np.array([80,255,255])
    green_mask = cv2.inRange(hsv, green_lower_range, green_upper_range)
    ColVicP(green_mask, "GREEN",n,image)
    yellow_lower_range = np.array([15,100,100])
    yellow_upper_range = np.array([25,255,255])
    yellow_mask = cv2.inRange(hsv, yellow_lower_range, yellow_upper_range)
    ColVicP(yellow_mask, "YELLOW",n,image)

