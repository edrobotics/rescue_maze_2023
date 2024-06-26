// This is the main program for the Auriga


#include <robot_lowlevel.h>
#include <colour_sensor.h>
#include <Arduino.h>
//#include <MeAuriga.h> //DO NOT INCLUDE!!! This was causing the problem all along! The only include of the MeAuriga.h is in robot_lowlevel.cpp. Investigate.
#include <Wire.h>
#include <SPI.h>

#define PICODE
// #define TESTING_NAV
// #define TESTING
// #define COLSENS_CALIBRATION

#ifdef COLSENS_CALIBRATION
#define TESTING
#endif

extern ColourSensor colSensor; // The colSensor object in robot_lowlevel.cpp
extern RobotPose pose;
extern double BASE_TURNING_SPEED;
extern double g_pidSetPoint;

HardwareButton colCalButton {30, false};

void setup()
{
  // Init serial for debugging
  #ifdef PICODE
  Serial.begin(9600);
  #else
  Serial.begin(9600);
  #endif
  
  // Init hardware
  lightsAndBuzzerInit();
  lights::activated();

  gyroInit();
  encodersInit();
  initColourSensor();
  servoSetup();
  flushDistanceArrays();
  fillRampArrayFalse();
  // startDistanceMeasure(); // Why is this here?

  // #warning Debugging
  // checkSmoothWallChanges();
  // checkPotWallChanges();
  

  // Wait for beginning (to give time to remove hands etc.)
  if (colCalButton.isPressed())
  {
    colSensor.clearCalibrationData();
    serialcomm::sendColourCal(); // So that Markus can start the scoring run timer
    while(colCalButton.isPressed())
    {
      colSensor.calibrationRoutineLoop();
    }
    delay(20);
    lights::turnOff();
  }
  delay(100);
  sounds::startupSound();
  serialcomm::sendLOP();
}

// Why are these here?
// double robotAngle = 0;
// double wallDistance = 0;




