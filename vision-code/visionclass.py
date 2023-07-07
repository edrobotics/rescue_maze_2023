#!/usr/bin/env python3
#this is the code that processes the images and sends socket communication to the navigation code 
import numpy as np
import socket
import time
import cv2
import logging
import os
import pathlib


class imgproc:
    victim_pos = None

#find sample path
    cwd = os.getcwd()
    if cwd == '/Users/lukas/GitHub/rescue_maze_2023/vision-code':
        sampledir = "/Users/lukas/GitHub/rescue_maze_2023/vision-code/samples/"
        basefolder = "/Users/lukas/GitHub/rescue_maze_2023/vision-code/"
    else: 
        basefolder = "/home/theseus/rescue_maze_2023/vision-code/"
        sampledir = "/home/theseus/rescue_maze_2023/vision-code/samples/"


    #log directory

    def createfolder(self):  
        log = pathlib.Path(f"{self.basefolder}log")
        list = []
        for file in log.glob(f"log*"):
            list.append(file)
        print(len(list))
        self.log_folder = f"{self.basefolder}log/log{len(list)}"
        os.mkdir(self.log_folder)
    


#loading samples
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


    lastdetected = {
        "H": [None, -10, 0],
        "S": [None, -10, 0],
        "U": [None, -10, 0],
        "red": [None, -10, 0],
        "yellow": [None, -10, 0],
        "green": [None, -10, 0],
    }




#sends message to navigaion code using sockets
    def sendMessage(self,msg):
        if self.connected:
            if self.info: print("sending message", msg)
            logging.info(f"sending: {msg}")

            try:
                message = msg.encode(self.FORMAT)
                msg_length = len(message).to_bytes(self.HEADER, "big")
                self.client.send(msg_length)
                self.client.send(message)
            except Exception as e:
                if self.info: print("failed to send messsage")
                logging.exception("failed to send message")
                self.connect(once = True, msg = msg)
