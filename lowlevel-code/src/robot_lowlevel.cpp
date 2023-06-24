// Provides most functions for main.cpp

#include <robot_lowlevel.h>
#include <colour_sensor.h>
// #include <ultrasonic_sensor.h>
#include <Arduino.h>
#include <MeAuriga.h>
#include <Servo.h>
#include <FastLED.h> // For the camera light
// #include <MeEncoderOnBoard.h>
#include <MeGyro.h>
#include <Wire.h>
#include <SPI.h>

// Encoder motor definitions
// Make an array of them? Could I then call encoderArray.loop() to loop all sensors?
MeEncoderOnBoard encoderLF(SLOT2); // Left front encoder motor
MeEncoderOnBoard encoderLB(SLOT3); // Left back encoder motor
MeEncoderOnBoard encoderRF(SLOT1); // Right front encoder motor
MeEncoderOnBoard encoderRB(SLOT4); // Right back encoder motor


// Servo
Servo servo;

// Ultrasonic sensor definitions
MeUltrasonicSensor ultrasonicF(PORT_6); // Front
MeUltrasonicSensor ultrasonicLF(PORT_8); // Left front
MeUltrasonicSensor ultrasonicLB(PORT_9); // Left back
MeUltrasonicSensor ultrasonicRF(PORT_7); // Right front
MeUltrasonicSensor ultrasonicRB(PORT_10); // Right back
MeUltrasonicSensor ultrasonicSensors[] =
{
  ultrasonicF,
  ultrasonicLF,
  ultrasonicLB,
  ultrasonicRF,
  ultrasonicRB
};


RGBColour colourBlack {0, 0, 0};
RGBColour colourWhite {100, 100, 100};
RGBColour colourBase {5, 42, 0};
RGBColour colourOrange {42, 5, 0};
RGBColour colourRed {150, 0, 0};
RGBColour colourBlue {0, 0, 150};
RGBColour colourError {200, 10, 0};
RGBColour colourAffirmative { 20, 150, 0};
RGBColour colourYellow {50, 50, 0};
RGBColour colourPurple {100, 0, 150};


// Colour sensor:
ColourSensor colSensor;

// Gyro definition
MeGyro gyro(0, 0x69);

// Buzzer (for debugging)
MeBuzzer buzzer;

// RGB ring
MeRGBLed ledRing(0, 12);

const double MAX_CORRECTION_DISTANCE = 16;
const double MIN_CORRECTION_ANGLE = 8;
const double MAX_WALLCHANGE_ANGLE = 25; // Should be changed later to compute the actual distances

#warning untuned constants
const double MAX_ULTRASONIC_FAR = 120; // Max distance to use differences in front ultrasonic sensor distance
const double MAX_ULTRASONIC_NEAR = 20; // Max distance to use absolute value of front ultrasonic sensor
const double MAX_FRONT_DIFFERENCE = 3; // The maximum allowed difference between two front ultrasonic readings
const double MAX_FRONT_CORRECTION_DISTANCE = 9; // The maximum allowed difference between pose and corrected pose when the front ultrasonic sensor gets involved.
const double MAX_FRONT_ANGLE = 7; // The maximum allowed angle for the front sensor to be used. Untuned constant
#warning If stopping too early, either ignore or drive back saying it is blocked (could be an obstacle)


/*
enum TurningDirection {
    cw,
    ccw,
};
enum WheelSide {
    wheels_left,
    wheels_right,
};
*/


//---------------------- Variable definitions ----------------------------//

// Wheel and wheelbase dimensions (all in cm)
const double WHEEL_DIAMETER = 6.8;
const double WHEEL_CIRCUMFERENCE = PI*WHEEL_DIAMETER;

// Driving
const double CMPS_TO_RPM = 1.0/WHEEL_CIRCUMFERENCE*60.0; // Constant to convert from cm/s to rpm
const double BASE_SPEED_CMPS = 15; // The base speed of driving (cm/s)
double g_baseSpeed_CMPS = BASE_SPEED_CMPS; // The speed to drive at
const double BASE_SPEED_RPM = CMPS_TO_RPM*g_baseSpeed_CMPS; // The base speed of driving (rpm)
double g_trueDistanceDriven = 0; // The correct driven distance. Measured as travelling along the wall and also updated when landmarks are seen
double g_targetDistance = 0; // The distance that you want to drive
double g_startDistance = 0; // The distance that you start from

// For potential wallchanges.
// First dimension: which sensor to use. ultrasonic_F (0) is unused
// Second dimension: wallchange_approaching or wallchange_leaving
// Should initialize all structs empty
PotWallChange g_potWallChanges[ULTRASONIC_NUM][2] {};
WallChangeType g_smoothWallChanges[ULTRASONIC_NUM] {};

// The offset from what should be correct when correcting with wallchanges

// Sensor constants
const double ULTRASONIC_FORWARDOFFSET = 7.3;
const double ULTRASONIC_SPACING = ULTRASONIC_FORWARDOFFSET*2; // The distance between the centers two ultrasonic sensors.
#warning uncalibrated constants
const double ULTRASONIC_SIDEOFFSET = 7;
const double ULTRASONIC_FRONT_OFFSET = 9.5; // The distance from the front sensor to the center of the robot
const double ULTRASONIC_DISTANCE_TO_WALL = 7.1; // The distance between the ultrasonic sensor (edge of the robot) and the wall when the robot is centered.
const double WALL_PRESENCE_TRESHOLD = 20; // Not calibrated !!!!!!!!!!!!!!!!!!!!!!!! Just a guess!!!!!!!!!!!!!!!!!!!!!!!
const double FRONT_WALL_STOPPING_TRESHOLD = 15 - ULTRASONIC_FRONT_OFFSET + 1.8;

// Wallchange offsets for original wheels
// double wallChangeOffsets[wcoff_num] =
// {
//   3.5,  // frontLeaving
//   -0.5, // frontApproaching
//   3,    // backLeaving
//   0     // backApproaching
// };

// Wallchange offsets for neoprene foam wheels
double wallChangeOffsets[wcoff_num] =
{
  2.3,  // frontLeaving
  -1.2, // frontApproaching
  2,    // backLeaving
  -0.5     // backApproaching
};

const double BACK_WALLCHANGE_DISTANCE = 15 + ULTRASONIC_SPACING/2.0;
const double FRONT_WALLCHANGE_DISTANCE = 15 - ULTRASONIC_SPACING/2.0;

// Array containing the order in which the sensors should be called
UltrasonicSensorEnum ultrasonicCallingOrder[ULTRASONIC_NUM] =
{
  ultrasonic_LF,
  ultrasonic_RB,
  ultrasonic_RF,
  ultrasonic_LB,
  ultrasonic_F,
};

// Array containing the raw distances of the sensors. It keeps the latest values (how many is determined by (DISTANCE_MEASUREMENT_SIZE))
// First dimension: Which ultrasonic sensor to use
// Second dimension: Which value to access
double ultrasonicRawDistances[ULTRASONIC_NUM][DISTANCE_MEASUREMENT_SIZE]{}; // Initializes all distances to 0
// This array contains the pointers for keeping track of which value in the raw distance array is the latest.
int ultrasonicRawDistancesPointer[ULTRASONIC_NUM] {};

// Contains the most recent raw and smooth distances
// First dimension: Which sensor to use
// Second dimension: Which data type to use (usmt_raw or usmt_smooth)
double ultrasonicCurrentDistances[ULTRASONIC_NUM][USMT_NUM] {};
double ultrasonicLastKnownDistances[ULTRASONIC_NUM][USMT_NUM] {};
double ultrasonicTheoreticalDistances[ULTRASONIC_NUM] {};

// Whether or not a wall is present at the currenct execution of checkSmoothWallChanges
// First dimension: Which sensor to use
// Second dimension: Which data type to use (usmt_raw or usmt_smooth)
bool currentSensorWallStates[ULTRASONIC_NUM][USMT_NUM];

// Whether or not a wall was present at the previous execution of checkSmoothWallChanges
// First dimension: Which sensor to use
// Second dimension: Which data type to use (usmt_raw or usmt_smooth)
bool previousSensorWallStates[ULTRASONIC_NUM][USMT_NUM] {};
// Double check that this sets them to false



// Wall presence (deprecated)
// bool leftWallPresent = false;
// bool rightWallPresent = false;
// bool frontWallPresent = false;

// Normal wall presence. True if both smooth values are below treshold.
// The dimension is the wall side, using the WallSide enum.
bool normalWallPresence[3] {};

// Safe wall presence. True only if raw and smooth values are below treshold.
// The dimension is the wall side, using the WallSide enum.
bool safeWallPresence[3] {};

// For gyro turning and angles
// double g_currentGyroAngle = 0; // The current angle value for the gyro, but converted to a mathangle
// double g_targetGyroAngle = 0;
// double g_lastWallAngle = 0; // Used to determine the angle in relation to the wall when a move ends
// double g_robotAngle = 0; // The (current) angle of the robot in relation to the wall
// double g_wallDistance = 0; // The (current) angle of the robot to the wall
// double g_startWallAngle = 0;
// double g_gyroOffset = 0;
RobotPose pose;

// For driving decision (driving backwards)
bool g_driveBack = false;
ColourSensor::FloorColour g_floorColour = ColourSensor::floor_notUpdated;
TouchSensorSide g_lastTouchSensorState = touch_none;

int g_kitsToDrop = 0;
char g_dropDirection = ' ';
bool g_returnAfterDrop = false;


//------------------ Serial communication -------------------------------//

void serialcomm::returnSuccess()
{
  Serial.println("!s"); // Success or Finished
  Serial.flush();
}

void serialcomm::returnFailure()
{
  Serial.println("!f"); // Failed
  Serial.flush();
}

void serialcomm::returnAnswer(int answer)
{
  Serial.print("!a,");
  Serial.print(answer);
  Serial.println("");
  // Serial.write('\n');
  Serial.flush();
}

void serialcomm::returnFloorColour(ColourSensor::FloorColour floorColour)
{
  switch (floorColour)
  {
    case ColourSensor::floor_black:
      returnAnswer('s');
      break;
    case ColourSensor::floor_blue:
      returnAnswer('b');
      break;
    case ColourSensor::floor_reflective:
      returnAnswer('c');
    default:
      returnAnswer(0); // If some error occured
      break;
  }
}

// Deprecated
// char serialcomm::readChar()
// {
//   while (Serial.available() == 0) {}
//   return static_cast<char>(Serial.read());
// }

void serialcomm::clearBuffer()
{
  delay(5);
  while (Serial.available() > 0) {
    Serial.read();
    delay(5);
  }
}

Command serialcomm::readCommand(bool waitForSerial)
{
  if (waitForSerial==false) return readCommand(waitForSerial, 1000);
  else return readCommand(waitForSerial, 5000); // 5000ms is the longest time to wait for Serial communication.
}

char lightCommandChar = ' ';
bool g_turboSpeed = false;

Command serialcomm::readCommand(bool waitForSerial, int timeout)
{
  if (waitForSerial == false && Serial.available() < 1) return command_none; // Return none if Serial was not available and you should not wait
  else if (waitForSerial == true)
  {
    long timeFlag = millis();
    while (Serial.available() == 0 && (millis()-timeFlag < timeout)) {} // Wait while no serial is available. When continuing serial will be available
    if (Serial.available() == 0) return command_none; // if the timout has passed and serial is still not available
  }
  String readString = Serial.readStringUntil('\n');
  int strIdx = 0;
  if (readString.charAt(strIdx) != '!') return command_invalid;
  ++strIdx;
  switch (readString.charAt(strIdx))
  {
    case 'i': // interrupt the current action
      return command_interrupt;
      break;

    case 'd': // driveStep
      ++strIdx;
      if (readString.charAt(strIdx) != ',')
      {
        // g_turboSpeed = false;
        // return command_driveStep;
        return command_invalid;
      }
      ++strIdx;
      if (readString.charAt(strIdx) == '1') g_turboSpeed = true;
      else g_turboSpeed = false;

      return command_driveStep;
      break;

    case 't': // turn
      ++strIdx;
      if ( readString.charAt(strIdx) != ',' ) return command_invalid; // Invalid because the form was not followed
      ++strIdx;

      if (readString.charAt(strIdx) == 'l') return command_turnLeft;
      else if (readString.charAt(strIdx) == 'r') return command_turnRight;
      else return command_invalid;
      break;

    case 'k': // drop rescue kit
    {
        ++strIdx;
        g_kitsToDrop = 0;
        // sounds::tone(220, 300);
        if (readString.charAt(strIdx) != ',' ) return command_invalid; // Invalid because the form was not followed
        ++strIdx;
        g_kitsToDrop = readString.charAt(strIdx) - '0';
        ++strIdx;
        // sounds::tone(440, 300);

        if (readString.charAt(strIdx) != ',' ) return command_invalid; // Invalid because the form was not followed
        ++strIdx;
        // sounds::tone(695, 300);
        g_dropDirection = readString.charAt(strIdx);
        ++strIdx;
        // sounds::tone(880, 700);

        if (readString.charAt(strIdx) != ',' ) return command_invalid; // Invalid because the form was not followed
        ++strIdx;
        if (readString.charAt(strIdx)=='0')
        {
          g_returnAfterDrop = false;
          lights::setColour(9, colourBlue, true);
        }
        else if (readString.charAt(strIdx)=='1')
        {
          g_returnAfterDrop = true;
          lights::setColour(9, colourRed, true);
        }
        else
        {
          g_returnAfterDrop = true;
          Serial.println("!e");
          lights::setColour(0, colourError, true);
          sounds::errorBeep();
        }

        return command_dropKit;
        break;
    }
    case 'w': // get wall states
      return command_getWallStates;
      break;


    case 'r': // resume the action interrupted by interrupt
      break;

    case 'b':
      ++strIdx;
      if (readString.charAt(strIdx) != ',' ) return command_invalid; // Invalid because the form was not followed
      ++strIdx;
      lightCommandChar = readString.charAt(strIdx);
      return command_light;
      break;

    default:
      sounds::errorBeep();
      return command_invalid;
      // break;
  }
  clearBuffer();
}

