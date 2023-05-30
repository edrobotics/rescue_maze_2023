#ifndef SERIALCOMM_H
#define SERIALCOMM_H
#include <Arduino.h>

// #define DEBUG_SERIAL_PORT Serial0
// #define serialdebug.print(x) DEBUG_SERIAL_PORT.print(x)

namespace serialcomm
{
    void returnSuccess();

    void returnFailure();

    void returnFloorColour(ColourSensor::FloorColour floorColour);

    void returnAnswer(int answer);

    char readChar();
    // Read a command following the outlined standard
    // Returns data of type Command.
    Command readCommand();

    Command readCommand(bool waitForSerial);

    void clearBuffer();

    bool checkInterrupt();

    void answerInterrupt();

    // Sends data that will be printed to the console / other debugging method
    // Should accept types in the same way that Serial.print() does.
    bool sendDebug();

    void sendLOP();
}

namespace serialdebug
{
    String test = "hello";
    void print(void);
    void print(int data);
    void print(double data);
    void print (__FlashStringHelper *data);

    Serial.println(test);

}





#endif