#connects to navigation code
    def connect(self, once = False, msg = None):
        connect = True
        while connect:
            if once: connect = False
            try:
                self.HEADER = 16
                PORT = 4242
                SERVER = socket.gethostbyname(socket.gethostname())
                ADDR = (SERVER, PORT)
                self.FORMAT = 'utf-8'
                DISCONNECT_MESSAGE = "!DISCONNECT"
                self.client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                self.client.connect(ADDR)
            except:
                print("failed")
                if not once: time.sleep(2)
                if msg: self.sendMessage(msg)
            else:
                print("Connected")
                self.connected = True
                break


    def __init__(self, showsource= False,showcolor = False, show_visual = False, logging = True, debugidentification = False, info = True, time=False, connect = True):
        self.showsource = showsource
        self.showcolor = showcolor
        self.show_visual = show_visual
        self.logging = logging
        self.debugidentification = debugidentification
        self.info = info
        self.time = time

        if connect:
            self.connect()
        else: self.connected = False
        if logging: self.createfolder()
    

    
    
    

    def adjust_white_balance(self, image_s, percent_red=0, percent_blue=0):
        image = image_s.copy()
        image = self.blank_out(image)

        half1 = image.copy()
        half2 = image.copy()
        half1[:,:320] = (0,0,0)
        half2[:,320:] = (0,0,0)
        timg = (half1, half2)
        adjusted_image = np.zeros((480,640,3), np.uint8)
        for half in timg:
            b, g, r = cv2.split(half)
            avg_b = np.mean(b)
            avg_g = np.mean(g)
            avg_r = np.mean(r)
            
            adj_b = b * (avg_g / avg_b) * (100 - percent_blue) / 100
            adj_r = r * (avg_g / avg_r) * (100 - percent_red) / 100
            
            adj_b = np.clip(adj_b, 0, 255).astype(np.uint8)
            adj_r = np.clip(adj_r, 0, 255).astype(np.uint8)

            half = cv2.merge([adj_b, g, adj_r])
            adjusted_image = adjusted_image + half
            
        if self.showsource:
            cv2.imshow("adjusted",adjusted_image)
        return adjusted_image       




    def do_the_work(self, image, fnum):
        self.fnum = fnum
        self.framedetected = {}
        self.original_image = np.copy(image)
        self.image = self.adjust_white_balance(self.original_image)
        self.image_clone = self.image.copy()#image where lines and contours will be showed to
        self.log("E")
        try: 
            self.find_victim()
            self.Color_victim2()
        except Exception as ex:
            logging.exception("exception in class")

        finally:
            self.evaluate_detected()



        if self.showsource:
            cv2.imshow("image_clone",self.image_clone)



    def detected(self,msg, victim): #makes the victim only send once every victim detection
        if self.info: print(f"{victim} detected")
        self.framedetected[victim] = msg




    def evaluate_detected(self):
        send = False
        if len(self.framedetected) > 0:

            for victim in self.framedetected:
                msg = self.framedetected[victim]
                
                list = self.lastdetected[victim]
                if list[1] + 2 < self.fnum:
                    send = True
                else:
                    if self.info: print("alredy detected")
                #updates last detected
                break
            if send:
                if len(self.framedetected) >= 1:
                    self.sendMessage(msg)



            
            S_detected_victims = ""
            for victim in self.framedetected:
                new_list = (msg, self.fnum, list[2]+ 1)
                self.lastdetected[victim] = new_list
                S_detected_victims += f"{victim} " 
            
                  
            pos = (10,20)
            self.putText(S_detected_victims,pos=pos)

            


            

         


    def blank_out(self, img33):
        #blanks out pixels that can't be poi but can still make problems
        if img33.ndim >= 3:
            _, _, x = np.shape(img33)
            if x == 3:
                n = (0,0,0)
            else:
                if self.info: print("something wrong in blankout")
                if self.info: print(x)
                n = (0)
        else: 
            n = (0)

        img33[:, 280:360] = n
        img33[:, 590:] = n
        img33[:, :15] = n

        img33[:30, :] = n
        img33[450:, :] = n
        return img33


    def preproccesing(self,img): #for visual victim identification
        gray = cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
        blurred = cv2.GaussianBlur(gray, (7, 7), 0)
        binary = cv2.adaptiveThreshold(blurred,255,cv2.ADAPTIVE_THRESH_MEAN_C, cv2.THRESH_BINARY,21,10)
        binary = np.invert(binary)
        binary = self.blank_out(binary)
        self.binary = binary
        return binary


    def find_victim(self): #for visual victims

        img = self.image
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
                self.putTextPos = (minx, maxy + 5)
                width = maxx - minx
                height = maxy - miny
                if width < 42: continue
                if height < 42: continue

                dsize = (200,200)
                RImgCnt = cv2.resize(imgCnt, dsize)
                if width < height: RImgCnt = cv2.rotate(RImgCnt, cv2.ROTATE_90_CLOCKWISE)
                if self.show_visual:
                    cv2.imshow("imgCnt",RImgCnt)

                if maxx < 320: self.side ="r"
                else: self.side = "l"

                result = self.identify_victim2(RImgCnt)