// Command serialcomm::readCommand()
// {
//   return serialcomm::readCommand(true);
// }


bool serialcomm::checkInterrupt()
{
  Command command = serialcomm::readCommand(false);
  if (command == command_interrupt) return true;
  else return false;
}

void serialcomm::answerInterrupt(int stepDriven)
{
  Serial.print("!a,i,");
  Serial.print(stepDriven);
  Serial.println("");
  Serial.flush();
}

void serialcomm::cancelInterrupt()
{
  Serial.println("!c,i");
}

//---------------------- Buzzer and lights (for debugging) ------------------//

const int CAMERA_LED_PIN = 34;
const int CAMERA_LED_NUM = 12;
CRGB cameraLeds[CAMERA_LED_NUM];

void lightsAndBuzzerInit()
{
  buzzer.setpin(45);
  ledRing.setpin(44);
  ledRing.fillPixelsBak(0, 2, 1);
  lights::turnOff();

  FastLED.addLeds<NEOPIXEL, CAMERA_LED_PIN>(cameraLeds, CAMERA_LED_NUM);
  for (int i=0; i<CAMERA_LED_NUM; ++i)
  {
    cameraLeds[i].r = 255;
    cameraLeds[i].g = 160;
    cameraLeds[i].b = 100;
  }
  FastLED.show();
}



void lights::turnOff()
{
  setColour(0, colourBlack, true);
  // ledRing.setColor(colourBlack.red, colourBlack.green, colourBlack.blue);
  // ledRing.show();
}

// Same as show() in library
void lights::showCustom()
{
  ledRing.show();
}

void lights::setColour(int index, RGBColour colour, bool showColour)
{
  ledRing.setColor(index, colour.red, colour.green, colour.blue);
  if (showColour==true) ledRing.show();
}

RGBColour lights::safeMltp(RGBColour base, double multiplier)
{
  RGBColour returnColour = colourBlack;
  if (multiplier<0) return returnColour;

  returnColour.red = base.red * multiplier;
  if (returnColour.red>255) returnColour.red = 255;
  returnColour.green = base.green * multiplier;
  if (returnColour.green>255) returnColour.green = 255;
  returnColour.blue = base.blue * multiplier;
  if (returnColour.blue>255) returnColour.blue = 255;
  return returnColour;
}

void lights::setColour(int index, RGBColour colour, double intensity, bool showColour)
{
  setColour(index, safeMltp(colour, intensity), showColour);
}

void lights::execLightCommand()
{
  switch (lightCommandChar)
  {
    case 'a':
      break;
    case 'b':
      break;
  }
}

void lights::showDirection(lights::LedDirection direction, RGBColour colour)
{
  turnOff();
  int centerLEDIndex = 3*direction + 3;
  for (int i=0;i<3;++i)
  {
    int ledIndex = centerLEDIndex - 1 + i;
    if (ledIndex > 12)
    {
      ledIndex -= 12;
    }
    setColour(ledIndex, colour, false);
    // ledRing.setColor(ledIndex, colourBase.red, colourBase.green, colourBase.blue);
  }
  ledRing.show();
}

void lights::showDirection(lights::LedDirection direction)
{
  showDirection(direction, colourBase);
}

void lights::fastBlink(RGBColour colour)
{
  turnOff();
  for (int i=0;i<3;++i)
  {
    delay(130);
    setColour(0, colour, true);
    // ledRing.setColor(colourBase.red, colourBase.green, colourBase.blue);
    // ledRing.show();
    delay(60);
    turnOff();
  }

}

void lights::affirmativeBlink()
{
  fastBlink(colourAffirmative);
}

void lights::negativeBlink()
{
  fastBlink(colourError);
}

void lights::noComm()
{
  setColour(0, colourError, true);
  delay(50);
  turnOff();
}

// Plays a light sequence and also plays the buzzer
void lights::activated()
{
  turnOff();
  for (int i=12; i>0; --i)
  {
    int ledIndex = i+3;
    if (ledIndex > 12) ledIndex -= 12;
    setColour(ledIndex, colourAffirmative, true);
    delay(30);
  }
  delay(30);
  turnOff();
  delay(70);
  for (int i=0;i<3;++i)
  {
    turnOff();
    delay(80);
    setColour(0, colourAffirmative, true);
    buzzer.tone(660, 70);
    delay(30);
  }
  delay(150);
  turnOff();
}

void lights::floorIndicator(ColourSensor::FloorColour floorColour)
{
  switch (floorColour)
  {
    case ColourSensor::floor_black:
      showDirection(front, colourRed);
      break;
    case ColourSensor::floor_blue:
      showDirection(front, colourBlue);
      break;
    default:
      showDirection(front, colourWhite);

  }


  if (floorColour == ColourSensor::floor_black)
  {
  }
  else if (floorColour == ColourSensor::floor_blue)
  {
  }
}

void lights::turnOnVictimLights(bool show)
{
    setColour(0, colourWhite, false);
    setColour(6, colourRed, false);
    setColour(9, colourRed, false);
    setColour(12, colourRed, show);

    // 2, 3, 4
}

void lights::indicateFrontSensor()
{
  turnOff();
  setColour(2, colourRed, false);
  setColour(4, colourRed, true);
}

void lights::reversing()
{
  setColour(8, colourWhite, false);
  setColour(10, colourWhite, true);
}


void lights::onRamp()
{
  setColour(2, colourBlue, false);
  setColour(4, colourBlue, false);
  setColour(8, colourBlue, false);
  setColour(10, colourBlue, true);
}


void lights::indicateCheckpoint()
{
  for (int k=0; k<3; ++k)
  {
    for (int i=1; i<=11; i += 2)
    {
      setColour(i, colourWhite, false);
      setColour(i+1, colourYellow, true);
    }
    delay(200);
    turnOff();
    delay(200);

  }
}

void lights::rampDriven()
{
  setColour(9, colourPurple, true);
}

void sounds::errorBeep()
{
  for (int i=0;i<3;++i)
  {
    buzzer.tone(555, 200);
    delay(200);
  }
}

void sounds::tone(int freq, int duration)
{
  buzzer.tone(freq, duration);
}

int lights::safeIndex(int index)
{
  while(index<1) {index += 12;}
  while(index>12) {index -= 12;}
  return index;
}

long circleTimeFlag = 0;
int leadIndex = 3; // Between 1 and 12
void lights::circleLoop(RGBColour colour1, RGBColour colour2, double speed)
{
  if (millis()-circleTimeFlag > 1000.0/(12.0*speed)) // Doing it like this instead of with a delay makes it more accurate (can stop more exactly)
  {
    setColour(0, colourBlack, false);
    for (int i=0;i<12;i+=3)
    {
    setColour(safeIndex(leadIndex+i), colour1, false);
    // setColour(safeIndex(leadIndex+2+i), colour2, false);
    }
    showCustom();
    leadIndex = safeIndex(leadIndex+1);
    circleTimeFlag = millis();
  }
}

void lights::circle(RGBColour colour1, RGBColour colour2, double speed, int duration)
{
  long timeFlag = millis();
  while (millis()-timeFlag < duration)
  {
    circleLoop(colour1, colour2, speed);
  }
  turnOff();
}

void lights::indicateBlueCircle()
{
  lights::circle(colourBlue, colourBlack, 1, 5500);
}


//------------------ Low level encoder functions ---------------------------//

// ISR processes (to run when an interrupt is called) for the encoder motors
void isr_encoderLF(void)
{
    if(digitalRead(encoderLF.getPortB()) == 0) encoderLF.pulsePosMinus();
    else encoderLF.pulsePosPlus();
}
void isr_encoderLB(void)
{
    if(digitalRead(encoderLB.getPortB()) == 0) encoderLB.pulsePosMinus();
    else encoderLB.pulsePosPlus();
}
void isr_encoderRF(void)
{
    if(digitalRead(encoderRF.getPortB()) == 0) encoderRF.pulsePosMinus();
    else encoderRF.pulsePosPlus();
}
void isr_encoderRB(void)
{
    if(digitalRead(encoderRB.getPortB()) == 0) encoderRB.pulsePosMinus();
    else encoderRB.pulsePosPlus();
}

// Loop all encoders
void loopEncoders()
{
    encoderLF.loop();
    encoderLB.loop();
    encoderRF.loop();
    encoderRB.loop();
}


// Most of (all in the future?) the code needed to initialize the encoders
// Should be run inside of setup()
void encodersInit()
{

  attachInterrupt(encoderLF.getIntNum(), isr_encoderLF, RISING);
  attachInterrupt(encoderLB.getIntNum(), isr_encoderLB, RISING);
  attachInterrupt(encoderRF.getIntNum(), isr_encoderRF, RISING);
  attachInterrupt(encoderRB.getIntNum(), isr_encoderRB, RISING);


  //Set PWM 8KHz
  TCCR1A = _BV(WGM10);
  TCCR1B = _BV(CS11) | _BV(WGM12);

  TCCR2A = _BV(WGM21) | _BV(WGM20);
  TCCR2B = _BV(CS21);

  encoderLF.setPulse(9);
  encoderLB.setPulse(9);
  encoderRF.setPulse(9);
  encoderRB.setPulse(9);
  encoderLF.setRatio(39.267);
  encoderLB.setRatio(39.267);
  encoderRF.setRatio(39.267);
  encoderRB.setRatio(39.267);
  encoderLF.setPosPid(0.18,0,0);
  encoderLB.setPosPid(0.18,0,0);
  encoderRF.setPosPid(0.18,0,0);
  encoderRB.setPosPid(0.18,0,0);
  encoderLF.setSpeedPid(0.18,0,0);
  encoderLB.setSpeedPid(0.18,0,0);
  encoderRF.setSpeedPid(0.18,0,0);
  encoderRB.setSpeedPid(0.18,0,0);
}

//-------------- Slightly higher level encoder functions ----------------------------------//

// Stops all wheels (and waits for them to stop)
void stopWheels()
{
  encoderLF.runSpeed(0);
  encoderLB.runSpeed(0);
  encoderRF.runSpeed(0);
  encoderRB.runSpeed(0);
  while(encoderLF.getCurrentSpeed() !=0 || encoderLB.getCurrentSpeed() !=0 || encoderRF.getCurrentSpeed() !=0 || encoderRB.getCurrentSpeed() !=0) {
    loopEncoders();
  }
}

// Moves the specified wheel side some distance.
// wheelSide - wheels_left or wheels_right (a group of 2 encoder motors)
// distance - the distance the wheel will move in cm. Positive is forward and negative is backwards
// speed - the speed the wheel will move at in cm/s. Always positive.
//TODO: It drives a bit too far. Maybe make it slow down in the end?
void moveWheelSide(WheelSide wheelSide, double distance, double speed)
{
  speed *= CMPS_TO_RPM;
  distance*=360/WHEEL_CIRCUMFERENCE; // Converts the distance from cm to degrees
  if (wheelSide==wheels_left) {
    encoderLF.speedMove(distance, speed);
    encoderLB.speedMove(distance, speed);
  } else if (wheelSide==wheels_right) {
    encoderRF.speedMove(-distance, speed);
    encoderRB.speedMove(-distance, speed);
  }
}


// Runs the specified wheel side at the specified speed
// wheelSide - wheels_left or wheels_right
// speed - the speed in cm/s to run the side at. Positive will move the robot forward
void runWheelSide(WheelSide wheelSide, double speed)
{
  speed *= CMPS_TO_RPM; // Convert speed from cm/s to rpm
  if (wheelSide == wheels_left) {
    encoderLF.runSpeed(speed);
    encoderLB.runSpeed(speed);
  } else if (wheelSide == wheels_right) {
    // Inverted speed because
    encoderRF.runSpeed(-speed);
    encoderRB.runSpeed(-speed);
  } else {} // An error: no valid WheelSide specified
}

