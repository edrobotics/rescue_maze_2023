import testingclass as tc
import logging
import cv2
import os
import shutil
import traceback
import find_color_class as fcc 



def log_traceback(ex):
    tb_lines = traceback.format_exception(ex.__class__, ex, ex.__traceback__)
    tb_text = ''.join(tb_lines)
    # I'll let you implement the ExceptionLogger class,
    # and the timestamping.
    print(tb_text)


def keystrocks():
    global stop           
    global file_name
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
                move(file_name,folders[folder])
                moved = True
        if key == 32 or key == ord("q"):
            break
#        elif key == ord("p"): print()
        elif moved:
            break
        elif key == ord("t"):
            TB.create()
            TB.showimage(testing.image)
            TB.clean_up()
                
    if key == 27:
        cv2.destroyAllWindows()
        stop = True
        print("stoping")


position = []
def get_intensity(action,x, y, flags, *userdata):
    global position
    if action == cv2.EVENT_LBUTTONDOWN:
        position = (x,y)
        print(f"position: {position}")
        print(f"bgr: {image[y,x]}")
        print(f"hsv: {testing.hsv[y,x]} ")



def move(file_name, type, source = None, base = None):
    global base_folder
    global source_path
    destination_path = os.path.join(base_folder,type,file_name)
    
    print(f"source: {source_path}")
    print(f"destination: {destination_path}")
    shutil.move(source_path, destination_path)


def showimage(victim,image):
    if GUI or len(testing.framedetected[0]) == 1:

        cv2.imshow("image",image)
        if len(victim) == 1:cv2.imshow("binary",testing.binary)
        colours = ("red","green","yellow")

        for colour in colours:
            cv2.imshow(colour,testing.masks[colour])
        keystrocks()            


def evaluatefolder(victim):
    print("evaluating", victim)
    global image
    global source_path
    global stop
    global file_name
    source_folder = os.path.join(base_folder,victim)
    file_list = os.listdir(source_folder)
    ct = 0
    for file_name in file_list:
        if stop:
            break
        ct += 1
#        print(file_name)
        source_path = os.path.join(source_folder, file_name)

        try:
            image = cv2.imread(source_path) 
            testing.do_the_work(image, file_name)
          
            if len(testing.framedetected) == 1:
                if victim != testing.framedetected[0]:
                    showimage(victim, testing.image_clone)
            elif len(testing.framedetected) == 0 and victim == "none":
                continue

            else:
                showimage(victim, testing.image_clone)

#        except IndexError:
#            print(file_name)
#            move(file_name,"problem")

        except Exception as e:
            print(file_name)
            logging.exception("exception in evaluate folder")
            log_traceback(e)
            
    print(f"{victim} was evaluated")
    tot = len(file_list)
    if ct <len(file_list):
        print(f"finished {ct} out of {tot} images")
    else:
        print(f"finished {tot} images")
    testing.ending()


base_folder = f"/Users/lukas/GitHub/rescue_maze_2023/vision-code/log/sorted3/"  # Specify the path of the source folder

if __name__ == '__main__':
    GUI = True
    #GUI = False
    stop = False
    logging.basicConfig(filename='vision.log', encoding='utf-8', level=logging.DEBUG)
    logging.info("started")
    TB = fcc.Trackbars() #trackbar object

    logging.basicConfig(filename='testing.log', encoding='utf-8', level=logging.DEBUG)
    logging.info("started")
    victim = input("victim to evaluate: ")
    windows = ("image","window", "binary", "red", "yellow","green")
    testing = tc.testing(victim, info=False,debugidentification=True)
    #debug = tc.testing(victim, info=False)
    base_folder = f"/Users/lukas/GitHub/rescue_maze_2023/vision-code/log/sorted3/"  # Specify the path of the source folder
    for window in windows:
        cv2.namedWindow(window)
        cv2.setMouseCallback(window, get_intensity)
        #print("created window", window)
        cv2.waitKey(1)
    victims = ("U","H","S", "red","yellow","green","none")
    if victim == "all":
        for victim in victims:
            evaluatefolder(victim)
            testing.clearstat()
            if stop:
                break

    else:
        if victim in victims:
            evaluatefolder(victim)
        else:
            print("invalid victim")
    cv2.destroyAllWindows()