#                self.identify_victim2(RImgCnt)
                if result[0]:
                    break

        return result 


    def identify_victim2(self,ivictim): #compares binary poi with binary sample images different way to count similarities          
        x = -1
        identified = False
        victim = None
        kits = None
        sim = [0,0]
        for key in self.Dictand:
            x = x + 1 
            sample = self.Dictand[key]

            for i in range(2):
                if i == 1: ivictim = cv2.rotate(ivictim,cv2.ROTATE_180)
                M_AND = cv2.bitwise_and(sample, ivictim)
                M_AND_C = np.count_nonzero(M_AND)
                M_OR = cv2.bitwise_and(self.Dictor[key], ivictim)
                M_OR_C = np.count_nonzero(M_OR)
                Max_and = np.count_nonzero(sample)
                Max_or = np.count_nonzero(ivictim)
                sim_and = M_AND_C/Max_and
                sim_or = M_OR_C/Max_or
                if sim_and > sim[0] and sim_or > sim[1]:
                    sim[0] = sim_and
                    sim[1] = sim_or
                    victim = key

                if self.debugidentification: # show masks
                    cv2.imshow("M_AND", M_AND)                
                    cv2.imshow("ivictim",ivictim)                
                    cv2.imshow("M_OR",M_OR)                
                    cv2.imshow("dictand",sample)                
                    cv2.imshow("dictor",self.Dictor[key])                
                    cv2.waitKey(0)

  
        if sim[0] + sim[1] > 1.91:
            identified = True

        if victim: kits = self.how_many_kits(victim)
        
        self.putText(f"{victim}{sim[0]:.2f}, {sim[1]:.2f}")

        return (identified,kits, victim)
    
    def how_many_kits(self,victim):
        dict = {
            "H": 3,
            "S": 2,
            "U": 0
        }
        kits = dict[victim]
        return kits
    
    def putText(self, text, pos = None):
        if pos == None:
            bottomLeftCornerOfText = self.putTextPos
        else:
            bottomLeftCornerOfText = pos
        font                   = cv2.FONT_HERSHEY_SIMPLEX
        fontScale              =  1/2
        color                  = (255,255,255)
        thickness              =   1
        try:
            cv2.putText(self.image_clone,text,bottomLeftCornerOfText,font,fontScale,color,thickness)
        except Exception as ex:
            logging.exception("could not put text")
            logging.debug(f"textpos: {pos}")
            print("failed putting text")
    

    
    def safeguards_color(self, contour, victim,mask):#reducing false identified colour victims
        correct = False
        text_pos = (10,470)
        (x,y,w,h) = cv2.boundingRect(contour)
        if x > 300: self.side = "l"
        else: self.side = "r"

        if True or self.find_edges(contour, mask, x,y,w,h):
            if self.check_position(x,y,w,h):
                if True or self.check_movement(victim,x,y,w,h): #evaluate and remove
                    correct = True
                else: 
                    print("something of in multiframe safeguard")
                    self.putText("not moving",text_pos)

            elif self.info: 
                print("to high or low")
                self.putText("wrong position",text_pos)

        else: 
            if self.info: 
                print("no edges")
                self.putText("no edges",text_pos)
        return correct



    def check_position(self, x,y,w,h): # makes sure the victim is on the right height
        b_position = None
        victimheight = 185
        victimheight2 = 460
        if victimheight > x and victimheight - 40 < x + w:
            b_position = True
        elif victimheight2 > x and victimheight2 - 40 < x + w:
            b_position = True

        width1 = 69
        width2 = 420
        if y < width1 or y + h > width2: #FIX VALUES  
            b_position = False
        #draw lines where it can't be
        cv2.line(self.image_clone, (0,width1),(640,width1),(0,0,200), 1)
        cv2.line(self.image_clone, (0,width2),(640,width2),(0,0,200), 1)

        
        cv2.line(self.image_clone, (victimheight,0),(victimheight,480),(0,200,0), 1)
        cv2.line(self.image_clone, (victimheight2,0),(victimheight2,480),(0,200,0), 1)

        return b_position
 
    def check_movement(self,victim,x,y,w,h):
        b_movement = None
        if self.victim_pos:
            pos = self.victim_pos

            if self.is_close(x, pos[2]) and self.is_close(w,pos[4]) and self.is_close(h,pos[5]):
                same_height = True
#                b_movement = True #only for testing
                if y < pos[3]:
                    b_movement = True
                else: 
                    if self.info: print("not moving ;)")
            else: 
                if self.info: print("change in position/shape")


        self.victim_pos = (victim,self.fnum, x,y,w,h)

        return b_movement




    def is_close(self, num1, num2, marginal = 5):
        close = False
        if num1 > num2:
           if num1 - num2 < marginal:
                close = True
            
        elif num2 > num1:
            if num2 - num1 < marginal:
                close = True
        else: 
            close = True
        return close


        