// Let the wheels finish their motion.
// stopWhenDone - if true, the wheels will stop when done. If false, they will continue rotating.
void letWheelsTurn(bool stopWhenDone)
{
  bool LFdone = false;
  bool LBdone = false;
  bool RFdone = false;
  bool RBdone = false;
  while(!LFdone || !LBdone || !RFdone || !RBdone) {
    if (abs(encoderLF.distanceToGo())<5) {
      if (stopWhenDone==true) encoderLF.runSpeed(0);
      LFdone = true;
    }
    if (abs(encoderLB.distanceToGo())<5) {
      if (stopWhenDone==true) encoderLB.runSpeed(0);
      LBdone = true;
    }
    if (abs(encoderRF.distanceToGo())<5) {
      if (stopWhenDone==true) encoderRF.runSpeed(0);
      RFdone = true;
    }
    if (abs(encoderRB.distanceToGo())<5) {
      if (stopWhenDone==true) encoderRB.runSpeed(0);
      RBdone = true;
    }
    loopEncoders();
  }
  stopWheels();
}

//----------------------- Turning -------------------------------------//

void gyroInit()
{
  gyro.begin();
}

// Info: the gyro gives values between -180 and 180. Positive is clockwise.
// 0 <= mathangle < 360 ( -180 < gyroangle <= 180)
// Converts angle from -180 to 180 degrees, positive clockwise, to 0 to 360 degrees, positive counterclockwise
// Should they be the same function? Apparently they do the same thing...
double gyroAngleToMathAngle(double angle) {return -angle + 180;}
// Does the opposite of above. They are inverse of eachother
double mathAngleToGyroAngle(double angle) {return -angle + 180;}

// Offsets the angles so that one is 180 degrees.
// refAngle - the angle that will become 180 degrees.
// calcAngle - the angle that will be adjusted in accordance.
// The angles should conform to the mathangle standard outlined above. Both input and output should conform.
// Should only be used when the difference between the angles is < 180 degrees
void centerAngle180(double& refAngle, double& calcAngle)
{
  double angleDiff = 180-refAngle; // Will always give a valid result if: 0 <= refAngle < 360
  refAngle += angleDiff; // Should always evaluate to 180, but in case there is a small error I still use this way of doing it.
  calcAngle += angleDiff;
  while (calcAngle >= 360) calcAngle -= 360; // Bind calcAngle to valid mathangle interval.
  while (calcAngle < 0) calcAngle += 360; // Bind calcAngle to valid mathangle interval.
}

// // Updates the robot angles (including the lastwallangle)
// void updateRobotPose()
// {
//   WallSide wallToUse = wall_none;
//   flushDistanceArrays();
//   checkWallPresence();
//   if (safeWallPresence[wall_left] && safeWallPresence[wall_right]) wallToUse = wall_both;
//   else if (safeWallPresence[wall_left]) wallToUse = wall_left;
//   else if (safeWallPresence[wall_right]) wallToUse = wall_right;
//   else wallToUse = wall_none; // If no wall was detected
//   updateRobotPose(wallToUse);
//   g_lastWallAngle = g_robotAngle; // I do not know if this should be here

//   // Debugging
//   // Serial.print(g_lastWallAngle);
//   // Serial.print("  ");
//   // Serial.println(g_startWallAngle);
// }


// // Sets the global variables for angles and distances.
// // Does not update the gyro itself
// // Call the distance updating functions first!
// void updateRobotPose(WallSide wallSide, double& secondaryWallDistance)
// {
//   double tmpGyroAngle = g_currentGyroAngle;
//   double tmpGyroOffset = g_gyroOffset;

//   // In case both walls are present
//   double secondaryRobotAngle = 0;

//   if (wallSide != wall_none)
//   {
//     calcRobotPose(wallSide, g_robotAngle, g_wallDistance, false);
//     if (wallSide == wall_both)
//     {
//       calcRobotPose(wall_right, secondaryRobotAngle, secondaryWallDistance, false);
//       g_robotAngle = (g_robotAngle + secondaryRobotAngle)/2.0;
//     }
//   }
//   else
//   {
//     centerAngle180(tmpGyroAngle, tmpGyroOffset);
//     g_robotAngle = g_startWallAngle + tmpGyroAngle - tmpGyroOffset; // Safe to do because we moved the angles to 180 degrees, meaning that there will not be a zero-cross
//     calcRobotPose(wallSide, g_robotAngle, g_wallDistance, true); // Is this really needed? The angle should already be up to date and the wall distance is not relevant in this case

//   }

// }
// // Alternative way of calling (for when both sides are present)
// // Call distance updates first
// void updateRobotPose(WallSide wallSide)
// {
//   double secondaryWallDistance = 0;
//   updateRobotPose(wallSide, secondaryWallDistance);
// }

// // Update the "true" distance to the wall and the angle from upright
// // Other code should call the getUltrasonics() beforehand
// // wallSide - which wallSide to check. wall_left, wall_right or wall_both
// // angle - the variable to return the calculated angle to. Returned in degrees.
// // angle (if useGyroAngle == true) - the angle calculated by the gyro. Will be used in the calculation and not modified. Degrees. 0 is forward, positive against the left wall and negative against the right wall
// // trueDistance - the variable to return the calculated distance to. Returned in cm.
// // useGyroAngle - whether to use the angle calculated by the gyro (true) or calculate the angle youself (false)
// void calcRobotPose(WallSide wallSide, double& angle, double& trueDistance, bool useGyroAngle)
// {
//   double d1 = 0;
//   double d2 = 0;
//   if (wallSide==wall_left || wallSide == wall_both) // Always calculate for the left wall if the right wall is not present
//   {
//     d1 = ultrasonicCurrentDistances[ultrasonic_LF][usmt_smooth];
//     d2 = ultrasonicCurrentDistances[ultrasonic_LB][usmt_smooth];
//   }
//   else if (wallSide==wall_right)
//   {
//     // Should this be inverted from the left side (like it is now)?
//     d1 = ultrasonicCurrentDistances[ultrasonic_RB][usmt_smooth];
//     d2 = ultrasonicCurrentDistances[ultrasonic_RF][usmt_smooth];
//   }

//   if (useGyroAngle==false) angle = atan((d2 - d1)/ULTRASONIC_SPACING);
//   else angle *= DEG_TO_RAD; // Convert the angle to radians to execute the calculation
//   trueDistance = cos(angle) * ((d1 + d2)/2.0);
//   angle *= RAD_TO_DEG; // Convert the angle to degrees

//   // Debugging
//   //Serial.print(angle); Serial.print("    "); Serial.println(trueDistance);
// }

// Prints data relavant for the robot pose. NOT DONE YET.
// void printRobotPose()
// {
//   Serial.print("g_robotAngle:  ");Serial.println(g_robotAngle);
//   Serial.print("g_trueDistanceDriven:  ");Serial.println(g_trueDistanceDriven);
//   // NOT DONE
// }


// Complete, thorough update of the pose (reads ultrasonic arrays)
void RobotPose::update()
{
  flushDistanceArrays();
  checkWallPresence();
  // lastAngle = angle; // Should this be here?
  update(getSafeWallToUse());
}

void RobotPose::update(WallSide wallToUse)
{
  double distanceIncrement = 0;
  update(wallToUse, distanceIncrement);
}

void RobotPose::update(double distanceIncrement)
{
  update(getSafeWallToUse(), distanceIncrement);
}

// Updates the robot pose based on known distances and also updates gyroOffset angle (+gyro)
void RobotPose::update(WallSide wallToUse, double distanceIncrement)
{
  gyro.update();
  gyroAngle = gyroAngleToMathAngle(gyro.getAngleZ()); 

  if (wallToUse != wall_none)
  {
    calculate2(wallToUse, angle, false, distanceIncrement);
    // gyroOffset = gyroAngle-angle; // Only set in the beginning and end of a move
  }
  else
  {
    double tmpGyroAngle = gyroAngle;
    double tmpGyroOffset = gyroOffset;
    centerAngle180(tmpGyroAngle, tmpGyroOffset);
    angle = tmpGyroAngle - tmpGyroOffset;
    calculate2(wallToUse, angle, true, distanceIncrement);
  }

}

// Updates the gyro offset using the current angle values (update them before calling this function)
void RobotPose::updateGyroOffset()
{
  if (getSafeWallToUse() != wall_none)
  {
    gyroOffset = gyroAngle - angle;
  }
}

void RobotPose::updateOnRamp(WallSide wallToUse, double distanceIncrement)
{
  distOnRamp += yDistIncrement;
  xDistOnRamp += distanceIncrement*cos(abs(gyro.getAngleX())*DEG_TO_RAD);
  yDistOnRamp += distanceIncrement*sin(-gyro.getAngleX()*DEG_TO_RAD);
}

WallSide RobotPose::getNormalWallToUse()
{
  if (normalWallPresence[wall_left] && normalWallPresence[wall_right]) return wall_both;
  else if (normalWallPresence[wall_left]) return wall_left;
  else if (normalWallPresence[wall_right]) return wall_right;
  else return wall_none; // If no wall was detected
}

WallSide RobotPose::getSafeWallToUse()
{
  if (safeWallPresence[wall_left] && safeWallPresence[wall_right]) return wall_both;
  else if (safeWallPresence[wall_left]) return wall_left;
  else if (safeWallPresence[wall_right]) return wall_right;
  else return wall_none; // If no wall was detected
}

bool g_frontUltrasonicUsed = false;

void RobotPose::calculate2(WallSide wallSide, double& tAngle, bool useGyroAngle, double distanceIncrement)
{
  double d1 = 0;
  double d2 = 0;
  double a1 = 0;
  double a2 = 0;
  if (useGyroAngle==true)
  {
    a1 = tAngle;
    a2 = tAngle;
  }
  if (wallSide==wall_both)
  {
    calculate1(wall_left, a1, d1, useGyroAngle);
    calculate1(wall_right, a2, d2, useGyroAngle);
    xDist = double(d1+d2)/2.0;
    angle = double(a1+a2)/2.0;
  }
  else if (wallSide==wall_left)
  {
    calculate1(wall_left, a1, d1, useGyroAngle);
    xDist = d1;
    angle = a1;
  }
  else if (wallSide==wall_right)
  {
    calculate1(wall_right, a2, d2, useGyroAngle);
    xDist = d2;
    angle = a2;
  }
  else
  {
    // If no wall is present
    // If front ultrasonic sensor data is available, use that difference instead of the encoders
    xDist += distanceIncrement*sin(angle*DEG_TO_RAD);
  }

  // Blending with front ultrasonic sensor
  if (ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth] < MAX_ULTRASONIC_NEAR)
  {
    // If you should use the direct value of the front sensor
    double newY = 30 - (ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth] + ULTRASONIC_FRONT_OFFSET) + 15;
    while(newY < 0) {newY += 30;} // Wrap the value to 0-30, as yDist should be between 0 and 30 normally
    double difference =  newY - yDist;
    if (abs(difference) < MAX_FRONT_CORRECTION_DISTANCE) // Problem: something in here should go into else
    {
      #warning could be problematic for wallchanges. Maybe always use front sensor differences (see below)?
      yDistIncrement = difference;
      yDist = newY;
    }
    g_frontUltrasonicUsed = true;
  }
  // else if (ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth] < MAX_ULTRASONIC_FAR && abs(angle) < MAX_FRONT_ANGLE)
  // {
  //   // When to look at differences in ultrasonic readings
  //   #warning lastDistance will be wrong after turning moves
  //   static double lastDistance = ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth];
  //   double distance = ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth];
  //   double difference = lastDistance - distance;
    
  //   lastDistance = distance; // Set variable for next execution

  //   if (abs(difference) < MAX_FRONT_DIFFERENCE)
  //   {
  //     yDistIncrement = difference;
  //     yDist += yDistIncrement;
  //   }
  //   g_frontUltrasonicUsed = true;
  
  //   #warning does not yet check for wallchanges (should use already existing ones) (maybe not needed?)
  // }
  else
  {
    yDistIncrement = distanceIncrement*cos(angle*DEG_TO_RAD);
    yDist += yDistIncrement;
    g_frontUltrasonicUsed = false;
  }
  // yDistIncrement = distanceIncrement*cos(angle*DEG_TO_RAD); // The default increment
  incrementShadowDistances(yDistIncrement);
  // yDist+=yDistIncrement;
  #warning If correction using front ultrasonic sensor is done, the wallchange distances may be off?
  #warning Maybe use a hierarchial order, where the front ultrasonic sensor is always used over others if it is available?
}

  // // Bledning with front ultrasonic sensor
  
  // if (ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth] < MAX_ULTRASONIC_NEAR)
  // {
  //   // If you should use the direct value of the front sensor
  //   double newY = 30 - (ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth] + ULTRASONIC_FRONT_OFFSET);
  //   while(newY < 0) {newY += 30;} // Wrap the value to 0-30
  //   yDistIncrement =  newY - yDist;
  //   if (abs(newY - yDist) < MAX_FRONT_CORRECTION_DISTANCE) // Problem: something in here should go into else
  //   {

  //   }
  // }
  // else
  // {
  // yDistIncrement = distanceIncrement*cos(angle*DEG_TO_RAD);
  // }

  // yDist += yDistIncrement; // Increment y distance
  // incrementShadowDistances(yDistIncrement);

