from processing import *




if __name__ == '__main__':
    n = 0


    #camera starts here
    camera = PiCamera()
    camera.resolution = (640, 480)
    camera.framerate = 10
    #camera.shutter_speed = 10000
    #camera.iso = 800
    rawCapture = PiRGBArray(camera, size=(640, 480))
    for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
        image = frame.array
        log(image, "E", n)
        rawCapture.truncate(0)
        find_colour_victim(image,n)
        find_visual_victim(image,n)
        try:
            if not ssh: cv2.imshow("frame", image)
            pass
        except:
            ssh = True
            showcolor = False
        #key = cv2.waitKey(1)
    #  if key == 27:
    #     break
        n += 1
