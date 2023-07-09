#!/usr/bin/env python3
"""PyBluez simple example l2capclient.py

Demo L2CAP client for bluetooth module.

$Id: l2capclient.py 524 2007-08-15 04:04:52Z albert $
"""

import sys

import bluetooth
import time


sock = bluetooth.BluetoothSocket(bluetooth.L2CAP)


bt_addr = "D8:3A:DD:27:13:D2"
port = 0x1001

print("Trying to connect to {} on PSM 0x{}...".format(bt_addr, port))

sock.connect((bt_addr, port))

print("Connected. Type something...")
while True:
    data = input()
    if not data:
        break
    inputString = data
    inputChars = [char for char in inputString]
    intToSend = ord(inputChars[0])
    sendByte = intToSend.to_bytes(1, "big")
    sock.send(data)
    data = sock.recv(1024)
    print("Data received:", str(data))

sock.close()