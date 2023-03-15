import serial
import time

# ser = serial.Serial('/dev/ttyUSB0', 9600, timeout=1)
ser = serial.Serial('COM5', 9600, timeout=1)
ser.reset_input_buffer()
time.sleep(1) # Delay because the arduino resets when you first open the terminal?


while True:
    command = input("Command: ")
    ser.write(command.encode(encoding="ascii"))
    ser.flush()
    while ser.in_waiting == 0:
        pass # Do nothing
    commandFinished = False
    while commandFinished != True:
        if (ser.in_waiting > 0):
            serialInput = ser.read(2).decode(encoding='ascii')
            # print(f"[DEBUGGING] {serialInput}")
            serialInput = serialInput.replace(",", "")
            print(f"[RECIEVED] {serialInput}")
            if (serialInput == "!a"):
                while (ser.in_waiting == 0):
                    pass
                ser.read() # Read the comma
                serialInput = ser.read()
                print(f"[DATA] {format(ord(serialInput), 'b')}")
                commandFinished = True
            elif (serialInput == "!s"):
                print("[COMMAND FINISHED SCCESSFULLY]")
                commandFinished = True
            elif (serialInput == "!f"):
                print("[COMMAND FAILED]")
                commandFinished = True
        
    while ser.in_waiting > 0: # Clear the input buffer
        ser.read()
    
