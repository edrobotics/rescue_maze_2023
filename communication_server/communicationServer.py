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


def startSocketServer():
    server.listen()
    print(f"[LISTENING] server is listening on {SERVER}:{PORT}")
    while True:
        conn, addr = server.accept() # Code should wait here for connection
        thread = threading.Thread(target=handleClient, args = (conn, addr))
        thread.start()
        print(f"[ACTIVE CONNECTIONS] {threading.activeCount() - 1}")

def handleClient(connection, address):
    print(f"[NEW CONNECTION] {address} connected")

    connected = True
    while connected: # Each loop recieves a message consisting of a header and the message itself
        readSocketCommand(connection, address)
        # message_length = connection.recv(HEADER) # The message recieved is the byte representation of what the client sends. Code should wait here?
        # message_length = int.from_bytes(message_length, "big") # The client sent the length of the message, so we need to convert from bytes to an int
        # if message_length: # If the message length is not 0
        #     message = connection.recv(message_length).decode(FORMAT)
        #     if (message == DISCONNECT_MESSAGE):
        #         connected = False
        #         print(f"[DISCONNECTED] {address}")
        #     else:
        #         print(f"[{address}] {message}")

    connection.close()

# A socket command is:
# !<type>, <args>

def readSocketCommand(connection, address):
    message_length = connection.recv(HEADER) # The message recieved is the byte representation of what the client sends. Code should wait here?
    message_length = int.from_bytes(message_length, "big") # The client sent the length of the message, so we need to convert from bytes to an int
    if message_length: # If the message length is not 0
        message = connection.recv(message_length).decode(FORMAT) # Reads the entire message
        if (message == DISCONNECT_MESSAGE):
            connected = False
            print(f"[DISCONNECTED] {address}")
        else:
            print(f"[{address}] {message}") # Used for debugging
            if (message[0] != "!"):
                # The read sring was not a command
                print(f"[ERROR] Not a valid command: {message}")
                return False
            message = message[1:] # Remove the exclamation mark
            message = message.split(",") # Split the string and save it to a list
            command = message[0]
            print(f"[COMMAND] {command}") # Debugging
            if (command == "driveStep"):
                pass
            elif (command == "getVictimStates"):
                pass


# Start the socket server in its own thread
print("[STARTING] starting server...")
startSocketServer()