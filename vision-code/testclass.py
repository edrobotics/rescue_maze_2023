#!/usr/bin/env python3
import cv2
import numpy as np
import socket
import time
import logging
import os


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
#change the following codes to dicts?

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
    cwd = os.getcwd()
    if cwd == '/Users/lukas/rescue_maze_2023/vision-code':
        sampledir = "./samples/"
       # print("on mac")        
    else: sampledir = "./samples/" #change this absolute on pi
    Dictand = {
        "H": None, 
        "S": None, 
        "U": None 
        }
    Dictor = {
        "H": None, 
        "S": None, 
        "U": None 
        }
    
    sample_tuple = (Dictand,Dictor)
    i = 0 
    for Dict in sample_tuple:
        
        if Dict == 0: ending = "sample_and"
        else: ending = "sample_or"
        i = i + 1
        for key in Dict:
            sample_path = f"{sampledir}{key}{ending}.png"
            bgr = cv2.imread(sample_path)
            binary = cv2.cvtColor(bgr,cv2.COLOR_BGR2GRAY)
            Dict[key] = binary









    def __init__(self, showsource= False,showcolor = False, show_visual = False):
        self.showsource = showsource
        self.showcolor = showcolor
        self.show_visual = show_visual


    def do_the_work(self, image, fnum):
        self.image = image
        self.fnum = fnum
        self.log("E")
        
        self.Color_victim2()
        self.find_victim()


    def blank_out(self, binary):
        #blanks out pixels that can't be poi but can still make problems
        binary[:, 280:350] = (0)
        binary[:10, :] = (0)
        binary[:40, :320] = (0)
        binary[470:, :] = (0)
        binary[420:, :320] = (0)
        return binary


    def preproccesing(self,img):
        gray = cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
        blurred = cv2.GaussianBlur(gray, (7, 7), 0)
    #    ret,binary = cv2.threshold(gray,125,255,0, cv2.THRESH_BINARY)
    # binary = cv2.adaptiveThreshold(blurred,255,cv2.ADAPTIVE_THRESH_GAUSSIAN_C,cv2.THRESH_BINARY,21,10)
        binary = cv2.adaptiveThreshold(blurred,255,cv2.ADAPTIVE_THRESH_MEAN_C, cv2.THRESH_BINARY,21,10)
        binary = np.invert(binary)
    #    cv2.imshow("binary", binary)
        binary = self.blank_out(binary)
        self.binary = binary
        return binary


    def find_victim(self):

        img = self.image
        #cv2.imshow("test",image)
        binary = self.preproccesing(img)
 
        contours, hierarchy = cv2.findContours(binary,cv2.RETR_LIST,cv2.CHAIN_APPROX_NONE)
    #    cv2.imshow("binary2", binary)
        cv2.drawContours(img, contours, -1, (0, 255, 0), 3)
        imgCnt = self.get_poi(contours)
        dsize = (200,200)
        RImgCnt = cv2.resize(imgCnt, dsize)
        result = self.identify_victim(RImgCnt)
        if result[0] == True: 
            sendMessage(f"k{result[1]}{self.side}")

    def get_poi(self, contours): #loops through contours and returns all above a size and appoximation points 
        for cnt in contours:
            area = cv2.contourArea(cnt)
#            image2 = self.img.copy
#            rect = cv2.minAreaRect(cnt)
#            box = cv2.boxPoints(rect)
#            box = np.int0(box)
#            cv2.drawContours(img, [box], 0, (255, 0, 0), 3)
#           cv2.imshow("image2", image2)
#            cv2.waitKey(0)
            if area>200:
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
                imgCnt = self.binary[miny:maxy, minx:maxx]
                width = maxx - minx
                height = maxy - miny

                if maxx < 320: self.side ="r"
                else: self.side = "l"

        return imgCnt 


    def identify_victim(self,ivictim): #compares binary poi with binary sample images 
        x = -1
        identified = False
        victim = None
        kits = None
        for key in self.Dictand:
            x = x + 1 
            sample = self.Dictand[key]

            for i in range(2):
                
                if i == 1: ivictim = cv2.rotate(ivictim,cv2.ROTATE_180)
                M_AND = cv2.bitwise_and(sample, ivictim)
                M_AND_C = np.count_nonzero(M_AND)
                M_OR = cv2.bitwise_and(self.Dictor[key], ivictim)
                M_OR_C = np.count_nonzero(M_OR)
                MIN_and = np.count_nonzero(sample) -100
                MIN_or = np.count_nonzero(ivictim) - 100#change this to %?
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

        red_lower_range = np.array([130,100,100])
        red_upper_range = np.array([180,255,255])
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

    def Color_victim2(self):
        image = self.image
        #    image = image.copy
        status = 0
        hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
        #    hsv[:, 290:350] = (0,0,0)

        lower_range = {
            "red" : np.array([130,100,100]),
            "green": np.array([50,40,40]),
            "yellow": np.array([15,100,100])
            }
        upper_range = {
            "red" : np.array([180,255,255]),
            "green" : np.array([80,255,255]),
            "yellow" : np.array([25,255,255])
            }
        for color in lower_range:
            lower = lower_range[color]
            upper = upper_range[color]
            mask = cv2.inRange(hsv,lower,upper)
            if color == "red":
                red2_lower =np.array([0,40,40]) 
                red2_upper =np.array([10,255,255]) 
                mask2 = cv2.inRange(hsv,red2_lower,red2_upper)
                mask = np.bitwise_or(mask,mask2)
            mask = self.blank_out(mask)
            self.ColVicP(mask, color)



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
                print(f"{color}: {cv2.contourArea(c)}")

                x,y,w,h = cv2.boundingRect(c)
                if x > 300: self.side = "l"
            else: self.side = "r"
            if color == "GREEN": k = "k0"
            else: k = "k1"
            sendMessage(k+self.side)
            mask = cv2.bitwise_and(self.image, self.image, mask=mask)
            self.log(color,img = mask)
            logging.info(f"found {color}, image {self.fnum}")
        if self.showcolor: cv2.imshow(color, mask)





    def log(self, name, img=None):
        if img is None:
            img = self.image
        
        
        path = f'./log/{name}{self.fnum}.png'
        cv2.imwrite(path,img)

        
        






