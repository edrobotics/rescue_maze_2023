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





class imgproc:
    victim_pos = None

    cwd = os.getcwd()
    if cwd == '/Users/lukas/GitHub/rescue_maze_2023/vision-code':
        sampledir = "/Users/lukas/GitHub/rescue_maze_2023/vision-code/samples/"
        print("on mac")        
    else: sampledir = "/home/pi/rescue_maze_2023/vision-code/samples/" 
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
        
        if i == 0: ending = "sample_and"
        else: ending = "sample_or"
        i = i + 1
        for key in Dict:
            sample_path = f"{sampledir}{key}{ending}.png"
            print(sample_path)
            bgr = cv2.imread(sample_path)
            binary = cv2.cvtColor(bgr,cv2.COLOR_BGR2GRAY)
            Dict[key] = binary


    #lastdetected = (None, -10, None)
    lastdetected = {
        "H": [None, -10, 0],
        "S": [None, -10, 0],
        "U": [None, -10, 0],
        "red": [None, -10, 0],
        "yellow": [None, -10, 0],
        "green": [None, -10, 0],
    }

    blacklist = []
    def detected(self,msg, victim):

        list = self.lastdetected[victim]
        for key in self.lastdetected:
            if self.lastdetected[key][1] == self.fnum:
                self.blacklist.append(victim)





        if list[1] + 2 < self.fnum:
           self.sendMessage(msg)

        else:
            print("alredy detected")
        new_list = (msg, self.fnum, list[2]+ 1)
        self.lastdetected[victim] = new_list






    def sendMessage(self,msg):
        print("sending message", msg)
        logging.info(f"sending: {msg}")

        try:
            message = msg.encode(FORMAT)
            msg_length = len(message).to_bytes(HEADER, "big")
            client.send(msg_length)
            client.send(message)
        except:
            print("failed to send messsage")



    def __init__(self, showsource= False,showcolor = False, show_visual = False, logging = True, debugidentification = False, info = True, time=False):
        self.showsource = showsource
        self.showcolor = showcolor
        self.show_visual = show_visual
        self.logging = logging
        self.debugidentification = debugidentification
        self.info = info
        self.time = time


    def do_the_work(self, image, fnum):
        self.image = image
        self.image_clone = image.copy()
        self.fnum = fnum
        self.log("E")
        
        color_time = time.time()


        self.find_victim()
        self.Color_victim2()
        if self.showsource:
            cv2.imshow("image_clone",self.image_clone)



    def blank_out(self, binary):
        #blanks out pixels that can't be poi but can still make problems
        binary[:, 280:360] = (0)
        binary[:, 590:] = (0)
        binary[:, :15] = (0)

        binary[:30, :] = (0)
        binary[450:, :] = (0)
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


        cv2.drawContours(self.image_clone, contours, -1, (0, 255, 0), 3)
        result = self.get_poi(contours)
        if result[0] == True: 
            self.detected(f"k{result[1]}{self.side}", result[2])

    def get_poi(self, contours): #loops through contours and returns all above a size and appoximation points 
        result = [None, None, None]
        for cnt in contours:
            area = cv2.contourArea(cnt)
