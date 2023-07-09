#!/usr/bin/env python3
#this is the code that processes the images and sends socket communication to the navigation code 
import numpy as np
import socket
import time
import cv2
import logging
import os
import pathlib
import bluetooth



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
            
    def socket_recv(self):
        logging.info("waiting for data")
        recv = self.client.recv()
        if len(recv) > 0:
            pass
        else: 
            if recv == b'a': kits = 1
            elif recv == b'b': kits = 2
            elif recv == b'c': kits = 3
            else: 
                kits = 3
                print("wrong input")
                print(recv)
                logging.info(f"{recv}")
            msg = f"d{kits}"
            self.sendMessage(msg)
        #s
    
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
        self.B_putText = False
        self.connect_BT()

        if connect:
            self.connect()
        else: self.connected = False
        if logging: self.createfolder()
    

    
    
    def connect_BT(self):
        print("connecting BT")
        self.sock = bluetooth.BluetoothSocket(bluetooth.L2CAP)
        bt_addr = "D8:3A:DD:27:13:D2"
        port = 0x1001
        self.sock.connect((bt_addr, port))


    def BT_interact(self):
        
        recv = self.sock.recv(1024)



        if self.ToSend:
            self.sock.send(self.ToSend)




    def do_the_work(self, image, fnum):
        self.fnum = fnum
        self.framedetected = {}
        self.original_image = np.copy(image)
        self.image = image
        #self.image = self.adjust_white_balance(self.original_image)
        self.image_clone = self.image.copy()#image where lines and contours will be showed to
        self.log("E")

        try: 
            self.find_victim()
            #self.Color_victim2()
        except Exception as ex:
            logging.exception("exception in class")

        finally:
            if self.client.recv() == "9":
                self.BT_interact()
#            self.evaluate_detected()



#        if self.showsource:
#            cv2.imshow("image_clone",self.image_clone)




    ToSend = ""
    def detected(self,msg, victim): #makes the victim only send once every victim detection
        if self.info: print(f"{victim} detected")
        self.framedetected[victim] = msg
        self.ToSend = self.how_many_kits2(victim)




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
            "H": "3",
            "S": "2",
            "U": "1"
        }
        kits = dict[victim]
        return kits
    def how_many_kits2(self,victim):

        dict = {
            "H": "c",
            "S": "b",
            "U": "a"
        }
        kits = dict[victim]
        return kits
    
    def putText(self, text, pos = None):
        if self.B_putText:
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






    def log(self, name, img=None):#logging the images
        try:
            if img is None:
                img = self.original_image
    
            if self.logging:
                path = f'{self.log_folder}/{name}{self.fnum}.png'
                cv2.imwrite(path,img)
        except Exception as ex:
            logging.exception("couldn't log")

        
        




