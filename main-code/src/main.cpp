#include <robot_lowlevel.h>
#include <Arduino.h>
//#include <MeAuriga.h> DO NOT INCLUDE!!! This was causing the problem all along! The only include of the MeAuriga.h is in robot_lowlevel.cpp. Investigate.
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

  // Wait for beginning (to give time to remove hands etc.)
  delay(1000);
}

void loop()
{
  driveBlind(30);
  stopWheels();
  delay(500);
  turnSteps(cw, 1);
  delay(500);
}