#            cv2.waitKey(0)
            if area>400:
                rect = cv2.minAreaRect(cnt)
                box = cv2.boxPoints(rect)
                box = np.int0(box)
                cv2.drawContours(self.image_clone, [box], 0, (255, 0, 0), 3)

                #para = cv2.arcLength(cnt,True)
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
                dsize = (200,200)
                RImgCnt = cv2.resize(imgCnt, dsize)
                if self.show_visual:
                    cv2.imshow("imgCnt",RImgCnt)

                if maxx < 320: self.side ="r"
                else: self.side = "l"

                result = self.identify_victim(RImgCnt)
                if result[0]:
                    break

        return result 


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
                MIN_and = np.count_nonzero(sample)
                MIN_and = MIN_and * 0.98
                MIN_or = np.count_nonzero(ivictim) * 0.98
                if self.debugidentification:
                    cv2.imshow("M_AND", M_AND)                
                    cv2.imshow("ivictim",ivictim)                
                    cv2.imshow("M_OR",M_OR)                
                    cv2.imshow("dictand",sample)                
                    cv2.imshow("dictor",self.Dictor[key])                
                    print(MIN_and)
                    print(M_AND_C)
                    print(MIN_or)
                    print(M_OR_C)
                    cv2.waitKey(0)

                
            #  print(f"victim size: {M_AND_C}")
                if MIN_and < M_AND_C and M_OR_C > MIN_or:
                    if x == 0: 
                        victim = "H"
                        kits = 3
                    if x == 1: 
                        victim = "S"
                        kits = 2
                    if x == 2: 
                        kits = 0 
                        victim = "U"
                # if i == 1: side = "left"
                    if self.info: print(f"identified: {victim}")
                    logging.info(f"identified victim: {victim}")
                    self.log(victim, img= ivictim)
        if victim:
            identified = True

        return (identified,kits, victim)

    def check_position(self, contour):
        b_position = None
        (x,y,w,h) = cv2.boundingRect(contour)
        victimheight = 142
        victimheight2 = 450
        if victimheight > x and victimheight - 25 < x + w:
            b_position = True
        elif victimheight2 > x and victimheight2 - 25 < x + w:
            b_position = True
        return b_position
 
    def check_movement(self,contour,victim):
        b_movement = None
        (x,y,w,h) = cv2.boundingRect(contour)
        same_height = False
        if self.victim_pos:
            pos = self.victim_pos

            if self.is_close(x, pos[2]) and self.is_close(w,pos[4]) and self.is_close(h,pos[5]):
                #same_height = True
                if y < pos[3]:
                    b_movement = True
                    print("moving")

        self.victim_pos = (victim,self.fnum, x,y,w,h)




    def is_close(self, num1, num2, marginal = 5):
        print(num1, num2)
        if num1 - num2 < marginal:
            close = True
        elif num2 - num1 < marginal:
            close = True
        else: 
            close = False
        return close



        




    def Color_victim2(self):
        image = self.image
        #    image = image.copy
        status = 0
        hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)

        lower_range = {
            "green": np.array([69,100,69]),
            "yellow": np.array([10,35,130]),
            "red" : np.array([130,110,60])
            }
        upper_range = {
            "green" : np.array([80,255,150]),
            "yellow" : np.array([69,100,255]),
            "red" : np.array([180,255,140])
            }
        for color in lower_range:
            lower = lower_range[color]
            upper = upper_range[color]
            mask = cv2.inRange(hsv,lower,upper)
            if color == "red":
                red2_lower =np.array([0,100,100]) 
                red2_upper =np.array([7,255,255]) 
                mask2 = cv2.inRange(hsv,red2_lower,red2_upper)
                mask = np.bitwise_or(mask,mask2)
            mask = self.blank_out(mask)
            self.ColVicP(mask, color)



    def ColVicP(self, mask,color):
        kernel = np.ones((9, 9), np.uint8) 
        mask = cv2.erode(mask,kernel, iterations=1)
        mask = cv2.dilate(mask,kernel, iterations=1) 

        if np.count_nonzero(mask) > 5000 and np.count_nonzero(mask)< 20000:
            #print(np.count_nonzero(mask))
            ret,thresh = cv2.threshold(mask, 40, 255, 0)
            contours, hierarchy = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
            c = max(contours, key = cv2.contourArea)
            mask = cv2.bitwise_and(self.image, self.image, mask=mask)
            if cv2.contourArea(c) > 5000:
                self.check_movement(c,color)
                print(f"{color}: {cv2.contourArea(c)}")
                if self.check_position(c):
                    x,y,w,h = cv2.boundingRect(c)
                    if x > 300: self.side = "l"
                    else: self.side = "r"
                    if color == "green": k = "k0"
                    else: k = "k1"
                    self.detected(k+self.side,victim=color)
                    self.log(color,img = mask)
                    logging.info(f"found {color}, image {self.fnum}")
                    print(f"found {color}, image {self.fnum}")
                else: 
                    print("outside possible frame")
                    self.log(color,img = mask)
                    logging.info(f"found {color}, image {self.fnum}, outside possible position")
        if self.showcolor: cv2.imshow(color, mask)





    def log(self, name, img=None):
        if img is None:
            img = self.image
        
        
        if self.logging:
            path = f'./log/{name}{self.fnum}.png'
            cv2.imwrite(path,img)

        
        






