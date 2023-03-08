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
    case 'w':
      //driveStep();
      driveStep();
      break;
    case 's':
      driveBlind(-30, false);
      stopWheels();
      break;
    case 'a':
      turnSteps(ccw, 1);
      break;
    case 'd':
      turnSteps(cw, 1);
      break;
    default:
      break;
    }
  }


}