void RobotPose::calculate1(WallSide wallSide, double& tAngle, double& distance, bool useGyroAngle)
{
  double d1 = 0;
  double d2 = 0;
  if (wallSide==wall_left || wallSide == wall_both) // Always calculate for the left wall if the right wall is not present
  {
    d1 = ultrasonicCurrentDistances[ultrasonic_LF][usmt_smooth] + ULTRASONIC_SIDEOFFSET;
    d2 = ultrasonicCurrentDistances[ultrasonic_LB][usmt_smooth] + ULTRASONIC_SIDEOFFSET;
  }
  else if (wallSide==wall_right)
  {
    // Should this be inverted from the left side (like it is now)?
    d1 = ultrasonicCurrentDistances[ultrasonic_RB][usmt_smooth] + ULTRASONIC_SIDEOFFSET;
    d2 = ultrasonicCurrentDistances[ultrasonic_RF][usmt_smooth] + ULTRASONIC_SIDEOFFSET;
  }
  if (useGyroAngle==false) tAngle = atan((d2 - d1)/ULTRASONIC_SPACING);
  else tAngle *= DEG_TO_RAD; // Convert the angle to radians to execute the calculation

  if (wallSide==wall_right) distance = 30 - distance; // Invert if the measured side is the right one
  distance = cos(tAngle) * ((d1 + d2)/2.0);
  tAngle *= RAD_TO_DEG; // Convert the angle to degrees
}

void RobotPose::print()
{
  Serial.print("angle:  ");Serial.print(angle, 2);Serial.print("  ");
  Serial.print("startAngle:  ");Serial.print(startAngle, 2);Serial.print("  ");
  Serial.print("lastAngle:  ");Serial.print(lastAngle, 2);Serial.print("  ");
  Serial.print("targetAngle:  ");Serial.print(targetAngle, 2);Serial.print("  ");
  Serial.print("gyroAngle:  ");Serial.print(gyroAngle, 2);Serial.print("  ");
  Serial.print("gyroOffset:  ");Serial.print(gyroOffset, 2);Serial.print("  ");
  Serial.print("targetGyroAngle:  ");Serial.print(targetGyroAngle, 2);Serial.print("  ");
  Serial.print("xDist:  ");Serial.print(xDist, 1);Serial.print("  ");
  Serial.print("yDist:  ");Serial.print(yDist, 1);Serial.print("  ");
  Serial.println("");
}


// Returns the distance left to turn in degrees. When you get closer, it decreases. When you have overshot, it will be negative.
// zeroCross - if the turn will cross over 0. Should not be needed anymore?
// turningdirection - which direction you will turn. 1 is counter-clockwise and -1 is clockwise (math angles)
// tarAng - the angle you want to turn to.
// curAng - the angle you are currently at.
// The maximum allowed difference between the angles is <180
double leftToTurn(bool zeroCross, int turningDirection, double tarAng, double curAng)
{
  static double lastLeftToTurn = 0;
  double targetAngle = tarAng;
  double currentAngle = curAng;
  centerAngle180(targetAngle, currentAngle);

  double leftToTurn = 0;
  if (turningDirection==1)
  {
    leftToTurn = targetAngle - currentAngle;
    lastLeftToTurn = leftToTurn;
  }
  else if (turningDirection == -1)
  {
    leftToTurn = currentAngle - targetAngle;
    lastLeftToTurn = leftToTurn;
  }
  else 
  {
    // If turningDirection is incorrect, use the last leftToTurn variable
    leftToTurn = lastLeftToTurn;
  }
  
  return leftToTurn;

  /* The old code:
  if (zeroCross==false) { // Normal turn
    if (turningDirection == 1) {
      if (tarAng>300 && curAng<180) return 360 - tarAng + curAng; // If you have overshot so much that you passed zero
      else return turningDirection*(tarAng-curAng); // Else turn like normal
    } else if (turningDirection == -1) {
      if (tarAng<60 && curAng >180) return 360 - curAng + tarAng; // if you have overshot so much that you passed zero
      else return turningDirection*(tarAng-curAng); // Else turn like normal
    }
  } else if (zeroCross==true && turningDirection==1) { // Turning that passes 0 and is counter-clockwise (positive direction)
    if (curAng>180) return 360 - curAng + tarAng; // Handles it until curAng is 0
    else return turningDirection*(tarAng-curAng); // When curAng has passed 0, it is just lika a normal turn (like above)
  } else if (zeroCross==true && turningDirection==-1) { // Turning that passes 0 and is clockwise (negative direction)
    if (curAng<180) return curAng + 360-tarAng; // Handles it until curAng is <360 (passes 0)
    else return turningDirection*(tarAng-curAng); // When curAng has passed 0, it is just lika a normal turn (like above)
  }
  return -100; // Return -100 if nothing matched (which should never happen)
  */
}


double gyroDriveCorrectionCoeff = 1;
// Turn using the gyro
// turnAngle - the angle to turn, in degrees. Should not be greater than 90 (only tested with 90). Positive is counter-clockwise (math angle)
// stopMoving - Whether or not the robot should stop when the function is done. Set to false when driving continuously.
// baseSpeed - Optional argument for specifying the speed to move at while turning. cm/s
// If baseSpeed != 0, the function will update trueDistanceDriven.
void gyroTurn(double turnAngle, bool stopMoving, bool aware = false, double baseSpeed = 0)
{
  // Used to determine turning direction of the wheels
    int multiplier = 1; // Positive is ccw
    bool crossingZero = false;
    if (turnAngle<0) multiplier=-1; // Negative is cw

    // Checking for unreasonable values:
    if (turnAngle > 170)
    {
      turnAngle = 90;
      sounds::errorBeep();
    }
    if (turnAngle < -170)
    {
      turnAngle = -90;
      sounds::errorBeep();
    }

    gyro.update();
    pose.gyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
    pose.targetGyroAngle = pose.gyroAngle + turnAngle;
    if (pose.targetGyroAngle<0) {
    pose.targetGyroAngle +=360; // If the target angle is negative, add 360 to make it between 0 and 360 (no angle should be smaller than -360)
    crossingZero = true;
  } else if (pose.targetGyroAngle >= 360) {
    pose.targetGyroAngle -= 360; // Should bind the value to be between 0 and 360 for positive target angles (no angle should be 720 degrees or greater, so this should work)
    crossingZero = true;
  }
    //double speedToRun = multiplier*1.5*g_baseSpeed_CMPS*CMPS_TO_RPM;
    double dumbDistanceDriven = 0; // Only needed if speedToRun != 0
    double speedToRun = multiplier*35/CMPS_TO_RPM;
    if (baseSpeed != 0)
    {
      if (aware==true) startDistanceMeasure(); // For aware gyro turn
      runWheelSide(wheels_left, baseSpeed - speedToRun*gyroDriveCorrectionCoeff);
      runWheelSide(wheels_right, baseSpeed + speedToRun*gyroDriveCorrectionCoeff);
    }
    else
    {
      runWheelSide(wheels_left, -speedToRun);
      runWheelSide(wheels_right, speedToRun);
    }

    double varLeftToTurn = leftToTurn(crossingZero, multiplier, pose.targetGyroAngle, pose.gyroAngle);

    while (varLeftToTurn > 15) {
      // pose.print();
      gyro.update();
      pose.gyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
      varLeftToTurn = leftToTurn(crossingZero, multiplier, pose.targetGyroAngle, pose.gyroAngle);
      // Serial.println(varLeftToTurn);

      if (baseSpeed != 0 && aware==true)
      {
        // Update truedistancedriven (copied from driveStepDriveLoop and slightly modified)
        double dumbDistanceIncrement = getDistanceDriven() - dumbDistanceDriven;
        pose.update(wall_none, dumbDistanceIncrement);
        dumbDistanceDriven = getDistanceDriven();
      }
    }

    // Slowing down in the end of the turn.
    // All of this code could be placed inside of the first while-loop, but then every iteration would take more time because of checks and the gyro would become less accurate due to that.
    speedToRun = multiplier*20/CMPS_TO_RPM;
    if (baseSpeed != 0)
    {
      runWheelSide(wheels_left, baseSpeed - speedToRun*gyroDriveCorrectionCoeff);
      runWheelSide(wheels_right, baseSpeed + speedToRun*gyroDriveCorrectionCoeff);
    }
    else
    {
      runWheelSide(wheels_left, -speedToRun);
      runWheelSide(wheels_right, speedToRun);
    }

    while (varLeftToTurn > 2) {
      // pose.print();
      gyro.update();
      pose.gyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
      varLeftToTurn = leftToTurn(crossingZero, multiplier, pose.targetGyroAngle, pose.gyroAngle);

      if (baseSpeed != 0 && aware==true)
      {
        // Update truedistancedriven (copied from driveStepDriveLoop and slightly modified)
        double dumbDistanceIncrement = getDistanceDriven() - dumbDistanceDriven;
        pose.update(wall_none, dumbDistanceIncrement);
        dumbDistanceDriven = getDistanceDriven();
      }
    }

    if (stopMoving==true) stopWheels();

}

// GyroTurn but it is updating the robot pose variables accordingly.
// The code is copied from gyroTurnSteps(), so I should probably make gyroTurnSteps() make use of awareGyroTurn().
void awareGyroTurn(double turnAngle, bool stopMoving, double baseSpeed = 0)
{
  pose.update();
  pose.startAngle = pose.angle;

  double startGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
  gyroTurn(turnAngle, true, true, baseSpeed);
  gyro.update();
  double endGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
  centerAngle180(endGyroAngle, startGyroAngle);
  double angleDiff = endGyroAngle - startGyroAngle;
  #warning Offset used wrong!
  pose.gyroOffset = gyroAngleToMathAngle(gyro.getAngleZ()) - pose.angle;// The angle measured by the gyro (absolute angle) in the beginning.

  pose.update();
  pose.startAngle = pose.startAngle + angleDiff;

}

// Turns the specified steps (90 degrees) in the direction specified above.
// Automatic correction for the last angle to the wall can be specified by the last argument. Make sure that the g_lastWallAngle is up to date!
// direction - cw (clockwise) or ccw (counter-clockwise) turn.
// steps - the amount of 90-degree turns to do in the chosen direction. (NOT YET IMPLEMENTED!)
// doCorrection - Whether or not you should correct for the g_lastWallAngle
void gyroTurnSteps(TurningDirection direction, int steps, bool doCorrection)
{
  int multiplier=-1;
  if (direction==ccw) multiplier=1;
  double turnAngle = multiplier*90;
  if (steps==0) turnAngle = 0;
  pose.update();
  pose.startAngle = pose.angle;

  if (doCorrection == true)
  {
    turnAngle = turnAngle - pose.angle;
    // g_lastWallAngle = 0; // You should have turned perfectly. Should be replaced by actually checking how far you have turned.
  }

  double startGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
  gyroTurn(turnAngle, true);
  gyro.update();
  double endGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
  centerAngle180(endGyroAngle, startGyroAngle);
  double angleDiff = endGyroAngle - startGyroAngle;
  // pose.angle -= multiplier*90;
  if (steps==0) pose.angle = pose.angle + angleDiff;
  else pose.angle = pose.angle + angleDiff - multiplier*90;
  pose.gyroOffset = gyroAngleToMathAngle(gyro.getAngleZ()) - pose.angle; // Set gyro offset (preliminary, may not be very good?)
  pose.update(0);

  // Serial.println("Robot pose at end");
  // pose.print(); // Debugging

}

void turnSteps(TurningDirection direction, int steps)
{
  flushDistanceArrays();
  gyroTurnSteps(direction, steps, true);
  flushDistanceArrays();
  if (abs(pose.angle) > MIN_CORRECTION_ANGLE)
  {
  straighten();
  }
  flushDistanceArrays();
}

// Should be done after every move. (at the end of drivestep and turnsteps) That way, the robot should always be straigt for the new measurements.
void straighten()
{
  gyroTurnSteps(cw, 0, true);
}

void sideWiggleCorrection(WallSide direction)
{
  
}

// Wiggles and updates pose before and after
void sideWiggleCorrection()
{
  double distanceError = 0; // Positive is to the right of the centre of the tile
  distanceError = pose.xDist - 15;
  WallSide wallToWiggle = pose.getSafeWallToUse();
  if (distanceError < 0) wallToWiggle = wall_right; // If to the left, go right
  else wallToWiggle = wall_left; // If to the right, go left

  sideWiggleCorrection(wallToWiggle); // Does the wiggling
}

void turnToPIDAngle()
{
  // Use gyroturn to do it.
}



//--------------------- Sensors --------------------------------------//


void initColourSensor()
{
  colSensor.init();
  colSensor.refreshReferences();
}

// Adds curDistanceData onto the specified array
void updateDistanceArray(UltrasonicSensorEnum sensorToUse)
{
  ultrasonicRawDistances[sensorToUse][ultrasonicRawDistancesPointer[sensorToUse]] = ultrasonicCurrentDistances[sensorToUse][usmt_raw];
  ++ultrasonicRawDistancesPointer[sensorToUse];
  while (ultrasonicRawDistancesPointer[sensorToUse] >= DISTANCE_MEASUREMENT_SIZE) ultrasonicRawDistancesPointer[sensorToUse] -= DISTANCE_MEASUREMENT_SIZE;
  while (ultrasonicRawDistancesPointer[sensorToUse] < 0) ultrasonicRawDistancesPointer[sensorToUse] += DISTANCE_MEASUREMENT_SIZE;
}

