import cv2
import numpy as np
from picamera.array import PiRGBArray
from picamera import PiCamera

camera = PiCamera()
camera.resolution = (640, 480)
camera.framerate = 30

minx = 640
maxx = 0
miny = 480
maxy = 0
rawCapture = PiRGBArray(camera, size=(640, 480))

for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
    img = frame.array
    imgContour = img.copy()

    gray = cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
    ret,binary = cv2.threshold(gray,70,255,0, cv2.THRESH_BINARY)
    imgContour, contours, hierarchy = cv2.findContours(binary,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    cv2.imshow("Contour Detection",imgContour)

    cv2.drawContours(img, contours, -1, (0, 255, 0), 3)
    cv2.imshow("Contour printed",img)

    for cnt in contours:
        area = cv2.contourArea(cnt)

        if area>20 and area < 10000:
            para = cv2.arcLength(cnt,True)
            approx = cv2.approxPolyDP(cnt, 0.009 * cv2.arcLength(cnt, True), True)
            n = approx.ravel() 
            i = 0
            minx = 640
            maxx = 0
            miny = 480
            maxy = 0
            if len(approx) < 4:
                break
            while i < len(n):     
                if(i % 2 == 0):
                    x = n[i]
                    y = n[i + 1]
                    print(x, y)
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
            if maxy == miny or maxx ==miny:
                break
            h, v = imgCnt.shape
            dsize = (200,int(v/h * 200))

            RImgCnt = cv2.resize(imgCnt, dsize)  


            cv2.imshow("contour", RImgCnt)
            print("area: " + str(area))
            print("perimeter: " + str(para))
            approx = cv2.approxPolyDP(cnt,0.02*para,True)
            print("approximate points: " + str(len(approx)))
            par = para/area
            print(par)
            if par > 8 and par < 12: 
                print("S detected")
            elif par > 18 and par < 24: 
                print("U detected")
            elif par > 50 and par < 100: 
                print("H detected")
    rawCapture.truncate(0)
    key = cv2.waitKey(2)
    if key == 27:
        break
