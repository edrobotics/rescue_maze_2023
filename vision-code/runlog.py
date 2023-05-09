from processing import * 
from multiprocessing import Process
import time

if __name__ == '__main__': 
    print("start")
    logging.basicConfig(filename='example.log', encoding='utf-8', level=logging.DEBUG)
    logging.info("started")
    n = 0 
    while True: 
        start_time = time.time()
        path = f"./log/E{n}.png"
        img = cv2.imread(path)
        logging.info(f'started image {n}')
        fcv = Process(target = find_colour_victim, args=(img,n))
        fvv = Process(target = find_visual_victim, args=(img,n))
        fcv.start()
        fvv.start()
        fcv.join()
        fvv.join()
        print(time.time() - start_time)
        

        n += 1