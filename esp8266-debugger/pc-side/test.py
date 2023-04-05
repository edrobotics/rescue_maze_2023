test = "#s,wa,1,1,0\n"

test = test.replace("\n", "")


def convertMessage(message):
    if "#s,wa" in message:
        messageData = message[6:].split(",")
        print(messageData[0])
        print (messageData[1])
        message = f"{message[:5]}, F:{messageData[0]} , L:{messageData[2]} , R:{messageData[2]}"

    return message # Do I have to do this or is the message argument modified?


print(f"{convertMessage(test)}")
# test = convertMessage(test)