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

void loop()
{
  
  if (Serial.available() >0)
  {
    char command = Serial.read();
    switch (command)
    {
      // Driving
      case 'w': // drive one step forward
        driveStep();
        serialcomm::returnSuccess();
        break;

      case 's': // drive one step backwards. Only used for testing/debugging
        driveBlind(-30, false);
        stopWheels();
        serialcomm::returnSuccess();
        break;

      case 'a': // turn counterclockwise one step
        turnSteps(ccw, 1);
        serialcomm::returnSuccess();
        break;

      case 'd': // turn clockwise one step
        turnSteps(cw, 1);
        serialcomm::returnSuccess();
        break;
      

      // Sensors
      case 'u': // Send the current state of the walls to the maze code (raspberry). 'u' for ultrasonic - bad name. Use 's' later?
        int wallStates = getWallStates();
        Serial.write(wallStates);
        break;

      // Rescue kits and victims
      case 'v':
        // The code for deployment goes here (structure planning not done yet)
        serialcomm::returnSuccess();
        break;
      default:
        break;
    }

  }


}