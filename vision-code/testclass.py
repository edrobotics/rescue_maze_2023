
#!/usr/bin/env python3
import cv2
import numpy as np
import socket
import time
import logging





class main:
    def __init__(self):
#        self.image = image
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


    def find_victim(self,):

        img = self.image
        #cv2.imshow("test",image)
        gray = cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
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
        cv2.drawContours(img, contours, -1, (0, 255, 0), 3)
    #    print("looping")
    #    print(len(contours))
        for cnt in contours:
            area = cv2.contourArea(cnt)
            image2 = img
            rect = cv2.minAreaRect(cnt)
            box = cv2.boxPoints(rect)
            box = np.int0(box)
            cv2.drawContours(img, [box], 0, (255, 0, 0), 3)
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
                if maxx < 300: self.side ="r"
                else: self.side = "l"
                if width < 15 or height < 15:
    #                print("to small area")
                    continue
           #     h, v = imgCnt.shape
                dsize = (200,200)
                RImgCnt = cv2.resize(imgCnt, dsize)
                identify_victim(RImgCnt,side,framenum)
                result = identify_victimI(RImgCnt)
                if result[0] == True: 
                    sendMessage(f"k{result[1]}{side}")


    def Color_victim(self):
        image = self.image
#    image = image.copy
        status = 0
        hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    #    hsv[:, 290:350] = (0,0,0)

        red_lower_range = np.array([100,100,100])
        red_upper_range = np.array([220,255,255])
        red_mask = cv2.inRange(hsv, red_lower_range, red_upper_range)
        self.ColVicP(red_mask, "RED")
        green_lower_range = np.array([50,40,40])
        green_upper_range = np.array([80,255,255])
        green_mask = cv2.inRange(hsv, green_lower_range, green_upper_range)
        self.ColVicP(green_mask, "GREEN")
        yellow_lower_range = np.array([15,100,100])
        yellow_upper_range = np.array([25,255,255])
        yellow_mask = cv2.inRange(hsv, yellow_lower_range, yellow_upper_range)
        self.ColVicP(yellow_mask, "YELLOW")



    def ColVicP(self, mask,color):
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
                if x > 300: self.side = "l"
            else: self.side = "r"
            sendMessage("k1"+side)
            mask = cv2.bitwise_and(image, image, mask=mask)
            self.log(color)
            logging.info(f"found {color}, image {n}")
#    if showcolor: cv2.imshow(color, mask)


    def new_img(self, image,num):
        self.find_victim()
        self.Color_victim()
        self.image = image
        self.num = num


    def log(self):
        pass


    def identify_victim(self):
        pass




def sendMessage(self, msg):
    print(f"sending message: {msg}")
    try:
        message = msg.encode(FORMAT)
        msg_length = len(message).to_bytes(HEADER, "big")
        client.send(msg_length)
        client.send(message)
    except:
        print("failed to send messsage")
    print(f"sending message: {msg}")
    try:
        message = msg.encode(FORMAT)
        msg_length = len(message).to_bytes(HEADER, "big")
        client.send(msg_length)
        client.send(message)
    except:
        print("failed to send messsage")

