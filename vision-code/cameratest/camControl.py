import io
import time
import threading
import picamera

import numpy as np
import cv2

# Create a pool of image processors
done = False
lock = threading.Lock()
pool = []
callback = None

#width = 1280
#height = 720

#width = 1648
#height = 928

#width = 1296
#height = 736

width = 640
height = 480

class ImageProcessor(threading.Thread):
    def __init__(self, vFlip=True, showSource=False):
        threading.Thread.__init__(self)
        self.showSource = showSource
        self.image = np.empty((height, width, 3), dtype=np.uint8)
        self.event = threading.Event()
        self.terminated = False
        self.start()
    def run(self):
        # This method runs in a separate thread
        global done, callback
        while not self.terminated:
            if self.event.wait(1):
                try:
                    if self.showSource:
                        cv2.imshow("source", self.image)
                        cv2.waitKey(1)
                    if callback is not None:
                        callback(self.image)
                finally:
                    # Reset the event
                    self.event.clear()
                    # Return ourselves to the pool
                    with lock:
                        pool.append(self)
starving = False
def streams():
    while not done:
        with lock:
            if pool:
                processor = pool.pop()
            else:
                processor = None
        if processor:
            starving = False
            yield processor.image
            processor.event.set()
        else:
            if not starving:
                print("waiting for image processor")
                starving = True
            time.sleep(0.001)


class camControl(threading.Thread):
    def __init__(self, vflip=False, showSource=False):
        threading.Thread.__init__(self)
        self.showSource = showSource 
        self.vflip = vflip
        self.start()

    def registerCallback(self, cb):
        global callback
        callback = cb

    def run(self):
        global pool, width, height
        with picamera.PiCamera() as camera:
            pool = [ImageProcessor(showSource=self.showSource) for i in range (4)]
            print("pool={}".format(len(pool)))
            camera.resolution = (width, height)
            # Set the framerate appropriately; too fast and the image processors
            # will stall the image pipeline and crash the script
            camera.framerate = 10
            camera.raw_format = 'bgr'
            camera.resolution = (width, height)
            camera.vflip = self.vflip
            #camera.start_preview()
            print("Calibrating camera")
            time.sleep(3)
            print("Camera calibration done")
            camera.exposure_mode = 'off'
            camera.capture_sequence(streams(), format="bgr", use_video_port=True)

        # Shut down the processors in an orderly fashion
        while pool:
            with lock:
                processor = pool.pop()
            processor.terminated = True
            processor.join()

    def stop(self):
        global done
        done = True

