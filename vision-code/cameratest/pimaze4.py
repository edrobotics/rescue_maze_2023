import cv2
import numpy as np
from picamera.array import PiRGBArray
from picamera import PiCamera
import socket
import time
import random
ssh = False
showcolor = False
inRobot = False
socket = False 
while socket: 
    print("Connecting to server...")
    try:
        HEADER = 16
        PORT = 4242
        SERVER = socket.gethostbyname(socket.gethostname())
        ADDR = (SERVER, PORT)
        FORMAT = 'utf-8'
        DISCONNECT_MESSAGE = "!DISCONNECT"
        client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client.connect(ADDR)
        
        break
    except: 
        print("failed to connect to server")



def sendMessage(msg):
    if socket: 
        message = msg.encode(FORMAT)
        msg_length = len(message).to_bytes(HEADER, "big")
        client.send(msg_length)
        client.send(message)



def identify_victim(victim): 
#    victim = np.invert(victim)
    if ssh == False:
        cv2.imshow("victim", victim)

    pixels = np.count_nonzero(victim)
    print("pixels: ", pixels)
    contours, hierarchy = cv2.findContours(victim,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    for cnt in contours:
        try:
            M = cv2.moments(cnt)
            cx = int(M['m10']/M['m00'])
            cy = int(M['m01']/M['m00'])
 #           print("cx+cy:  ",cx,cy)
        except: 
            print("Something is wrong I can feel it")
        area = cv2.contourArea(cnt)
        para = cv2.arcLength(cnt,True)
        if para == 0: 
            break
        approx = cv2.approxPolyDP(cnt, 0.003 * cv2.arcLength(cnt, True), True)
        apr = area/para
#        print("apr: ",str(apr))
   #     print("area: ", str(area))
  #      print("para: ", str(para))
#        print("approx: ", len(approx))
        if apr > 11 and apr < 15 and len(approx) > 12 and len(approx) < 17:
            print("H detected")
            sendMessage("k3")
        if apr > 9 and apr < 13 and len(approx) > 32 and len(approx) < 40:
            print("S detected")
            sendMessage("k2")
        if apr > 10 and apr < 13 and len(approx) > 17 and len(approx) < 23:
            print("U detected")
            sendMessage("k0")


def find_visual_victim():
    img = image.copy
    gray = cv2.cvtColor(image,cv2.COLOR_BGR2GRAY)
    ret,binary = cv2.threshold(gray,70,255,0, cv2.THRESH_BINARY)

    binary = np.invert(binary)
#    cv2.imshow("binary", binary)
    if inRobot: 
        binary[:, 300:360] = (0)
        binary[0:150, :] = (0)
    cv2.imshow("binary2", binary)
    contours, hierarchy = cv2.findContours(binary, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)
    cv2.drawContours(image, contours, -1, (0, 255, 0), 3)

    print("looping..")
    for cnt in contours:
        area = cv2.contourArea(cnt)
        
        rect = cv2.minAreaRect(cnt)
        x, y, w, h = cv2.boundingRect(cnt)
#        print("rect",rect)
        print(x,y,w,h)
        box = cv2.boxPoints(rect)
        cv2.rectangle(image,(x,y),(x+w,y+h),(255,0,0),2)
        box = np.int0(box)
#        print("box: ", box)
        if area>100:
            cv2.drawContours(image, [box], 0, (0, 0, 255), 3)
            para = cv2.arcLength(cnt,True)
            approx = cv2.approxPolyDP(cnt, 0.009 * cv2.arcLength(cnt, True), True)
            imgCnt = binary[x:x+w, y:y+h]

            if w < 5 or h < 5:
                print("to small area")
                break

            h, v = imgCnt.shape
            print(h,v)
            if h < 5 or v <5: 
                print("to small") 
                break
            dsize = (200,200)
            RImgCnt = cv2.resize(imgCnt, dsize)  
            identify_victim(RImgCnt)
            
def find_colour_victim():
    status = 0
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)

    red_lower_range = np.array([150,200,42])
    red_upper_range = np.array([220,255,255])
    red_mask = cv2.inRange(hsv, red_lower_range, red_upper_range)
    if np.count_nonzero(red_mask) > 1337:
        print("has red") 
        print(np.count_nonzero(red_mask))
        sendMessage("k1")
    green_lower_range = np.array([50,42,42])
    green_upper_range = np.array([69,255,255])
    green_mask = cv2.inRange(hsv, green_lower_range, green_upper_range)
    if np.count_nonzero(green_mask) > 1337: 
        print("has green")
        print(np.count_nonzero(green_mask))
        sendMessage("k0")
    yellow_lower_range = np.array([15,100,100])
    yellow_upper_range = np.array([35,255,255])
    yellow_mask = cv2.inRange(hsv, yellow_lower_range, yellow_upper_range)
    if np.count_nonzero(yellow_mask) > 6969:
        print("has yellow", )
        print(np.count_nonzero(yellow_mask))
        sendMessage("k1")


    if showcolor:
        cv2.imshow("yellow", yellow_mask)
        cv2.imshow("green", green_mask)
        cv2.imshow("red", red_mask)
    return status




#camera starts here
camera = PiCamera()
camera.resolution = (640, 480)
camera.framerate = 3

rawCapture = PiRGBArray(camera, size=(640, 480))

for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
    image = frame.array
   # find_colour_victim()
    find_visual_victim()
    if ssh == False: 
        cv2.imshow("frame", image)
    rawCapture.truncate(0)
    key = cv2.waitKey(2)
    if key == 27: 
        break
