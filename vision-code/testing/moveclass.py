import testingclass as tc
import logging
import cv2
import os
import shutil
import traceback
import find_color_class as fcc 



position = []
def get_intensity(action,x, y, flags, *userdata):
    global position
    if action == cv2.EVENT_LBUTTONDOWN:
        position = (x,y)
        print(f"position: {position}")
        print(f"bgr: {image[y,x]}")
        print(f"hsv: {testing.hsv[y,x]} ")

def log_traceback(ex):
    tb_lines = traceback.format_exception(ex.__class__, ex, ex.__traceback__)
    tb_text = ''.join(tb_lines)
    # I'll let you implement the ExceptionLogger class,
    # and the timestamping.
    print(tb_text)

class move_c:

    def __init__(self,sourcedir, destdir):
        self.sourcedir = sourcedir
        self.destdir = destdir 
        self.TB = fcc.Trackbars() #trackbar object



    def keystrocks(self,image, filename):
        self.image = image
        self.filename = filename

        stop = False           
        folders = {
            "u":"U",
            "h":"H",
            "s":"S",
            "r":"red",
            "y":"yellow",
            "g":"green",
            "n":"none",
            "o":"other"
        }
        moved = False
        key = 0
        while key != 27:
            key = 0
            #cv2.imshow("window",testing.image_clone)
            key = cv2.waitKey(1)
            for folder in folders:
                if key == ord(folder):
                    self.move(folders[folder])
                    moved = True
            if key == 32 or key == ord("p"):
                break
            elif moved:
                break
            elif key == ord("t"):
                self.TB.create()
                self.TB.showimage(self.image)

                self.TB.clean_up()
                    
        if key == 27:
            cv2.destroyAllWindows()
            stop = True
            print("stoping")






    def move(self, type):
        source_path = os.path.join(self.sourcedir,self.filename)
        destination_path = os.path.join(self.destdir,type,self.filename)
        
        print(f"source: {source_path}")
        print(f"destination: {destination_path}")
        shutil.move(source_path, destination_path)