void loop()
{

  static bool shouldDelay = false;
  resetWallChanges();
  
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

  // lights::indicateBlueCircle();
  // lights::turnOff();
  // pose.update();
  // if (abs(pose.xDist - 15) > 3)
  // {
  // sideWiggleCorrection();
  // }
  // delay(2000);
  // lights::turnOff();

  // int multiplier = 1;
  // bool stopMoving = true;
  // double turnStepAngle = 10;
  // awareGyroTurn(-multiplier*turnStepAngle/2.0, stopMoving, 20, false, 0);
  // awareGyroTurn(-multiplier*turnStepAngle/2.0, stopMoving, 11, false, 15);
  // awareGyroTurn(multiplier*turnStepAngle*1.5, stopMoving, 20, false, 0);
  // awareGyroTurn(multiplier*turnStepAngle/2.0, stopMoving, 11, false, -15);
  // awareGyroTurn(-multiplier*turnStepAngle, true, 30, false, 0);
  // delay(1000);

  // getUltrasonics();
  // printUltrasonics();
  // startDistanceMeasure();
  // while(true)
  // {
  //   delay(20);
  //   loopEncoders();
  //   Serial.println(getDistanceDriven());
  // }
  driveStep();
  delay(2000);
  

  #else
  #ifdef PICODE
  if (shouldDelay==true)
  {
  delay(200);
  }
  lights::turnOff();
  #endif
  #ifdef TESTING_NAV
  // For testing without the Pi:
  Command command = command_none;
  flushDistanceArrays();
  makeNavDecision(command);
  // printUltrasonics();
  command = command_driveStep; // For debugging
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
    lights::turnOff();
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
        g_pidSetPoint = 15;
        // lights::showDirection(lights::front);
        bool continuing = false;
        bool rampDriven = false;
        FloorColour floorColourAhead = floor_notUpdated;
        double xDistanceOnRamp = 0;
        double yDistanceOnRamp = 0;
        driveStepBegin: // Label to be able to use goto statements
          TouchSensorSide frontSensorDetectionType = touch_none;
          bool commandSuccess = driveStep(floorColourAhead, rampDriven, frontSensorDetectionType, xDistanceOnRamp, yDistanceOnRamp, continuing);
          // bool commandSuccess = true;
          // lights::turnOff();
          if (floorColourAhead == floor_black || frontSensorDetectionType == touch_both)
          {
            lights::reversing();
            driveStep(); // For driving back
            // commandSuccess = true; // Not really, but Markus program wants it
          }

          if (frontSensorDetectionType == touch_left)
          {
            // Correct by turning right
            awareGyroTurn(-18, true, BASE_TURNING_SPEED, true, -5);
            awareGyroTurn(-8, true, BASE_TURNING_SPEED, true, 5);
            continuing = true;
            g_pidSetPoint = 20;
            goto driveStepBegin;
            
          }
          else if (frontSensorDetectionType == touch_right)
          {
            // Correct by turning left
            awareGyroTurn(18, true, BASE_TURNING_SPEED, true, -5);
            awareGyroTurn(8, true, BASE_TURNING_SPEED, true, 5);
            continuing = true;
            g_pidSetPoint = 10;
            goto driveStepBegin;
          }

          if (floorColourAhead == floor_blue)
          {
            lights::indicateBlueCircle();
          }
          else if (floorColourAhead == floor_reflective)
          {
            lights::indicateCheckpoint();
            
          }
          // Serial.print("Floor colour ahead: ");
          // Serial.println(floorColourAhead);
          // If we have moved, mazenav has to know the new colour. If we have not moved, the colour is already known.
          if (commandSuccess == true || (floorColourAhead == floor_black)) //  || floorColourAhead == floor_black || floorColourAhead == floor_blue // Removed because it would return success every time it saw blue or black, regardless if it had driven a step or not.
          {
            if (floorColourAhead == floor_black) rampDriven = false;
            Serial.print("!a,");
            Serial.print(colSensor.floorColourAsChar(floorColourAhead)); // If you have not driven back floorColourAhead will actually be the current tile
            Serial.print(',');
            if (rampDriven == true)
            {
              Serial.print("1,");
              Serial.print(xDistanceOnRamp, 0);
              Serial.print(',');
              Serial.print(yDistanceOnRamp, 0);
            } 
            else Serial.print("0");
            Serial.println("");
            // serialcomm::returnFloorColour(floorColourAhead); // Interpreted as success by mazenav (except for black tile)
          }
          else
          {
            serialcomm::returnFailure();
          }
          // if (pose.yDist > 15) pose.yDist -= 30; // Just set to 0 instead?
          pose.yDist = 0; // To prevent the robot driving too far? This could help
          shouldDelay = true;
          g_pidSetPoint = 15;
      }
        break;

      case command_driveBack: // drive one step backwards. Only used for testing/debugging
        shouldDelay = false;
        driveBlind(-30, false);
        stopWheels();
        serialcomm::returnSuccess();
        break;

      case command_turnLeft: // turn counterclockwise one step
        shouldDelay = false;
        serialcomm::returnSuccess();
        // lights::showDirection(lights::left);
        turnSteps(ccw, 1, BASE_TURNING_SPEED);
        
        // sounds::tone(440, 100);
        // lights::turnOff();
        serialcomm::returnSuccess();
        break;

      case command_turnRight: // turn clockwise one step
        shouldDelay = false;
        serialcomm::returnSuccess();
        // lights::showDirection(lights::right);
        turnSteps(cw, 1, BASE_TURNING_SPEED);
        // sounds::tone(880, 100);
        // lights::turnOff();
        serialcomm::returnSuccess();
        break;
      
      // Sensors
      case command_getWallStates: // Send the current state of the walls to the maze code (raspberry). The form is 0bXYZ, where X, Y, Z are 0 or 1, 1 meaning the wall is present. X front, Y left, Z right.
        {
          shouldDelay = false;
          uint8_t wallStates = getWallStates();
          serialcomm::returnAnswer(wallStates);
          break;
        }

      case command_dropKit:
        shouldDelay = false;
        serialcomm::returnSuccess();
        handleVictim(false);
        serialcomm::returnSuccess();
        break;

      case command_light:
        shouldDelay = false;
        // Execute the lighting (including delay)
        serialcomm::returnSuccess();
        lights::execLightCommand();
        lights::turnOff();
        serialcomm::returnSuccess();
        break;

      case command_invalid:
        shouldDelay = false;
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