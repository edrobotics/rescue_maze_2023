#!/usr/bin/env python3
import serial
import time

ser = serial.Serial('/dev/ttyUSB0', 9600, timeout=1)
ser.reset_input_buffer()
time.sleep(1) # Delay because the arduino resets when you first open the terminal?

#sendInt = ord('A')
#sendByte = sendInt.to_bytes(1, "big")
#ser.write(sendByte)
#ser.flush()

while True:
    inputString = "A"
    inputChars = [char for char in inputString]
    intToSend = ord(inputChars[0])
    sendByte = intToSend.to_bytes(1, "big")
    print("Sending: " + chr(intToSend))
    ser.write(sendByte)
    ser.flush()
    while ser.in_waiting == 0:
        pass # Do nothing
    if ser.in_waiting > 0:
        serialInput = ser.read()
        serialInput = int.from_bytes(serialInput, "big")
        print("Recieving: " + chr(serialInput))
    
    