// Calculates the average (distance) for the specified array
double calcDistanceAverage(UltrasonicSensorEnum sensorToCalc)
{
  double sum = 0;
  for (int i=0;i<DISTANCE_MEASUREMENT_SIZE;++i)
  {
    sum += ultrasonicRawDistances[sensorToCalc][i];
  }
  return sum/DISTANCE_MEASUREMENT_SIZE;
}

// Fills the distanceArrays with fresh data
void flushDistanceArrays()
{
  for (int i=0;i<DISTANCE_MEASUREMENT_SIZE;++i)
    {
      getUltrasonics();
      delay(5); // Maybe not necessary, but I have it just in case
    }
}

void getUltrasonics1()
{

}

void getUltrasonics2()
{

}

// Updates all ultrasonic distances, loops encoders and updates the gyro. The time taken is such that this function can be called directly after itself without interference
void ultrasonicUpdateLoop(UltrasonicSensorEnum sensor, double maxDistance, bool waitForInterference)
{
  ultrasonicCurrentDistances[sensor][usmt_raw] = ultrasonicSensors[sensor].distanceCm(maxDistance);
  updateDistanceArray(sensor);
  ultrasonicCurrentDistances[sensor][usmt_smooth] = calcDistanceAverage(sensor);
  if (waitForInterference == true) ultrasonicIdle();
}

void ultrasonicIdle()
{
  loopEncoders();
  gyro.update();
  delay(2);
}

// Get all ultrasonic sensors and update the gyro and encoders
// Perhaps return an array in the future (or take on as a mutable(?) argument?)
void getUltrasonics()
{
  ultrasonicUpdateLoop(ultrasonic_LF, 35, true);
  ultrasonicUpdateLoop(ultrasonic_RB, 35, true);
  ultrasonicUpdateLoop(ultrasonic_RF, 35, true);
  ultrasonicUpdateLoop(ultrasonic_LB, 35, true);
  ultrasonicUpdateLoop(ultrasonic_F, 120, false);

}
// For determining wall presence for individual sensors
// bool wallPresentLF = false;
// bool wallPresentRF = false;
// bool wallPresentLB = false;
// bool wallPresentRB = false;
// bool wallPresentF = false;

void printUltrasonics()
{
  
  Serial.print("RAW:    ");
  // Serial.print("F:");Serial.print(ultrasonicCurrentDistances[ultrasonic_F][usmt_raw]);
  Serial.print(" LF:");Serial.print(ultrasonicCurrentDistances[ultrasonic_LF][usmt_raw]);
  // Serial.print(" LB:");Serial.print(ultrasonicCurrentDistances[ultrasonic_LB][usmt_raw]);
  // Serial.print(" RF:");Serial.print(ultrasonicCurrentDistances[ultrasonic_RF][usmt_raw]);
  // Serial.print(" RB:");Serial.print(ultrasonicCurrentDistances[ultrasonic_RB][usmt_raw]);
  // Serial.println("");
  Serial.print("    ");

  Serial.print("SMOOTH: ");
  // Serial.print("F:");Serial.print(ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth]);
  Serial.print(" LF:");Serial.print(ultrasonicCurrentDistances[ultrasonic_LF][usmt_smooth]);
  // Serial.print(" LB:");Serial.print(ultrasonicCurrentDistances[ultrasonic_LB][usmt_smooth]);
  // Serial.print(" RF:");Serial.print(ultrasonicCurrentDistances[ultrasonic_RF][usmt_smooth]);
  // Serial.print(" RB:");Serial.print(ultrasonicCurrentDistances[ultrasonic_RB][usmt_smooth]);
  Serial.println("");
}

void prinSensortWallPresence()
{
  Serial.print("RAW:    ");
  Serial.print("F:");Serial.print(currentSensorWallStates[ultrasonic_F][usmt_raw]);
  Serial.print(" LF:");Serial.print(currentSensorWallStates[ultrasonic_LF][usmt_raw]);
  Serial.print(" LB:");Serial.print(currentSensorWallStates[ultrasonic_LB][usmt_raw]);
  Serial.print(" RF:");Serial.print(currentSensorWallStates[ultrasonic_RF][usmt_raw]);
  Serial.print(" RB:");Serial.print(currentSensorWallStates[ultrasonic_RB][usmt_raw]);
  Serial.println("");


  Serial.print("SMOOTH: ");
  Serial.print("F:");Serial.print(currentSensorWallStates[ultrasonic_F][usmt_smooth]);
  Serial.print(" LF:");Serial.print(currentSensorWallStates[ultrasonic_LF][usmt_smooth]);
  Serial.print(" LB:");Serial.print(currentSensorWallStates[ultrasonic_LB][usmt_smooth]);
  Serial.print(" RF:");Serial.print(currentSensorWallStates[ultrasonic_RF][usmt_smooth]);
  Serial.print(" RB:");Serial.print(currentSensorWallStates[ultrasonic_RB][usmt_smooth]);
  Serial.println("");
}

void printWallPresence()
{
  Serial.print("NORMAL: ");
  Serial.print("F: ");Serial.print(normalWallPresence[wall_front]);
  Serial.print(" L: ");Serial.print(normalWallPresence[wall_left]);
  Serial.print(" R: ");Serial.print(normalWallPresence[wall_right]);
  Serial.println("");

  Serial.print("SAFE:   ");
  Serial.print("F: ");Serial.print(safeWallPresence[wall_front]);
  Serial.print(" L: ");Serial.print(safeWallPresence[wall_left]);
  Serial.print(" R: ");Serial.print(safeWallPresence[wall_right]);
  Serial.println("");
}

// Update individual sensor wall presence (raw and smooth)
// sensor - the sensor whose values to update
void updateSensorWallPresence(UltrasonicSensorEnum sensor)
{
  //Raw
  if (ultrasonicCurrentDistances[sensor][usmt_raw] < WALL_PRESENCE_TRESHOLD) currentSensorWallStates[sensor][usmt_raw] = true;
  else currentSensorWallStates[sensor][usmt_raw] = false;

  //Smooth
  if (ultrasonicCurrentDistances[sensor][usmt_smooth] < WALL_PRESENCE_TRESHOLD) currentSensorWallStates[sensor][usmt_smooth] = true;
  else currentSensorWallStates[sensor][usmt_smooth] = false;
}

// Update the wallpresence variables based on the ultrasonic values
// wallToUse is the wall for which to calculate the wall presence
void updateWallPresence(WallSide wallToUse)
{
  UltrasonicSensorEnum sensor1 = ultrasonic_F;
  UltrasonicSensorEnum sensor2 = ultrasonic_F;
  if (wallToUse == wall_left)
  {
    sensor1 = ultrasonic_LF;
    sensor2 = ultrasonic_LB;
  }
  else if (wallToUse == wall_right)
  {
    sensor1 = ultrasonic_RF;
    sensor2 = ultrasonic_RB;
  }
  else
  {
    // Perhaps use for debugging
  }

  if (currentSensorWallStates[sensor1][usmt_smooth] == true && currentSensorWallStates[sensor2][usmt_smooth] == true) // Could be more effective for front wall, but this does the job
  {
    normalWallPresence[wallToUse] = true;
    if (currentSensorWallStates[sensor1][usmt_raw] == true && currentSensorWallStates[sensor2][usmt_raw] == true)
    {
      safeWallPresence[wallToUse] = true;
    }
    else
    {
      safeWallPresence[wallToUse] = false;
    }
  }
  else
  {
    normalWallPresence[wallToUse] = false;
    safeWallPresence[wallToUse] = false;
  }
}


// Check if the walls are present. Uses raw distance data instead of "true" distance data. What does this mean?
void checkWallPresence()
{
  // Update individual sensor wall presence
  updateSensorWallPresence(ultrasonic_LF);
  updateSensorWallPresence(ultrasonic_RB);
  updateSensorWallPresence(ultrasonic_RF);
  updateSensorWallPresence(ultrasonic_LB);
  updateSensorWallPresence(ultrasonic_F);

  // Debugging
  // Serial.print("LF:");
  // Serial.print(currentSensorWallStates[ultrasonic_LF][usmt_smooth]);
  // Serial.print("  LB:");
  // Serial.print(currentSensorWallStates[ultrasonic_LB][usmt_smooth]);
  // Serial.print("  RF:");
  // Serial.print(currentSensorWallStates[ultrasonic_RF][usmt_smooth]);
  // Serial.print("  RB:");
  // Serial.print(currentSensorWallStates[ultrasonic_RB][usmt_smooth]);
  // Serial.println("");

  // Wall presence
  updateWallPresence(wall_left);
  updateWallPresence(wall_right);
  updateWallPresence(wall_front);

}

// Sets the previous wall states for the specified sensor
// sensor - the sensor to set the values for
void setSinglePreviousSensorWallState(UltrasonicSensorEnum sensor)
{
  previousSensorWallStates[sensor][usmt_raw] = currentSensorWallStates[sensor][usmt_raw];
  previousSensorWallStates[sensor][usmt_smooth] = currentSensorWallStates[sensor][usmt_smooth];
}

// Sets the vairable for previous wall states to the current ones
void setPreviousSensorWallStates()
{
  setSinglePreviousSensorWallState(ultrasonic_LF);
  setSinglePreviousSensorWallState(ultrasonic_RB);
  setSinglePreviousSensorWallState(ultrasonic_RF);
  setSinglePreviousSensorWallState(ultrasonic_LB);
  setSinglePreviousSensorWallState(ultrasonic_F);
}

void updateLastKnownDistance(UltrasonicSensorEnum sensor)
{
  if (currentSensorWallStates[sensor][usmt_raw] == true)
  {
    ultrasonicLastKnownDistances[sensor][usmt_raw] = ultrasonicCurrentDistances[sensor][usmt_raw];
  }
  if (currentSensorWallStates[sensor][usmt_smooth] == true)
  {
    ultrasonicLastKnownDistances[sensor][usmt_smooth] = ultrasonicCurrentDistances[sensor][usmt_smooth];
  }

}

void updateLastKnownDistances()
{
  updateLastKnownDistance(ultrasonic_LF);
  updateLastKnownDistance(ultrasonic_LB);
  updateLastKnownDistance(ultrasonic_RF);
  updateLastKnownDistance(ultrasonic_RB);
}

void updateTheoreticalDistance(UltrasonicSensorEnum sensor)
{
}


// Returns the struct containing information about the presence of the walls.
// The reason for using a struct is that I do not know how else to return 3 values
int getWallStates()
{
  flushDistanceArrays();
  checkWallPresence();
  uint8_t wallStates = 0;
  if (normalWallPresence[wall_front]) wallStates |= 0b100;
  if (normalWallPresence[wall_left]) wallStates |= 0b010;
  if (normalWallPresence[wall_right]) wallStates |= 0b001;
  return wallStates;
}

// Check for a potential wallchange for one sensor and update the struct/array.
void checkPotWallChange(int sensor)
{
  if (currentSensorWallStates[sensor][usmt_raw] != previousSensorWallStates[sensor][usmt_raw])
  {
    if (currentSensorWallStates[sensor][usmt_raw] == true)
    {
      g_potWallChanges[sensor][wallchange_approaching].detected = true;
      g_potWallChanges[sensor][wallchange_approaching].timestamp = millis();
    }
    else
    {
      g_potWallChanges[sensor][wallchange_leaving].detected = true;
      g_potWallChanges[sensor][wallchange_leaving].timestamp = millis();
    }
  }
  else
  {
    g_potWallChanges[sensor][wallchange_approaching].detected = false;
    g_potWallChanges[sensor][wallchange_leaving].detected = false;
  }
}

// Check for potential wallchanges in all sensors
void checkPotWallChanges()
{
  for (int i=1; i<ULTRASONIC_NUM; ++i)
  {
    checkPotWallChange(i);
  }
}

void checkSmoothWallChange(int sensor)
{
  if (currentSensorWallStates[sensor][usmt_smooth] != previousSensorWallStates[sensor][usmt_smooth])
  {
    if (currentSensorWallStates[sensor][usmt_smooth] == true) g_smoothWallChanges[sensor] = wallchange_approaching;
    else g_smoothWallChanges[sensor] = wallchange_leaving;
  }
  else g_smoothWallChanges[sensor] = wallchange_none;
}

// Checking for smooth wall changes
// Needs to be more robust
void checkSmoothWallChanges()
{
  for (int i=1; i<ULTRASONIC_NUM; ++i)
  {
    checkSmoothWallChange(i);
  }
}




//----------------------Driving----------------------------------------//

