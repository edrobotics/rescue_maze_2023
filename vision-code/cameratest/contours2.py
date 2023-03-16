import cv2
import numpy as np
from picamera.array import PiRGBArray
from picamera import PiCamera



def identify_victim(victim): 
    victim = np.invert(victim)
    cv2.imshow("victim", victim)
    imgContour, contours, hierarchy = cv2.findContours(victim,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    for cnt in contours:

        area = cv2.contourArea(cnt)
        para = cv2.arcLength(cnt,True)
        if para == 0:
            break
        approx = cv2.approxPolyDP(cnt, 0.005 * cv2.arcLength(cnt, True), True)
        apr = area/para
        print("apr: ",str(apr))
        print("area: ", str(area))
        print("para: ", str(para))
        print("approx: ", len(approx))
        if apr > 9 and apr < 12 and len(approx) > 12 and len(approx) < 16:
            print("H detected")
        if apr > 9 and apr < 11 and len(approx) > 25 :
            print("S detected")
        if apr > 10 and apr < 12 and len(approx) > 16 and len(approx) < 20:
            print("U detected")


camera = PiCamera()
camera.resolution = (640, 480)
camera.framerate = 5

rawCapture = PiRGBArray(camera, size=(640, 480))

for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
    img = frame.array
    imgContour = img.copy()

    gray = cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
    ret,binary = cv2.threshold(gray,70,255,0, cv2.THRESH_BINARY)
    imgContour, contours, hierarchy = cv2.findContours(binary,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    cv2.imshow("binary", binary)
    cv2.drawContours(img, contours, -1, (0, 255, 0), 3)

    for cnt in contours:
        area = cv2.contourArea(cnt)
        if area>10 and area < 100000:
            para = cv2.arcLength(cnt,True)
            approx = cv2.approxPolyDP(cnt, 0.009 * cv2.arcLength(cnt, True), True)
            n = approx.ravel() 
            i = 0
            minx = 640
            maxx = 0
            miny = 480
            maxy = 0
            if len(approx) < 4:
                print("break len: ", str(len(approx)))
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
            if maxy == miny or maxx ==minx:
                print("error area == 0")
                break
            h, v = imgCnt.shape
            dsize = (200,int(v/h * 200))
            RImgCnt = cv2.resize(imgCnt, dsize)  
            identify_victim(RImgCnt)
            cv2.imshow("contour", RImgCnt)
    rawCapture.truncate(0)
    key = cv2.waitKey(2)
    if key == 27:
        break
