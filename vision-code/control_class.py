import visionclass as tc
from picamera.array import PiRGBArray
from picamera import PiCamera
import logging
import cv2

ssh = False

if __name__ == '__main__':
    n = 0
    logging.basicConfig(filename='log/vision.log', encoding='utf-8', level=logging.DEBUG)
    logging.info("started")
#initating class
    imgproc = tc.imgproc()

    #camera starts here
    camera = PiCamera()
    camera.resolution = (640, 480)
    camera.framerate = 10
    camera.awb_mode = 'off'
    rg, bg = (1.8, 1.42)
    camera.awb_gains = (rg, bg)
    rawCapture = PiRGBArray(camera, size=(640, 480))
    for frame in camera.capture_continuous(rawCapture, format="bgr", use_video_port=True):
        image = frame.array
        try:
            imgproc.do_the_work(image,n)
        except Exception as e: 
            print(e)
            print("something went wrong in do the work")
            logging.exception("something went wrong")
        rawCapture.truncate(0)
        try:
            if not ssh: 
                cv2.imshow("frame", image)
        except:
            ssh = True
            showcolor = False
            print("failed showing image")
        key = cv2.waitKey(1)
        if key == 27:
            cv2.destroyAllWindows()
            break
        n += 1
