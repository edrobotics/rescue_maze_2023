import socket
import threading

HEADER = 16
PORT = 5050
SERVER = socket.gethostbyname(socket.gethostname())
ADDR = (SERVER, PORT)
FORMAT = 'utf-8'
DISCONNECT_MESSAGE = "!DISCONNECT"

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind(ADDR)

def handleClient(connection, address):
    print(f"[NEW CONNECTION] {address} connected")

    connected = True
    while connected:
        message_length = connection.recv(HEADER) # The message recieved is the byte representation of what the client sends
        message_length = int.from_bytes(message_length, "big") # The client sent the length of the message, so we need to convert from bytes to an int
        if message_length: # If the message length is not 0
            message = connection.recv(message_length).decode(FORMAT)
            if (message == DISCONNECT_MESSAGE):
                connected = False
                print(f"[DISCONNECTED] {address}")
            else:
                print(f"[{address}] {message}")

    connection.close()


def startSocketServer():
    server.listen()
    print(f"[LISTENING] server is listening on {SERVER}:{PORT}")
    while True:
        connection, address = server.accept()
        thread = threading.Thread(target=handleClient, args = (connection, address))
        thread.start()
        print(f"[ACTIVE CONNECTIONS] {threading.activeCount() - 1}")

print("[STARTING] starting server...")
startSocketServer()