// Drive just using the encoders
// distance - the distance to drive
// The speed is adjusted globally using the g_baseSpeed_CMPS variable.
void driveBlind(double distance, bool stopWhenDone)
{
  moveWheelSide(wheels_left, distance, g_baseSpeed_CMPS);
  moveWheelSide(wheels_right, distance, g_baseSpeed_CMPS);
  letWheelsTurn(stopWhenDone);
}

//Drives at the set speed. Need to loop encoders in between
// speed - the speed to drive at in cm/s
// You need to call loopEncoders() Yourself!
void driveSpeed(double speed)
{
  runWheelSide(wheels_left, speed);
  runWheelSide(wheels_right, speed);
}

long startPositionEncoderLF = 0;
long startPositionEncoderLB = 0;
long startPositionEncoderRF = 0;
long startPositionEncoderRB = 0;


void startDistanceMeasure()
{
  startPositionEncoderLF = encoderLF.getCurPos();
  startPositionEncoderLB = encoderLB.getCurPos();
  startPositionEncoderRF = encoderRF.getCurPos();
  startPositionEncoderRB = encoderRB.getCurPos();
}

long turns = 0;

// Returns the distance driven by the robot since startDistanceMeasure() was called. Return is in cm.
// Idea: Handle if one encoder is very off?
double getDistanceDriven()
{
  long distanceEncoderLF = encoderLF.getCurPos()-startPositionEncoderLF;
  long distanceEncoderLB = encoderLB.getCurPos()-startPositionEncoderLB;
  long distanceEncoderRF = -(encoderRF.getCurPos()-startPositionEncoderRF);
  long distanceEncoderRB = -(encoderRB.getCurPos()-startPositionEncoderRB);

  return ((distanceEncoderLF+distanceEncoderLB+distanceEncoderRF+distanceEncoderRB)/4.0)/360*WHEEL_CIRCUMFERENCE; // Returns the average

}

const double normAngleP = 1; //    1  , 1, 1  ,
const double normDistanceP = 2.5; // 2.2, 3, 3  ,
const double normDistanceD = 1; // 0, 1, 0.5,

const double rampAngleP = 0.2;
const double rampDistanceP = 2;
const double rampDistanceD = 0.5;

// PID coefficients for wall following (in the process of tuning)
// The comments after the coefficients are a history of coefficients that worked allright
double angleP = normAngleP;
double distanceP = normDistanceP;
double distanceD = normDistanceD;

void useNormPID()
{
  angleP = normAngleP;
  distanceP = normDistanceP;
  distanceD = normDistanceD;
  // sounds::tone(220, 50);
}

void useRampPID()
{
  angleP = rampAngleP;
  distanceP = rampDistanceP;
  distanceD = rampDistanceD;
  // sounds::tone(880, 50);
}

// Variables for derivative calculation
WallSide g_lastWallSide = wall_none; // Was annoying to do as static

// Drive with wall following. Will do one iteration, so to actually follow the wall, call it multiple times in short succession.
// wallSide - which wall to follow. Can be wall_left, wall_right or wall_both. Directions relative to the robot.
// startAngle - The angle relative to the wall for the begin of the move (degrees, mathangle)
// gyroOffset  - The angle that the gyro read for the begin of the move (degrees, mathangle)
// The last two arguments are only used if the wallSide == wall_none
void pidDrive(WallSide wallSide)
{
  static double lastDistError = 0;
  static unsigned long lastExecutionTime = millis();
  double distanceError = 0; // positive means that we are to the right of where we want to be.
  double distanceDerivative = 0; // The rate of change for the derivative term. Initialized to 0, so that it is only used if actively changed

  pose.update(wallSide);

  distanceError = pose.xDist - 15;

  if (g_lastWallSide == wallSide && g_lastWallSide != wall_none) distanceDerivative = 1000.0*(distanceError - lastDistError)/(millis()-lastExecutionTime); // Calculate the derivative. (The 1000 is to make the time in seconds)
  // Update variables for the next execution loop
  lastExecutionTime = millis();
  lastDistError = distanceError;
  g_lastWallSide = wallSide;

  double goalAngle = 0;
  if (wallSide != wall_none) { // If you do not have a wall, continue driving straight forwards with correction
    goalAngle = distanceError*distanceP + distanceDerivative*distanceD; // Calculate the angle you want depending on the distance to the wall (error) and the speed at which you approach (derivative)
  }
  double angleError = goalAngle - pose.angle; // Calculate the correction needed in the wheels to get to the angle
  double correction = angleP*angleError; // Calculate the correction in the wheels. Positive is counter-clockwise (math)

  // Filter out the extreme cases
  if (correction > 10) correction = 10;
  else if (correction < -10) correction = -10;

  // For driving backwards
  int multiplier = 1;
  if (g_driveBack == true) multiplier = -1;

  runWheelSide(wheels_left, multiplier*g_baseSpeed_CMPS - correction);
  runWheelSide(wheels_right, multiplier*g_baseSpeed_CMPS + correction);
  loopEncoders(); // Could maybe remove - already done in getUltrasonics()
  pose.lastAngle = pose.angle; // Update the g_lastWallAngle - okay to do because this will not be read during the execution loop of pidTurn. It will only be used before.
  // Serial.println(g_robotAngle);


  // Debugging
  // Serial.print(distanceError);Serial.println("");
  // Serial.print(g_robotAngle);Serial.print(" ");Serial.print(goalAngle);Serial.println("");
  // Serial.println(correction);

  // Debugging
  // Serial.print("Correction:");
  // Serial.print(correction);
  // Serial.print("    ");
  // Serial.print("distanceError:");
  // Serial.print(distanceError);
  // Serial.print("    ");
  // Serial.print("distanceDerivative:");
  // Serial.print(distanceDerivative);
  // Serial.print("    ");
  // Serial.print("angleError:");
  // Serial.print(angleError);
  // Serial.print("    ");
  // Serial.println("");

}

// Takes the average of a double array containg 10 elements
// Returns the average
// TODO: make it more general - accept a double array of any size
//------------------------------------------------------------------WARNING!!!!!! Potential try to access value not in array. Be careful.
double measurementAverage(double arrayToCalc[]) // Use a reference to the array instead? Less expensive?
{
  double sum = 0;
  for (int i=0; i<10; ++i) {
    sum += arrayToCalc[i];
  }
  return sum/10.0;
}



bool g_onRampIterations[ON_RAMP_ARR_SIZE];
int g_onRampPointer = 0;
bool g_previousOnRampState = false;
double g_trueDistanceDrivenOnRamp = 0;
double g_horizontalDistanceDrivenOnRamp = 0;
double g_verticalDistanceDrivenOnRamp = 0;


void fillRampArrayFalse()
{
  for (int i=0; i<ON_RAMP_ARR_SIZE; ++i)
  {
    g_onRampIterations[i] = false;
  }
}

void addOnRampValue(bool state)
{
  g_onRampPointer = g_onRampPointer % ON_RAMP_ARR_SIZE; // Could cause errors if g_onRampPointer goes negative, which it never should
  g_onRampIterations[g_onRampPointer] = state;
  ++g_onRampPointer;
}

// Returns true if you are currently on a ramp
bool getIsOnRamp()
{
  int sum = 0;
  for (int i=0; i<ON_RAMP_ARR_SIZE; ++i)
  {
    if (g_onRampIterations[i] == true) ++sum;
  }
  int average = (double)sum/(double)ON_RAMP_ARR_SIZE;
  if (average > 0.5) return true;
  else return false;
}


void incrementShadowDistances(double incrementDistance)
{
  for (int i=1; i<ULTRASONIC_NUM; ++i)
  {
    g_potWallChanges[i][wallchange_approaching].shadowDistanceDriven += incrementDistance;
    g_potWallChanges[i][wallchange_leaving].shadowDistanceDriven += incrementDistance;
  }
}

void setShadowDistance(int sensor, WallChangeType wallChange, double setDistance)
{
  g_potWallChanges[sensor][wallChange].shadowDistanceDriven = setDistance;
}

// Updates the appropriate variables to the appropriate values in accordance with the detected wallchange, if there is one
// sensor - which ultrasonic sensor to check and update from
// wallChangeToCheck - wallchange_approaching or wallchange_leaving
void checkAndUseWallChange(int sensor, WallChangeType wallChangeToCheck, StoppingReason& stopReason)
{
  UltrasonicGroup ultrasonicGroup = ultrasonics_front;
  if (sensor == ultrasonic_LB || sensor == ultrasonic_RB) ultrasonicGroup = ultrasonics_back;

  if (g_smoothWallChanges[sensor] == wallChangeToCheck)
  {
    double offset = 0;
    // Front wallchange
    if ((g_driveBack==false && ultrasonicGroup==ultrasonics_front) || (g_driveBack==true && ultrasonicGroup==ultrasonics_back))
    {
      if (wallChangeToCheck == wallchange_approaching) offset = FRONT_WALLCHANGE_DISTANCE + wallChangeOffsets[wcoff_frontApproaching];
      else offset = FRONT_WALLCHANGE_DISTANCE + wallChangeOffsets[wcoff_frontLeaving];
    }
    // Back wallchange
    else
    {
      if (wallChangeToCheck == wallchange_approaching) offset = BACK_WALLCHANGE_DISTANCE + wallChangeOffsets[wcoff_backApproaching];
      else offset = BACK_WALLCHANGE_DISTANCE + wallChangeOffsets[wcoff_backLeaving];
    }

    if (millis() - g_potWallChanges[sensor][wallChangeToCheck].timestamp < 500) // Time is not tuned!!!
    {
      // Successful detection using potential wallchange
      double corrected = g_potWallChanges[sensor][wallChangeToCheck].shadowDistanceDriven + offset;
      if (abs(g_trueDistanceDriven - corrected) <= MAX_CORRECTION_DISTANCE) // Limit correction
      {
        g_trueDistanceDriven = corrected;
      }
      g_potWallChanges[sensor][wallChangeToCheck].timestamp = 0; // Resets the timeflag to prevent double detection. If correction was too large, also prevents from
    }
    else
    {
      // Normal detection using only smooth wallchange
      #warning code also used elsewhere
      double angleCorrDistance = 0;
      if (abs(pose.angle) <= MAX_WALLCHANGE_ANGLE)
      {
        angleCorrDistance = ultrasonicLastKnownDistances[sensor][usmt_raw]*sin(DEG_TO_RAD*pose.angle); // Left side
        if (sensor==ultrasonic_RF || sensor==ultrasonic_RB) // Right side
        {
          angleCorrDistance *= -1;
        }

        offset+=angleCorrDistance; // Set the distance to write to trueDistanceDriven
      }
      
      // Actually executing the wallchange
      if (abs(g_trueDistanceDriven-offset) <= MAX_CORRECTION_DISTANCE) // Limit correction
      {
        g_trueDistanceDriven = offset;
      }
    }

    if (wallChangeToCheck == wallchange_leaving)
    {
      if (ultrasonicGroup == ultrasonics_front) stopReason = stop_frontWallChangeLeaving;
      else stopReason = stop_backWallChangeLeaving;
    }
    else
    {
      if (ultrasonicGroup == ultrasonics_front) stopReason = stop_frontWallChangeApproaching;
      else stopReason = stop_backWallChangeApproaching;
    }
  }
}

// Checks for wallchanges and sets the truedistance driven variable accordingly.
// stopReason - the variable to safe the stoppingreason in
// This function can be run by itself and does not need any special code in driveStepDriveLoop
void checkWallChanges(StoppingReason& stopReason)
{
  checkPotWallChanges();
  checkSmoothWallChanges();

  // Check all the sensors for potential wallchanges, leaving and approaching.
  // If a wallchange is detected, begin counting shadow distance.
  for (int i=0; i<2; ++i)
  {
    WallChangeType wallChangeToCheck = wallchange_approaching; // For when i=0
    if (i==1) wallChangeToCheck = wallchange_leaving;
    for (int k=1; k<ULTRASONIC_NUM; ++k)
    {
      if (g_potWallChanges[k][wallChangeToCheck].detected == true)
      {
        #warning code also used elsewhere
        double corrDistance = 0; // Default
        if (abs(pose.angle) <= MAX_WALLCHANGE_ANGLE)
        {
          corrDistance = ultrasonicLastKnownDistances[k][usmt_raw]*sin(DEG_TO_RAD*pose.angle); // Left side
          if (k==ultrasonic_RF || k==ultrasonic_RB) // Right side
          {
            corrDistance *= -1;
          }
        }
        setShadowDistance(k, wallChangeToCheck, corrDistance); // Begins the shadowdistance
        // Serial.print(g_robotAngle);Serial.print("    ");Serial.println(corrDistance);
      }
    }
  }

  for (int i=0; i<2; ++i)
  {
    WallChangeType wallChangeToCheck = wallchange_approaching; // For when i=0
    if (i==1) wallChangeToCheck = wallchange_leaving;

    for (int k=1; k<ULTRASONIC_NUM; ++k)
    {
      checkAndUseWallChange(k, wallChangeToCheck, stopReason);
    }
  }

}

