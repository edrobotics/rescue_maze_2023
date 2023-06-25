import socket

HEADER = 16
PORT = 5050
SERVER = socket.gethostbyname(socket.gethostname())
ADDR = (SERVER, PORT)
FORMAT = 'utf-8'
DISCONNECT_MESSAGE = "!DISCONNECT"



def sendMessage(msg):
    message = msg.encode(FORMAT)
    msg_length = len(message).to_bytes(HEADER, "big")
    client.send(msg_length)
    client.send(message)

client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
client.connect(ADDR)

while True:
    messageToSend = input("Message: ")
    sendMessage(messageToSend)
    if (messageToSend == DISCONNECT_MESSAGE):
        break

