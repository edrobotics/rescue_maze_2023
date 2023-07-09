#!/usr/bin/env python3
import visionclass as vc
import numpy as np
import cv2
import logging


class testing(vc.imgproc):
    def __init__(self,victim, showsource=False, showcolor=False, show_visual=False, logging=True, debugidentification=False, info=False, time=False, connect=False):
        self.clearstat()
        self.victim = victim
        super().__init__(logging = False, connect=False,info=info)

    def do_the_work(self, image, fnum):
        self.framedetected = []
        self.original_image = np.copy(image)

        self.image = self.adjust_white_balance(self.original_image)
        self.image_clone = self.image.copy()#image where lines and contours will be showed to
        self.fnum = fnum


        self.find_victim()
        self.Color_victim2()

        
        self.log("E")
        self.put_detected()


    def detected(self, msg, victim):
        self.ct_detected[victim][0] += 1
        self.framedetected.append(victim)


    def clearstat(self):
        self.ct_detected = {
            "H": [0,0,0],
            "U": [0,0,0],
            "S": [0,0,0],
            "green": [0,0,0],
            "yellow": [0,0,0],
            "red": [0,0,0]
        }


    def ending(self,):
        for victim in self.ct_detected:
            print(f"{victim}: {self.ct_detected[victim]}")



    def safeguards_color(self, contour, victim,mask):#reducing false identified colour victims
        correct = False
        text_pos = (10,470)
        (x,y,w,h) = cv2.boundingRect(contour)
        if x > 300: self.side = "l"
        else: self.side = "r"

        if True or self.find_edges(contour, mask, x,y,w,h): 
            if self.check_position(x,y,w,h):
                correct = True

            else:
                if self.info: print("to high or low")
                self.putText("wrong position",text_pos)
                self.ct_detected[victim][1] += 1

        else: 
            if self.info: print("no edges")
            self.putText("no edges",text_pos)
            self.ct_detected[victim][2] += 1
        return correct
            

    def put_detected(self):
        S_detected_victims = ""
        if len(self.framedetected) > 0:
            for victim in self.framedetected:
                S_detected_victims += f"{victim} "
        else:
            S_detected_victims = "No victims"

                
        pos = [10,20]
        self.putText(S_detected_victims,pos=pos)