void printPotWallChanges()
{
  Serial.print("APPR  ");
  Serial.print("LF:");Serial.print(g_potWallChanges[ultrasonic_LF][wallchange_approaching].detected);Serial.print(" ");
  Serial.print("LB:");Serial.print(g_potWallChanges[ultrasonic_LB][wallchange_approaching].detected);Serial.print(" ");
  Serial.print("RF:");Serial.print(g_potWallChanges[ultrasonic_RF][wallchange_approaching].detected);Serial.print(" ");
  Serial.print("RB:");Serial.print(g_potWallChanges[ultrasonic_RB][wallchange_approaching].detected);Serial.print(" ");
  Serial.println("");

  Serial.print("LEAV  ");
  Serial.print("LF:");Serial.print(g_potWallChanges[ultrasonic_LF][wallchange_leaving].detected);Serial.print(" ");
  Serial.print("LB:");Serial.print(g_potWallChanges[ultrasonic_LB][wallchange_leaving].detected);Serial.print(" ");
  Serial.print("RF:");Serial.print(g_potWallChanges[ultrasonic_RF][wallchange_leaving].detected);Serial.print(" ");
  Serial.print("RB:");Serial.print(g_potWallChanges[ultrasonic_RB][wallchange_leaving].detected);Serial.print(" ");
  Serial.println("");
}

void printWallchangeData(UltrasonicSensorEnum sensor)
{
  Serial.print("POT  ");
  Serial.print("CurrentPresent:");Serial.print(currentSensorWallStates[sensor][usmt_raw]);Serial.print(" ");
  Serial.print("PreviousPresent:");Serial.print(previousSensorWallStates[sensor][usmt_raw]);Serial.print(" ");
  Serial.println("");

  Serial.print("APPR: ");
  Serial.print("DETECT: ");
  Serial.print(g_potWallChanges[sensor][wallchange_approaching].detected);Serial.print(" ");
  Serial.print("ShadowDist: ");Serial.print(g_potWallChanges[sensor][wallchange_approaching].shadowDistanceDriven);Serial.print(" ");
  Serial.print("TIME: ");Serial.print(millis()-g_potWallChanges[sensor][wallchange_approaching].timestamp);Serial.print(" ");
  Serial.println("");

  Serial.print("LEAV: ");
  Serial.print("DETECT: ");
  Serial.print(g_potWallChanges[sensor][wallchange_leaving].detected);Serial.print(" ");
  Serial.print("ShadowDist: ");Serial.print(g_potWallChanges[sensor][wallchange_leaving].shadowDistanceDriven);Serial.print(" ");
  Serial.print("TIME: ");Serial.print(millis()-g_potWallChanges[sensor][wallchange_leaving].timestamp);Serial.print(" ");
  Serial.println("");
}


int g_reflectiveIterations = 0; // Iterations on new tile when colour was reflective
int g_blueIterations = 0; // Iterations on new tile when colour was blue
int g_totalIterations = 0; // Total iterations on new tile

// What to run inside of the driveStep loop (the driving forward-portion)
// Arguments have the same names as the variables they should accept in driveStep.
// wallToUse - which wall to follow/measure
// startAngle - what the angle is when starting the step (mathangle)
// gyroOffset - which angle the gyro indicates when the step starts (mathangle)
// dumbDistanceDriven - used to keep track of the distance travelled measured by the encoder
// stopReason - gives the reason for why the robot stopped moving
bool driveStepDriveLoop(WallSide& wallToUse, double& dumbDistanceDriven, StoppingReason& stopReason, bool& rampDriven)
{
  // One way of doing it:
  // getUltrasonics1();
  // ColourSensor::FloorColour g_floorColour = colSensor.checkFloorColour();
  // if (g_driveBack == false)
  // {
  //   switch (g_floorColour)
  //   {
  //     case ColourSensor::floor_notUpdated:
  //       break; // Do nothing
  //     case ColourSensor::floor_black:
  //       // Drive back to last point and exit the loop
  //       stopReason = stop_floorColour;
  //       return true; // Exit the loop
  //       break;
  //     case ColourSensor::floor_blue:
  //       // Go on driving and tell Marcus that there is a blue tile
  //       // stopReason = stop_floorColour;
  //       // return true; // Exit the loop
  //       break;
  //     default:
  //       // Do nothing (includes silver)
  //       break; // Potential problem with the last break statement?
  //   }
  // }
  // getUltrasonics2();
  // printUltrasonics();
  getUltrasonics();
  checkWallPresence();
  updateLastKnownDistances();
  // Deciding which wall to follow
  wallToUse = pose.getSafeWallToUse();

  // Updating the distances
  // Increment the true distance driven.
  // Done by calculating the leg parallell to the wall in the right triangle formed by the distance travelled and the lines parallell to walls in both directions (see notes on paper)
  double dumbDistanceIncrement = getDistanceDriven() - dumbDistanceDriven;
  pose.update(wallToUse, dumbDistanceIncrement);
  if (g_driveBack==true) g_trueDistanceDriven = 30 - pose.yDist;
  else g_trueDistanceDriven = pose.yDist;
  dumbDistanceDriven = getDistanceDriven();

  //pidDrive(wallToUse, startAngle, gyroOffset);
  pidDrive(wallToUse);
  // pose.print(); // Debugging

  // pose.print();
  // Serial.println();
  // printUltrasonics();
  // Serial.println();
  // printWallPresence();
  // Serial.println();
  // Serial.println();
  // Serial.println();

  // Ramp handling -------------------

  // Checking for ramps (perhaps do running average?)
  // Need to handle when you get off the ramp, so that you don't stop immediately
  if (abs(gyro.getAngleX()) > 10) addOnRampValue(true);
  else addOnRampValue(false);

  bool onRamp = getIsOnRamp();
  bool rampChange = false;
  if (onRamp != g_previousOnRampState) rampChange = true;

  if (onRamp == true)
  {
    useRampPID();
    pose.updateOnRamp(wallToUse, dumbDistanceDriven);
    if (pose.distOnRamp > 10) rampDriven = true; // Could be moved to driveStep() ?
    // Serial.print(g_horizontalDistanceDrivenOnRamp);Serial.print("    ");Serial.print(g_verticalDistanceDrivenOnRamp);Serial.print("    ");Serial.println(-gyro.getAngleX());

    lights::onRamp();

  }
  else // What to do ONLY when NOT on a ramp ------------------------------------------------------
  {
    if (rampChange == true)
    {
      pose.yDist = 15;
      g_trueDistanceDriven = pose.yDist;
      lights::turnOff(); // Could cause problems?
    }
    useNormPID();
    // Checking if you are done
    // if (ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth] < FRONT_WALL_STOPPING_TRESHOLD && g_driveBack == false) // If the robot is the correct distance away from the front wall. The goal is that ultrasonicDistanceF is 5.2 when the robot stops. Should not do when driving backwards.
    // {
    //   // lights::setColour(3, colourBase, true);
    //   // g_trueDistanceDriven = 30; // The robot has arrived
    //   stopReason = stop_frontWallPresent;
    //   // stopReason = stop_floorColour; // For debugging driving backwards
    //   // g_floorColour = ColourSensor::floor_black; // Same as line above
    //   // return true;
    // }
    //Serial.println(ultrasonicDistanceF); // Debugging

    if (g_driveBack==false && g_trueDistanceDriven >= g_targetDistance+15 - (ULTRASONIC_FRONT_OFFSET + FRONT_WALL_STOPPING_TRESHOLD) - 1 && g_frontUltrasonicUsed==true)
    {
      if (stopReason == stop_none) stopReason = stop_frontWallPresent;
      g_driveBack = false;
      return true;
    }

    if (g_trueDistanceDriven >= g_targetDistance-1.7) // Should not drive based on encoders if you have driven up the ramp // && rampDriven == false
    {
      if (stopReason == stop_none) stopReason = stop_deadReckoning;
      g_driveBack = false; // Reset the driveBack variable (do not drive back the next step)
      return true;
    }

    // Checking for ground colour (should perhaps only be done when not on ramp?)
    g_floorColour = colSensor.checkFloorColour();
    if (g_driveBack == false)
    {
      // g_baseSpeed_CMPS = 15;
      switch (g_floorColour)
      {
        case ColourSensor::floor_notUpdated:
          break; // Do nothing
        case ColourSensor::floor_unknown:
          // g_baseSpeed_CMPS = 10;
          // Serial.println("Unknown");
          break;
        case ColourSensor::floor_black:
          // Drive back to last point and exit the loop
          // Serial.println("Black");
          // sounds::tone(440, 20);
          stopWheels();
          // sounds::tone(440, 20);
          stopReason = stop_floorColour;
          return true; // Exit the loop
          break;
        case ColourSensor::floor_blue:
          // Do nothing here. Is handled below.
          break;
        case ColourSensor::floor_reflective:
          // Do nothing here. Is handled below
          break;
        default:
          // Do nothing (includes silver)
          break; // Potential problem with the last break statement?
      }

      if (g_trueDistanceDriven > 15 - 4)
      {
        ++g_totalIterations;
        if (g_floorColour==ColourSensor::floor_reflective)
        {
          ++g_reflectiveIterations;
        }
        if (g_floorColour==ColourSensor::floor_blue)
        {
          ++g_blueIterations;
        }
      }
    }

    // Checking the front touch sensor
    TouchSensorSide touchSensorState = frontSensorActivated();
    if (g_driveBack==false && (touchSensorState != touch_none))
    {
        switch (touchSensorState)
        {
          case touch_left:
            stopReason = stop_frontTouchLeft;
            break;
          case touch_right:
            stopReason = stop_frontTouchRight;
            break;
          default:
            stopReason = stop_frontTouchBoth;
            break;
        }

        g_lastTouchSensorState = touchSensorState;
        return true; // Exit the loop
    }

  } // Only do when not on ramp ends here

  // Checking for wallchanges
  if (abs(pose.angle) <= 33) // Only check wallchanges if below certain angle. Correction for angle is also done with the distance.
  {
    // checkWallChanges(stopReason);
  }
  // printWallchangeData(ultrasonic_RF);
  // Serial.println("");

  // Checking for interrupts
  if (serialcomm::checkInterrupt() == true)
  {
    if (onRamp==false && g_driveBack==false)
    {
    stopWheels();
    bool stepDriven = false;
    if (g_trueDistanceDriven >= 15) stepDriven = true;
    serialcomm::answerInterrupt(stepDriven);
    Command intrCommand = serialcomm::readCommand(true);
    if (intrCommand == command_dropKit) handleVictim(true);
    else if (intrCommand == command_none) sounds::errorBeep();
    else {} // Do nothing and continue
    }

    else // If on a ramp or driving backwards
    {
      serialcomm::cancelInterrupt();
    }
  }



  // Updates for the next loop (may not be all of themo)
  g_previousOnRampState = onRamp;
  setPreviousSensorWallStates();
  return false; // The default return - not finished
}

