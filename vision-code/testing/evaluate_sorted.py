import visionclass as tc
import logging
import cv2
import getlog
import t_adjust_wb as awb
import os

position = []


def get_intensity(action,x, y, flags, *userdata):
    global position
    if action == cv2.EVENT_LBUTTONDOWN:
        position = (x,y)
        print(f"position: {position}")
        print(f"bgr: {image[y,x]}")
    
        print(f"adjusted bgr: {adjusted[y,x]}")
        print(f"adjusted hsv: {adjusted_hsv[y,x]} ")


if __name__ == '__main__':
    folder = input("enter what folder you want to test: ")

    source_folder = f"/Users/lukas/GitHub/rescue_maze_2023/vision-code/log/sorted/{folder}/"  # Specify the path of the source folder

    #n = 0

    logging.basicConfig(filename='evaluate.log', encoding='utf-8', level=logging.DEBUG)
    logging.info("started")
    imgproc = tc.imgproc(show_visual=False,showcolor=True, debugidentification= False, showsource=True,connect=False,logging=False)
    adj = tc.imgproc(show_visual=False,showcolor=True, debugidentification= False, showsource=False,connect=False,logging=False)

    cv2.namedWindow("Window")
    cv2.setMouseCallback("Window", get_intensity)
    cv2.namedWindow("mouse")
    cv2.setMouseCallback("mouse", get_intensity)
    file_list = os.listdir(source_folder)

    # Iterate over each file in the list and copy it to the destination folder
    n = -1 
    while True:
        file_name = file_list[n]
        print(file_name)

        source_path = os.path.join(source_folder, file_name)
        image = cv2.imread(source_path)
        adjusted = adj.adjust_white_balance(image)
        adjusted_hsv =cv2.cvtColor(adjusted,cv2.COLOR_BGR2HSV)


        imgproc.do_the_work(image,n)
        key = 0
        while key !=27:
            cv2.imshow("Window", adjusted)
            #cv2.imshow("adjusted", adjusted)


            key = cv2.waitKey(1)
            if key == 3: 
                n = n + 1
                break
            elif key == 2:
                n = n - 1 
                break
        if key == 27:
            cv2.destroyAllWindows()
            break