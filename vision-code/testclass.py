
#!/usr/bin/env python3
import cv2
import numpy as np
import socket
import time
import logging

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




def sendMessage(msg):
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






class imgproc:
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

    And_Victim = (Hand,Sand,Uand)
    OR_victim = (Hor,Sor,Uor)

    def __init__(showsource= False,showcolor = False, show_visual = False):
        self.showsource = showsource
        self.showcolor = showcolor
        self.show_visual = show_visual


    def do_the_work(self, image, fnum):
        self.image = image
        self.fnum = fnum
        self.find_victim()
        self.Color_victim()
        self.log("E")


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
                result = self.identify_victim(RImgCnt)
                if result[0] == True: 
                    sendMessage(f"k{result[1]}{self.side}")




    def identify_victim(self,ivictim):
        x = -1
        identified = False
        victim = None
        kits = None
        for sample in self.And_Victim:
            x = x +1 
            for i in range(2):
                
                if i == 1: ivictim = cv2.rotate(ivictim,cv2.ROTATE_180)
                M_AND = cv2.bitwise_and(sample, ivictim)
                M_AND_C = np.count_nonzero(M_AND)
                M_OR = cv2.bitwise_and(self.OR_victim[x], ivictim)
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
                    self.log(victim, img= ivictim)
                    identified = True
                    break
        return (identified,kits)


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
            if color == "GREEN": k = "k0"
            else: k = "k1"
            sendMessage(k+self.side)
            mask = cv2.bitwise_and(self.image, self.image, mask=mask)
            self.log(color,img = mask)
            logging.info(f"found {color}, image {self.fnum}")
#    if showcolor: cv2.imshow(color, mask)





    def log(self, name, img=None):
        if img is None:
            img = self.image
        
        
        path = f'./log/{name}{self.fnum}.png'
        cv2.imwrite(path,img)

        
        







