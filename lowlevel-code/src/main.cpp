#include <robot_lowlevel.h>
#include <colour_sensor.h>
#include <Arduino.h>
//#include <MeAuriga.h> //DO NOT INCLUDE!!! This was causing the problem all along! The only include of the MeAuriga.h is in robot_lowlevel.cpp. Investigate.
#include <Wire.h>
#include <SPI.h>

extern ColourSensor colSensor; // The colSensor object in robot_lowlevel.cpp

HardwareButton colCalButton {32, false};

void setup()
{
  // Init serial for debugging
  Serial.begin(9600);
  
  // Init hardware
  gyroInit();
  lightsAndBuzzerInit();
  encodersInit();
  initColourSensor();
  servoSetup();
  flushDistanceArrays();
  fillRampArrayFalse();
  // startDistanceMeasure(); // Why is this here?
  

  // Wait for beginning (to give time to remove hands etc.)
  delay(500);
  lights::activated();
  if (colCalButton.isPressed())
  {
  colSensor.clearCalibrationData();
  serialcomm::sendLOP(); // So that Markus can start the scoring run timer
  while(colCalButton.isPressed())
  {
    colSensor.calibrationRoutineLoop();
  }
  lights::turnOff();
  }
  delay(100);
  serialcomm::sendLOP();
}

// Why are these here?
// double robotAngle = 0;
// double wallDistance = 0;

#define PICODE
// #define TESTING_NAV
// #define TESTING
// #define COLSENS_CALIBRATION

#ifdef COLSENS_CALIBRATION
#define TESTING
#endif


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

  #ifdef COLSENS_CALIBRATION
  ColourSensor::FloorColour identifiedColour = colSensor.checkFloorColour();
  colSensor.printRatios();
  colSensor.printClearVal();
  colSensor.printColourName(identifiedColour);
  // colSensor.printValues();
  Serial.println("");
  delay(200);

  #else

  // deployRescueKit();
  // delay(2000);

  // getUltrasonics();
  // checkWallPresence();
  
  // driveStep();
  // Serial.println("");Serial.println("------------------------------------------------------------------------------");Serial.println("");
  
  // delay(1500);
  // lights::turnOff();
  // delay(500);

  // IMPORTANT!!! ----------------------------------------------------------------------------------------------------------------------------------------------------------------
  // I tried to change the usmt

  #endif

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
  
  

  #else

  #ifdef PICODE
  lights::turnOff();
  #endif
  #ifdef TESTING_NAV
  // For testing without the Pi:
  Command command = command_none;
  flushDistanceArrays();
  makeNavDecision(command);
  printUltrasonics();
  // command = command_driveStep; // For debugging
  delay(555);
  lights::turnOff();
  #endif
  

  // Add some kind of error correction or checking?
  // Potential problem: Some bytes sent over serial may have some additional meaning, thus rendering them inappropriate for use by us
  // Use an enum instead of integers? The enum will be easier to decipher and the integer representation could still be the same
  #ifdef PICODE
  if (Serial.available() != 0)
  {
    Command command = serialcomm::readCommand(false);
    // sounds::errorBeep();
    #endif
    switch (command)
    {
      // case command_none:
      //   // sounds::tone(440, 50);
      //   lights::noComm();
      //   break;
      // Driving
      case command_driveStep: // drive one step forward
      {
        serialcomm::returnSuccess(); // For validation and robustness of the communication protocol
        // lights::showDirection(lights::front);
        bool continuing = false;
        bool rampDriven = false;
        ColourSensor::FloorColour floorColourAhead = ColourSensor::floor_notUpdated;
        double xDistanceOnRamp = 0;
        double yDistanceOnRamp = 0;
        driveStepBegin: // Label to be able to use goto statements
          TouchSensorSide frontSensorDetectionType = touch_none;
          bool commandSuccess = driveStep(floorColourAhead, rampDriven, frontSensorDetectionType, xDistanceOnRamp, yDistanceOnRamp, continuing);
          // bool commandSuccess = true;
          // lights::turnOff();
          if (floorColourAhead == ColourSensor::floor_black || frontSensorDetectionType == touch_both)
          {
            lights::reversing();
            driveStep(); // For driving back
            // commandSuccess = true; // Not really, but Markus program wants it
          }

          if (frontSensorDetectionType == touch_left)
          {
            // Correct by turning right
            awareGyroTurn(-18, true, -5);
            continuing = true;
            goto driveStepBegin;
            
          }
          else if (frontSensorDetectionType == touch_right)
          {
            // Correct by turning left
            awareGyroTurn(18, true, -5);
            continuing = true;
            goto driveStepBegin;
          }

          if (floorColourAhead == ColourSensor::floor_reflective)
          {
            lights::indicateCheckpoint();
            
          }
          // Serial.print("Floor colour ahead: ");
          // Serial.println(floorColourAhead);
          // If we have moved, mazenav has to know the new colour. If we have not moved, the colour is already known.
          if (commandSuccess == true || (floorColourAhead == ColourSensor::floor_black)) //  || floorColourAhead == ColourSensor::floor_black || floorColourAhead == ColourSensor::floor_blue // Removed because it would return success every time it saw blue or black, regardless if it had driven a step or not.
          {
            Serial.print("!a,");
            Serial.print(colSensor.floorColourAsChar(floorColourAhead)); // If you have not driven back floorColourAhead will actually be the current tile
            Serial.print(',');
            if (rampDriven == true)
            {
              Serial.print("1,");
              Serial.print(xDistanceOnRamp);
              Serial.print(',');
              Serial.print(yDistanceOnRamp);
            } 
            else Serial.print("0");
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