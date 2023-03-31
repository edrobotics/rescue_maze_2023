#include <robot_lowlevel.h>
#include <colour_sensor.h>
#include <Arduino.h>
//#include <MeAuriga.h> //DO NOT INCLUDE!!! This was causing the problem all along! The only include of the MeAuriga.h is in robot_lowlevel.cpp. Investigate.
#include <Wire.h>
#include <SPI.h>

ColourSensor colourSensor;

void setup()
{
  // Init serial for debugging
  Serial.begin(9600);
  
  // Init hardware
  gyroInit();
  lightsAndBuzzerInit();
  encodersInit();
  initColourSensor();
  initSwitches();
  servoSetup();
  flushDistanceArrays();
  fillRampArrayFalse();
  // startDistanceMeasure(); // Why is this here?
  

  // Wait for beginning (to give time to remove hands etc.)
  delay(500);
  lights::activated();
  delay(100);
  serialcomm::sendLOP();
}

// Why are these here?
// double robotAngle = 0;
// double wallDistance = 0;

// #define PICODE
// #define TESTING_NAV
#define TESTING


void loop()
{

  #ifdef TESTING
 
  // Command command = serialcomm::readCommand();
  // if (command != command_invalid)
  // {
  //   delay(1000);
  //   serialcomm::returnSuccess();
  // }
  // else sounds::errorBeep();

  // delay(20);
  // serialcomm::clearBuffer();

  
  // ColourSensor::FloorColour identifiedColour = colourSensor.checkFloorColour();
  // // colourSensor.printRatios();
  // colourSensor.printColourName(identifiedColour);
  // colourSensor.printValues();
  // // Serial.println("");
  // delay(200);

  // driveStep();
  // delay(500);
  // lights::turnOff();
  // delay(500);
  // driveStep();
  // delay(500);
  // lights::turnOff();
  // delay(1500);
  // turnSteps(cw, 1);
  // delay(500);
  // turnSteps(ccw, 1);
  // delay(500);
  deployRescueKit();
  delay(2000);
  

  #else

  #ifdef PICODE
  if (Serial.available() > 0)
  {
    lights::turnOff();
  #endif
    #ifdef TESTING_NAV
    // For testing without the Pi:
    Command command = command_none;
    flushDistanceArrays();
    makeNavDecision(command);
    delay(555);
    lights::turnOff();
    #endif
    

    // Add some kind of error correction or checking?
    // Potential problem: Some bytes sent over serial may have some additional meaning, thus rendering them inappropriate for use by us
    // Use an enum instead of integers? The enum will be easier to decipher and the integer representation could still be the same
    #ifdef PICODE
    Command command = serialcomm::readCommand();
    // sounds::errorBeep();
    #endif
    switch (command)
    {
      // Driving
      case command_driveStep: // drive one step forward
      {
        serialcomm::returnSuccess(); // For validation and robustness of the communication protocol
        // lights::showDirection(lights::front);
        ColourSensor::FloorColour floorColourAhead = ColourSensor::floor_notUpdated;
        bool rampDriven = false;
        bool frontSensorDetected = false;
        bool commandSuccess = driveStep(floorColourAhead, rampDriven, frontSensorDetected);
        // bool commandSuccess = true;
        // lights::turnOff();
        if (floorColourAhead == ColourSensor::floor_black || frontSensorDetected == true) // floorColourAhead == ColourSensor::floor_blue ||  // Removed due to strategy change
        {
          lights::reversing();
          driveStep(); // For driving back
        }
        // If we have moved, mazenav has to know the new colour. If we have not moved, the colour is already known.
        if (commandSuccess == true) //  || floorColourAhead == ColourSensor::floor_black || floorColourAhead == ColourSensor::floor_blue // Removed because it would return success every time it saw blue or black, regardless if it had driven a step or not.
        {
          Serial.print("!a,");
          Serial.print(colourSensor.floorColourAsChar(floorColourAhead)); // If you have not driven back floorColourAhead will actually be the current tile
          Serial.print(',');
          Serial.print(rampDriven);
          Serial.println("");
          // serialcomm::returnFloorColour(floorColourAhead); // Interpreted as success by mazenav (except for black tile)
        }
        else
        {
          serialcomm::returnFailure();
        }
        
      }
        break;

      case command_driveBack: // drive one step backwards. Only used for testing/debugging
        driveBlind(-30, false);
        stopWheels();
        serialcomm::returnSuccess();
        break;

      case command_turnLeft: // turn counterclockwise one step
        // lights::showDirection(lights::left);
        turnSteps(ccw, 1);
        // sounds::tone(440, 100);
        // lights::turnOff();
        serialcomm::returnSuccess();
        break;

      case command_turnRight: // turn clockwise one step
        // lights::showDirection(lights::right);
        turnSteps(cw, 1);
        // sounds::tone(880, 100);
        // lights::turnOff();
        serialcomm::returnSuccess();
        break;
      
      // Sensors
      case command_getWallStates: // Send the current state of the walls to the maze code (raspberry). The form is 0bXYZ, where X, Y, Z are 0 or 1, 1 meaning the wall is present. X front, Y left, Z right.
        {
          uint8_t wallStates = getWallStates();
          serialcomm::returnAnswer(wallStates);
          break;
        }

      case command_dropKit:
        handleVictim(false);
        serialcomm::returnSuccess();
        break;

      case command_invalid:
        sounds::errorBeep();
        serialcomm::returnFailure();
        break;

      default:
        // Do nothing
        break;
      
    }
    serialcomm::clearBuffer();
  #ifdef PICODE
  }
  #endif

  #endif


}