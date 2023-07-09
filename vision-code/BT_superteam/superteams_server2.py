#!/usr/bin/env python3
import bluetooth
import serial
import time


def start_BT():
    global client_sock
    global server_sock
    server_sock = bluetooth.BluetoothSocket(bluetooth.L2CAP)

    port = 0x1001

    server_sock.bind(("", port))
    server_sock.listen(1)


    client_sock, address = server_sock.accept()
    print("Accepted connection from", address)


def look_BT():
    print("looking for BT")
    data = client_sock.recv(1024)
    return data

def send_BT(kits):

#    client_sock.send("H")
#
#    while data != b"H":
 #       data = look_BT()
    client_sock.send(kits)



def close():
    client_sock.close()
    server_sock.close()




def handShake(port): #remove this maybe
    port.close()
    port.open()
    port.write(b'H')
    while port.read()!=b'A':
        port.write(b'H')
        print('Sending H')
    print('Received A')
    port.write(b'A')
    while port.read()!=b'S':
        pass
    print('Received S')
    print('Handshake Established')

def read_S():
    recv = port.read()
    return recv


if __name__ == '__main__':
    port = serial.Serial('/dev/ttyUSB0', baudrate=9600, timeout=3.0)
    time.sleep(1)
    start_BT()
#    handShake(port)
    while True: 
        print("looping")
        BT_s = look_BT()
        if len(BT_s) == 0: continue
        print(BT_s)
#        inputString = BT_s
#        inputChars = [char for char in inputString]
#        intToSend = ord(inputChars[0])
#        sendByte = intToSend.to_bytes(1, "big")
 #       print("Sending: " + chr(intToSend))
        port.write(BT_s)
        port.flush()
        while port.in_waiting == 0:
            pass
        if port.in_waiting > 0:
            serialInput = port.read()
            serialInput = int.from_bytes(serialInput, "big")
            print("recv: " + chr(serialInput))


            if serialInput == "a": send_BT("a")
            elif serialInput == "b": send_BT("b")
            elif serialInput == "c": send_BT("c")
            elif serialInput == "d": send_BT("d")




        #else: time.sleep(2)

#print("Data received:", str(data))
#send = " sending"
#while data:
#    data = client_sock.recv(1024)
#    print("Data received:", str(data))
#    if data == "q": break
#    res = input("respons please: ")
#    client_sock.send(res)