bool driveStep(ColourSensor::FloorColour& floorColourAhead, bool& rampDriven, TouchSensorSide& frontSensorDetectionType, double& xDistanceOnRamp, double& yDistanceOnRamp, bool continuing)
{
  // straighten();
  if (g_turboSpeed==true) g_baseSpeed_CMPS = 21;
  else g_baseSpeed_CMPS = BASE_SPEED_CMPS;
  WallSide wallToUse = wall_none; // Initialize a variable for which wall to follow
  startDistanceMeasure(); // Starts the distance measuring (encoders)
  double dumbDistanceDriven = 0;
  g_horizontalDistanceDrivenOnRamp = 0;
  g_verticalDistanceDrivenOnRamp = 0;
  g_targetDistance = 30; // The distance that you want to drive. Normally 30
  g_startDistance = 0; // Where you start. Normally 0, but different when going backwards.
  if (g_driveBack == true)
  {
    // g_targetDistance = g_trueDistanceDriven + 2;
    // g_targetDistance = 15;
    g_startDistance = g_targetDistance - pose.yDist;
    // g_targetDistance += 3;
  }
  if (continuing == true)
  {
    g_startDistance = g_trueDistanceDriven;
  }
  g_trueDistanceDriven = g_startDistance;
  dumbDistanceDriven = 0;

  // For checking for blue and reflective tiles
  g_reflectiveIterations = 0;
  g_blueIterations = 0;
  g_totalIterations = 0;

  // Get sensor data for initial values
  if (g_driveBack == false) flushDistanceArrays();
  checkWallPresence();
  setPreviousSensorWallStates();
  wallToUse = pose.getSafeWallToUse();

  // Update pose
  pose.update(wallToUse, 0);
  pose.updateGyroOffset();

  g_lastWallSide = wall_none; // Tells pidDrive that derivative term should not be used

  StoppingReason stoppingReason = stop_none;
  

  // Timer stuff
  // unsigned long timerFlag = millis();
  // int iterations = 0;

  double shouldStop = false;
  // Drive until the truDistanceDriven is 30 or greater. This is the original way I did it, but the alternative way below may be used if the later parts of this code are changed.
  while (shouldStop == false)
  {
  shouldStop = driveStepDriveLoop(wallToUse, dumbDistanceDriven, stoppingReason, rampDriven); // There does not seem to be a time difference between calling the function like this and pasting in the code
  // Serial.print(dumbDistanceDriven);
  // Serial.print("      ");
  // Serial.print(g_trueDistanceDriven);
  // Serial.print("    ");
  // Serial.println(g_targetDistance);
  // printUltrasonics();
  // printWallPresence();
  // Serial.println("");Serial.println("");
  // ++iterations;
  }
  // Serial.print("Time: ");
  // Serial.println((millis()-timerFlag)/double(iterations));


  // // Checking how far you have driven
  // // Get new sensor data
  // getUltrasonics();
  // checkWallPresence();
  // // Reset drive variables
  // startDistanceMeasure();
  // dumbDistanceDriven = 0;
  // double trueDistanceDrivenFlag = g_trueDistanceDriven;
  // // Continue driving forward if necessary (close enough to the wall in front)
  // if (stoppingReason != stop_frontWallPresent && stoppingReason != stop_floorColour && ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth] < (15-ULTRASONIC_FRONT_OFFSET + 10) && (g_floorColour != ColourSensor::floor_black) && g_driveBack == false) // && g_floorColour != ColourSensor::floor_blue // Removed due to strategy change
  // {
  //   bool throwaWayRampDriven = false; // Just to give driveStepDriveLoop someting. Is not used for anything.
  //   lights::setColour(3, colourOrange, true);
  //   shouldStop = false;
  //   while (ultrasonicCurrentDistances[ultrasonic_F][usmt_smooth] > FRONT_WALL_STOPPING_TRESHOLD) // The part about trueDistance is a failsafe in case the sensor fails && (g_trueDistanceDriven-trueDistanceDrivenFlag) < 7
  //   {
  //     driveStepDriveLoop(wallToUse, dumbDistanceDriven, stoppingReason, throwaWayRampDriven);
  //     // Serial.println(ultrasonicDistanceF);
  //   }
  //   // lights::setColour(3, colourBase, true);
  //    stoppingReason = stop_frontWallPresentFaraway;
  // }

  stopWheels();
  // Serial.println("STOPPED"); // Debugging

  g_driveBack = false;

  // Serial.print(g_floorColour); Serial.print("  ");
  // Serial.println(colSensor.lastKnownFloorColour);

  // if (colSensor.lastKnownFloorColour == ColourSensor::floor_reflective)
  // {
  //   g_floorColour = ColourSensor::floor_reflective;
  // }
  g_floorColour = colSensor.lastKnownFloorColour;


  switch (stoppingReason)
  {
    case stop_frontWallPresent:
      lights::setColour(3, colourBase, true);
      break;
    case stop_frontWallPresentFaraway:
      lights::setColour(3, colourOrange, true);
      break;
    case stop_frontWallChangeApproaching:
      lights::setColour(1, colourBase, false);
      lights::setColour(5, colourBase, true);
      break;
    case stop_frontWallChangeLeaving:
      lights::setColour(1, colourOrange, false);
      lights::setColour(5, colourOrange, true);
      break;
    case stop_backWallChangeApproaching:
      lights::setColour(7, colourBase, false);
      lights::setColour(11, colourBase, true);
      break;
    case stop_backWallChangeLeaving:
      lights::setColour(7, colourOrange, false);
      lights::setColour(11, colourOrange, true);
      break;
    case stop_deadReckoning:
      lights::setColour(6, colourOrange, false);
      lights::setColour(12, colourOrange, true);
      break;
    case stop_floorColour:
      g_driveBack = true;
      lights::floorIndicator(g_floorColour);
      break;
    case stop_frontTouchBoth:
      g_driveBack = true;
      lights::indicateFrontSensor();
      break;
    case stop_frontTouchLeft:
      lights::setColour(2, colourRed, true);
      break;
    case stop_frontTouchRight:
      lights::setColour(4, colourRed, true);
      break;
    default:
      lights::setColour(0, colourOrange, true);
      break;
  }

  // if (colSensor.lastKnownFloorColour == ColourSensor::floor_reflective) // Double check that the floor is really reflective.
  // {
  //   delay(300);
  //   colSensor.checkFloorColour();
  //   // Handle checkpoints.
  //   lights::indicateCheckpoint();
  //   delay(100);
  //   lights::turnOff();
  // }

  // Give back the floor colour
  // Should update/double-check this before sending (but not always?)

  // Check for blue
  if (double(g_blueIterations)/double(g_totalIterations) > 0.85 && g_driveBack==false) // If the ground colour is blue
  {
    floorColourAhead = ColourSensor::floor_blue;
  }
  // Check for reflective
  else if (double(g_reflectiveIterations)/double(g_totalIterations) > 0.85 && g_driveBack==false) // If the ground colour is reflective
  {
    floorColourAhead = ColourSensor::floor_reflective;
  }
  else // When not blue or reflective or if driving back
  {
    floorColourAhead = g_floorColour; // Or use last known floor colour?

    if (floorColourAhead == ColourSensor::floor_blue) // Not allowed, so set unknown instead
    {
      floorColourAhead = ColourSensor::floor_unknown;
    }
    else if (floorColourAhead == ColourSensor::floor_reflective) // Not allowed, so set unknown instead
    {
      floorColourAhead = ColourSensor::floor_unknown;
    }
  }
  


  if (rampDriven == true)
  {
    lights::rampDriven();
    delay(500);
    lights::turnOff();
  }

  if (stoppingReason == stop_frontTouchBoth || stoppingReason == stop_frontTouchLeft || stoppingReason == stop_frontTouchRight)
  {
    // Determine if the button values are still the same

    TouchSensorSide sensorActivation = frontSensorActivated();
    // If they are the same or both front sensors are detecting, continue with detection
    if (sensorActivation==g_lastTouchSensorState || sensorActivation==touch_both)
    {
      frontSensorDetectionType = sensorActivation;
    }
    else // If the sensors are not the same as during the run and only one sensor (or none) is detected, consider it a false alarm
    {
      // Do nothing because it was a false detection and try to drive again.
      // If it is not a false detection, it will hopefully be detected the next loop because of lower speed.
      // Alternatively, if a detection is done enough times with short time between, consider it a real detection.
    }

    // Determine which side is affected and relay information to main loop
  }
  else // Only straighten or wiggle when not by obstacle (will drive back next time)
  {
    pose.update();
    if (abs(15-pose.xDist) > 3 && g_driveBack == false)
    {
      if (abs(pose.angle) > MIN_CORRECTION_ANGLE && g_driveBack == false)
      {
        straighten();
        pose.update();
      }
      sideWiggleCorrection();
      pose.update();
      straighten();
    }
    else if (abs(pose.angle) > MIN_CORRECTION_ANGLE && g_driveBack == false)
    {
      straighten();
    }
  }

  xDistanceOnRamp = pose.xDistOnRamp;
  yDistanceOnRamp = pose.yDistOnRamp;

  // Debugging
  // Serial.println(xDistanceOnRamp);
  // Serial.println(yDistanceOnRamp);
  pose.update();
  pose.yDist = 0; // Reset for next move

  // Determine whether you have driven a step or not
  if (g_trueDistanceDriven > 15 && stoppingReason != stop_floorColour) return true;
  else return false;

}

bool driveStep()
{
  ColourSensor::FloorColour throwAwayColour;
  bool throwawayRampDriven = false;
  TouchSensorSide throwawayFrontSensorDetectionType = touch_none;
  double throwawayxDistance = 0;
  double throwawayyDistance = 0;
  bool continuing = false;
  return driveStep(throwAwayColour, throwawayRampDriven, throwawayFrontSensorDetectionType, throwawayxDistance, throwawayyDistance, continuing);
}

// Make a navigation decision.
// Simple algorithm just used for testing when the maze-code is not present
Command nextAction = command_none;
void makeNavDecision(Command& action)
{
  if (nextAction == command_none) { // If the given nextAction was nothing (x, -1)
    getUltrasonics();
    checkWallPresence();
    if (normalWallPresence[wall_left] && normalWallPresence[wall_front]) action = command_turnRight;
    else if (normalWallPresence[wall_left]) action = command_driveStep; // If the left wall is there and not the front wall
    else { // If the left wall disappears for any reason, including when the front wall is present
      action = command_turnLeft;
      nextAction = command_driveStep;
    }
  } else {
      action = nextAction;
      nextAction = command_none;
    }

  }


//------------------------------ Victims and rescue kits ------------------------------//

int servoPos = 10; // Servo position. Here set to starting position
const int servoLower = 5;
const int servoUpper = 176;

void servoSetup()
{
  servo.attach(4);
  servo.write(servoPos);
}

void handleVictim(double fromInterrupt)
{
  // if (fromInterrupt == true)
  // {
  // Command command = serialcomm::readCommand(true);
  // serialcomm::returnSuccess();
  // if (command != command_dropKit) return;
  // }
  // Align the robot for deployment
  TurningDirection turnDirection = cw; // Default - if the kit is on the right
  if (g_dropDirection == 'r') turnDirection = ccw; // If the kit is on the left
  // Serial.print(g_dropDirection);
  if (g_kitsToDrop != 0) turnSteps(turnDirection, 1); // Only turn if you have to drop

  int blinkCycleTime = 500; // The time for a complete blink cycle in ms
  int droppedKits = 0;


  // Simultaneous blinking and deployment of rescue kits
  servoPos = servoLower;
  long beginTime = millis();
  long rkTimeFlag = 0;
  const int rkDelay = 500; // The time between deploying rescue kits in ms.
  const int minBlinkTime = 6000; // Should be 6000, but I added 1000 (1s) for some margins in the referees perception
  lights::turnOnVictimLights(true); // For the first half blink cycle
  while (droppedKits<g_kitsToDrop || millis()-beginTime < minBlinkTime)
  {
    static long blinkTimerFlag = beginTime;
    static int direction = 1; // For keeping track of in what direction the servo is moving. 1 is back and -1 is forward
    static long loopTimeFlag = 0;

    if (millis()-loopTimeFlag > 15) // Delay for the servo movement
    {
      loopTimeFlag = millis();

      // Dropping rescue kits
      if (millis()-rkTimeFlag > rkDelay && droppedKits < g_kitsToDrop) // If enough time has passed and not all kits have been dropped
      {
        servo.write(servoPos); // Write servo position
        servoPos += direction*2; // Increment/decrement depending on which direction you are moving in.
        if (direction==1 && servoPos>servoUpper) // When moving back, if reaching endpoint
        {
          direction = -1; // Switch direction
        }
        else if (direction==-1 && servoPos<servoLower) // If moving forward, if reaching endpoint
        {
          direction = 1; // Change direction
          rkTimeFlag = millis(); // Set timeflag for delay
          ++droppedKits; // Increment the number of dropped kits
        }
      }
    }

    // Blinking
    if (millis()-blinkTimerFlag > blinkCycleTime/2 && millis()-beginTime < minBlinkTime) // If we are in the second half of the cycle and we have blinked for less than 6 seconds
    {
      lights::turnOff(); // The lights should be off
      if (millis()-blinkTimerFlag > blinkCycleTime) // When we go beyond the cycle
      {
        lights::turnOnVictimLights(false); // Turn on the lights for the next half cycle. Show is called last in the loop
        blinkTimerFlag = millis(); // Reset the time flag for the next cycle
      }
    }

    // Displaying the amount of dropped kits
    for (int i=0;i<droppedKits;++i)
    {
      lights::setColour(lights::safeIndex(1+droppedKits), colourRed, false); // Turn on a light to show the number of dropped kits
    }
    
    lights::showCustom();
  }

  // Return the robot to original orientation
  if (turnDirection == ccw) turnDirection = cw; // Reverse direction
  else turnDirection = ccw; // Reverse direction
  if (g_kitsToDrop != 0 && (g_returnAfterDrop==true || fromInterrupt==true)) turnSteps(turnDirection, 1); // Only turn if you have to drop and only turn back if necessary

  // Reset variables
  g_dropDirection = ' ';
  g_kitsToDrop = 0;
  lights::turnOff();
}


// Deprecated
// void deployRescueKit()
// {

//   for (servoPos = servoLower; servoPos<=servoUpper; servoPos += 2)
//   {
//     servo.write(servoPos);
//     delay(15);
//   }
//   delay(500);
//   for (servoPos = servoUpper; servoPos >= servoLower; servoPos -= 2)
//   {
//     servo.write(servoPos);
//     delay(15);
//   }

//   // sounds::tone(440, 200);
//   // sounds::tone(880, 200);
// }



//----------------------------- Buttons and misc. sensors -------------------------//

// Front touch sensor buttons

HardwareButton pressPlateLeft {30, false};
HardwareButton pressPlateRight {32, false};


TouchSensorSide frontSensorActivated()
{
  if (pressPlateLeft.isPressed()==true && pressPlateRight.isPressed()==true)
  {
    return touch_both;
  }
  else if (pressPlateLeft.isPressed()==true)
  {
    return touch_left;
  }
  else if (pressPlateRight.isPressed()==true)
  {
    return touch_right;
  }
  else return touch_none;
}

void serialcomm::sendLOP()
{
  Serial.println("!l");
}

void serialcomm::sendColourCal()
{
  Serial.println("!l,c");
}