#safe guard to make sure the found mask is actually a contour
    def find_edges(self,contour,mask, x,y,w,h):
        print("achtung achtung, das ist nicht gut")
        contours_match = False
        image = np.copy(self.image)
        if x < 20:
            x = 20
        if y < 20:
            y = 20

        aoi = image[y -20 : y+h+20 ,x-20: x+w+ 20]
        aoi2 = np.copy(aoi)
        aoi3 = np.copy(aoi)
        binary = mask[y -20 : y+h+20 ,x-20: x+w+ 20]
        kernel =np.ones((9,9),np.uint8)
        img_gray = cv2.cvtColor(aoi, cv2.COLOR_BGR2GRAY)
        img_blur = cv2.GaussianBlur(img_gray, (7,7), 1) 
        img_blur = cv2.morphologyEx(img_blur, cv2.MORPH_CLOSE, kernel)
        edges = cv2.Canny(image=img_blur, threshold1=10, threshold2=20)
        edges = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, kernel)

        contours, hierarchy = cv2.findContours(edges,cv2.RETR_LIST,cv2.CHAIN_APPROX_SIMPLE)
        contours2, hierarchy = cv2.findContours(binary,cv2.RETR_LIST,cv2.CHAIN_APPROX_SIMPLE)
        for cnt in contours2: #loops trough all contours on the mask (should only be one)


            #gets approximation points 
            epsilon = 0.05*cv2.arcLength(cnt,True)
            approx = cv2.approxPolyDP(cnt,epsilon,True)
            points = len(approx)
            if points <3:
                continue
            cv2.drawContours(aoi2, [approx], -1, (0,0,255), 3)

            for cnt in contours :

                epsilon = 0.05*cv2.arcLength(cnt,True)
                approx2 = cv2.approxPolyDP(cnt,epsilon,True)
                cv2.drawContours(aoi3, [approx2], -1, (0,0,255), 3)

                #compares approximation points with canny image
                ct = 0 
                for point in approx:

                    for point2 in approx2:
                        if self.is_close(point[0][0], point2[0][0], marginal=20) and self.is_close(point[0][1], point2[0][1], marginal=20):

                            ct += 1
                if ct > points -2:
                    contours_match = True
                    break


 #       self.test_find_edges(contour,mask,x,y,w,h)
        return contours_match







    def Color_victim2(self):
        image = self.image
        #    image = image.copy
        status = 0
        hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
        self.hsv = hsv.copy()
        self.masks = {}

        lower_range = {
            "yellow": np.array([20,50,100]),#decrese Saturation? 
            "red" : np.array([130,69,100]), #increase saturation >100
            "green": np.array([42,50,70]) #decrese V 50 
            }
        upper_range = {
            "yellow" : np.array([35,255,255]),
            "red" : np.array([180,255,255]),
            "green" : np.array([90,255,255])
            }
        for color in lower_range:
            lower = lower_range[color]
            upper = upper_range[color]
            mask = cv2.inRange(hsv,lower,upper)
            if color == "red":
                red2_lower =np.array([0,120,60]) #0,100,100?
                red2_upper =np.array([7,255,255]) 
                mask2 = cv2.inRange(hsv,red2_lower,red2_upper)
                mask = np.bitwise_or(mask,mask2)
            mask = self.blank_out(mask)
            self.ColVicP(mask, color)


#analysing the contours 
    def ColVicP(self, mask,color):
        kernel = np.ones((9, 9), np.uint8) 
        mask = cv2.erode(mask,kernel, iterations=1)
        mask = cv2.dilate(mask,kernel, iterations=1) 
        log_mask = cv2.bitwise_and(self.image, self.image, mask=mask)

        if np.count_nonzero(mask) > 5000 and np.count_nonzero(mask)< 20000:
            #print(np.count_nonzero(mask))
            ret,thresh = cv2.threshold(mask, 40, 255, 0)# is this necessary?
            contours, hierarchy = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
            c = max(contours, key = cv2.contourArea)
            if cv2.contourArea(c) > 5000:
                if self.info: print("possible vicitm detcted")

                if self.safeguards_color(c, color, mask):


                    if self.info: print(f"{color}: {cv2.contourArea(c)}")
                    if color == "green": k = "k0"
                    else: k = "k1"
                    self.detected(k+self.side,victim=color)
                    logging.info(f"found {color}, image {self.fnum}")
                    if self.info: print(f"found {color}, image {self.fnum}")
                    self.log(color,img = log_mask)
                else: 
                    if self.info: print("stoped by safeguard")
                    self.log(f"F{color}",img = log_mask)
                    logging.info(f"found {color}, image {self.fnum}, but was stopped by safeguards")




        if self.showcolor: cv2.imshow(color, log_mask)
        self.masks[color] = log_mask





    def log(self, name, img=None):#logging the images
        try:
            if img is None:
                img = self.original_image
    
            if self.logging:
                path = f'{self.log_folder}/{name}{self.fnum}.png'
                cv2.imwrite(path,img)
        except Exception as ex:
            logging.exception("couldn't log")

        
        






