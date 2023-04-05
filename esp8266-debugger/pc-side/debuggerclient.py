import asyncio
import time
import datetime
from websockets.sync.client import connect
import os

SERVER_HOSTNAME = "esp-debugger.local"
SERVER_PORT = 81

wallOrder = ["F","LF","LB","FR","RB"]

def convertMessage(message):
    if "#s,wa" in message:
        messageData = message[6:].split(",")
        message = f"{message[:5]}, F:{messageData[0]} , L:{messageData[1]} , R:{messageData[2]}"
    elif ("#s,u" in message) or ("#s,w" in message) or ("#w" in message):
        splitPoint = 6
        if "#w" in message:
            splitPoint = 5
        messageData = message[(splitPoint):].split(",")
        message = message[:(splitPoint-1)]
        for i in range(5):
            message += f", {wallOrder[i]}:{messageData[i]} " 
    else:
        pass

    return message # Do I have to do this or is the message argument modified?

    

currentTime = datetime.datetime.now()
formattedTime = str(datetime.datetime.date(currentTime)) + "_" + datetime.datetime.time(currentTime).strftime("%X")
formattedTime = formattedTime.replace(":", "-")
logFileName = "esp-log_" + formattedTime + ".log"
logFilePath = f"./logfiles/{logFileName}"
print(logFileName)
logFile = open(logFilePath, "a")
logFile.write("Log file created on " + formattedTime + "\n")

with connect("ws://" + SERVER_HOSTNAME + ":" + str(SERVER_PORT)) as webSocket:
    beginTime = time.time()
    try:
        while True:
            readMessage = webSocket.recv()
            # readMessage = readMessage.replace("\n", "")
            readMessage = "".join(readMessage.splitlines())
            readMessage = convertMessage(readMessage)
            timeFlag = time.time() - beginTime
            printMessage = f"[{timeFlag:.3f}]{readMessage}"
            # readMessage = readMessage.replace("\n", "")
            print(printMessage)
            logFile.write(printMessage + "\n")
    except KeyboardInterrupt:
        pass

logFile.close()
answer = input("Do you want to save log file? (Y/n)")
if answer == "n":
    os.remove(logFilePath)
