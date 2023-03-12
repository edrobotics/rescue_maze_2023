import serial
import time
import socket
import threading


# For the socket server
HEADER = 16 # Could probably be a lot smaller
PORT = 5050
SERVER = socket.gethostbyname(socket.gethostname())
ADDR = (SERVER, PORT)
FORMAT = 'utf-8'
DISCONNECT_MESSAGE = "!DISCONNECT"

# Configuration (startup?) of the socket server
server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind(ADDR)

# Opening the serial port
ser = serial.Serial('/dev/ttyUSB0', 9600, timeout=1)
ser.reset_input_buffer()
time.sleep(1) # Delay because the arduino resets when you first open the terminal?

def sendSerial(command):
   command = ord(command)
   sendByte = command.to_bytes(1, "big")
   ser.write(sendByte)
   ser.flush()


def startSocketServer():
    server.listen()
    print(f"[LISTENING] server is listening on {SERVER}:{PORT}")
    while True:
        connection, address = server.accept() # Code should wait here for connection
        thread = threading.Thread(target=handleClient, args = (connection, address))
        thread.start()
        print(f"[ACTIVE CONNECTIONS] {threading.activeCount() - 2}")

def handleClient(connection, address):
    print(f"[NEW CONNECTION] {address} connected")

    connected = True
    while connected: # Each loop recieves a message consisting of a header and the message itself
        message_length = connection.recv(HEADER) # The message recieved is the byte representation of what the client sends. Code should wait here?
        message_length = int.from_bytes(message_length, "big") # The client sent the length of the message, so we need to convert from bytes to an int
        if message_length: # If the message length is not 0
            message = connection.recv(message_length).decode(FORMAT)
            if (message == DISCONNECT_MESSAGE):
                connected = False
                print(f"[DISCONNECTED] {address}")
            else:
                print(f"[{address}] {message}")

    connection.close()

# Start the socket server in its own thread
print("[STARTING] starting server...")
socketThread = threading.Thread(target=startSocketServer)
socketThread.start()

# The main loop of the program
while True:
    pass