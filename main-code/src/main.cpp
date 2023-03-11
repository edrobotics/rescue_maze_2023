#include <robot_lowlevel.h>
#include <Arduino.h>
//#include <MeAuriga.h> //DO NOT INCLUDE!!! This was causing the problem all along! The only include of the MeAuriga.h is in robot_lowlevel.cpp. Investigate.
#include <Wire.h>
#include <SPI.h>


void setup()
{
  // Init serial for debugging
  Serial.begin(9600);
  Serial.println("");
  
  // Init hardware
  gyroInit();
  encodersInit();
  startDistanceMeasure();

  // Wait for beginning (to give time to remove hands etc.)
  delay(1000);
}

double robotAngle = 0;
double wallDistance = 0;

//#define PICODE
#define TESTING_NAV

void loop()
{

  #ifdef PICODE
  if (Serial.available() > 0)
  {
  #endif
    #ifdef TESTING_NAV
    // For testing without the Pi:
    int command = -1;
    makeNavDecision(command);
    delay(500);
    #endif
    

    // Add some kind of error correction or checking?
    // Potential problem: Some bytes sent over serial may have some additional meaning, thus rendering them inappropriate for use by us
    // Use an enum instead of integers? The enum will be easier to decipher and the integer representation could still be the same
    #ifdef PICODE
    int command = Serial.read();
    #endif
    switch (command)
    {
      // Driving
      case 0: // drive one step forward
        driveStep();
        serialcomm::returnSuccess();
        break;

      case 2: // drive one step backwards. Only used for testing/debugging
        driveBlind(-30, false);
        stopWheels();
        serialcomm::returnSuccess();
        break;

      case 1: // turn counterclockwise one step
        turnSteps(ccw, 1);
        serialcomm::returnSuccess();
        break;

      case 3: // turn clockwise one step
        turnSteps(cw, 1);
        serialcomm::returnSuccess();
        break;
      
      
      // Sensors
      case 4: // Send the current state of the walls to the maze code (raspberry). The form is 0bXYZ, where X, Y, Z are 0 or 1, 1 meaning the wall is present. X front, Y left, Z right.
        int wallStates = getWallStates();
        Serial.write(wallStates);
        break;
      
      /*
      // Rescue kits and victims
      case 'v':
        // The code for deployment goes here (structure planning not done yet)
        serialcomm::returnSuccess();
        break;
        */
      default:
        break;
    }
  #ifdef PICODE
  }
  #endif


}