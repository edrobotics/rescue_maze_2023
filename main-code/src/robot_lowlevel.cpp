#include <robot_lowlevel.h>
#include <colour_sensor.h>
// #include <ultrasonic_sensor.h>
#include <Arduino.h>
#include <MeAuriga.h>
#include <Servo.h>
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
MeUltrasonicSensor ultrasonicLF(PORT_8); // Left front
MeUltrasonicSensor ultrasonicLB(PORT_9); // Left back
MeUltrasonicSensor ultrasonicRF(PORT_7); // Right front
MeUltrasonicSensor ultrasonicRB(PORT_10); // Right back
MeUltrasonicSensor ultrasonicF(PORT_6); // Front


// Colour sensor:
ColourSensor colSensor;

// Gyro definition
MeGyro gyro(0, 0x69);

// Buzzer (for debugging)
MeBuzzer buzzer;

// RGB ring
MeRGBLed ledRing(0, 12);



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
const double WHEEL_DIAMETER = 7.3;
const double WHEEL_CIRCUMFERENCE = PI*WHEEL_DIAMETER;

// Driving
const double CMPS_TO_RPM = 1.0/WHEEL_CIRCUMFERENCE*60.0; // Constant to convert from cm/s to rpm
const double BASE_SPEED_CMPS = 15; // The base speed of driving (cm/s)
const double BASE_SPEED_RPM = CMPS_TO_RPM*BASE_SPEED_CMPS; // The base speed of driving (rpm)
double g_trueDistanceDriven = 0; // The correct driven distance. Measured as travelling along the wall and also updated when landmarks are seen
double g_targetDistance = 0; // The distance that you want to drive
double g_startDistance = 0; // The distance that you start from


// Sensor constants
const double ULTRASONIC_SPACING = 14.6; // The distance between the centers two ultrasonic sensors.
const double ULTRASONIC_FRONT_OFFSET = 9.5; // The distance from the front sensor to the center of the robot
const double ultrasonicDistanceToWall = 7.1; // The distance between the ultrasonic sensor (edge of the robot) and the wall when the robot is centered.
const double wallPresenceTreshold = 20; // Not calibrated !!!!!!!!!!!!!!!!!!!!!!!! Just a guess!!!!!!!!!!!!!!!!!!!!!!!

// Sensor data
// const int DISTANCE_MEASUREMENT_SIZE = 5; // The number of measurements in the distance arrays
// Distance arrays (maybe fill with function instead?)
double ultrasonicDistancesF[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0};
double ultrasonicDistancesLF[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0};
double ultrasonicDistancesLB[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0};
double ultrasonicDistancesRF[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0};
double ultrasonicDistancesRB[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0};
// Averaged distances
double ultrasonicDistanceLF = 0;
double ultrasonicDistanceLB = 0;
double ultrasonicDistanceRF = 0;
double ultrasonicDistanceRB = 0;
double ultrasonicDistanceF = 0;
// Whether or not the sensor detected a wall the previous time when checkWallChanges was run
bool previousLFState = false;
bool previousLBState = false;
bool previousRFState = false;
bool previousRBState = false;


// Wall presence
bool leftWallPresent = false;
bool rightWallPresent = false;
bool frontWallPresent = false;

// For gyro turning and angles
double g_currentGyroAngle = 0;
double targetGyroAngle = 0;
double g_lastWallAngle = 0; // Used to determine the angle in relation to the wall when a move ends
double g_robotAngle = 0; // The angle of the robot in relation to the wall
double g_wallDistance = 0;
double g_startWallAngle = 0;
double g_gyroOffset = 0;

// For driving decision (driving backwards)
bool g_driveBack = false;
ColourSensor::FloorColour g_floorColour = ColourSensor::floor_notUpdated;

int g_kitsToDrop = 0;
char g_dropDirection = ' ';


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

char serialcomm::readChar()
{
  while (Serial.available() == 0) {}
  return static_cast<char>(Serial.read());
}

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
  if (waitForSerial == false && Serial.available() == 0) return command_none; // Return none if Serial was not available and you should not wait
  if (waitForSerial == true)
  {
    while (Serial.available() == 0) {} // Wait while no serial is available. When continuing serial will be available
  }
  char recievedChar = readChar();
  if (recievedChar != '!') return command_invalid;
  recievedChar = readChar(); // Read the next byte (the command)
  switch (recievedChar)
  {
    case 'i': // interrupt the current action
      return command_interrupt;
      break;

    case 'd': // driveStep
      return command_driveStep;
      break;

    case 't': // turn
      recievedChar = readChar();
      if (recievedChar != ',' ) return command_invalid; // Invalid because the form was not followed

      recievedChar = readChar();
      if (recievedChar == 'l') return command_turnLeft;
      else if (recievedChar == 'r') return command_turnRight;
      else return command_invalid;
      break;

    case 'k': // drop rescue kit
      g_kitsToDrop = 0;
      // sounds::tone(220, 300);
      recievedChar = readChar();
      if (recievedChar != ',' ) return command_invalid; // Invalid because the form was not followed

      while(Serial.available() == 0) {}
      g_kitsToDrop = Serial.read() - '0';
      // sounds::tone(440, 300);
      recievedChar = readChar();
      if (recievedChar != ',' ) return command_invalid; // Invalid because the form was not followed
      // sounds::tone(695, 300);
      g_dropDirection = readChar();
      // sounds::tone(880, 700);
      return command_dropKit;
      break;

    case 'w': // get wall states
      return command_getWallStates;
      break;


    case 'r': // resume the action interrupted by interrupt
      break;

    default:
      sounds::errorBeep();
      return command_invalid;
      // break;
  }
}

Command serialcomm::readCommand()
{
  return serialcomm::readCommand(true);
}


bool serialcomm::checkInterrupt()
{
  Command command = serialcomm::readCommand(false);
  if (command == command_interrupt) return true;
  else return false;
}

void serialcomm::answerInterrupt()
{
  Serial.print("!s,i");
  Serial.println("");
}

//---------------------- Buzzer and lights (for debugging) ------------------//

void lightsAndBuzzerInit()
{
  buzzer.setpin(45);
  ledRing.setpin(44);
  ledRing.fillPixelsBak(0, 2, 1);
  lights::turnOff();
}


RGBColour colourBlack {0, 0, 0};
RGBColour colourWhite {100, 100, 100};
RGBColour colourBase {5, 42, 0};
RGBColour colourOrange {42, 5, 0};
RGBColour colourRed {150, 0, 0};
RGBColour colourBlue {0, 0, 150};
RGBColour colourError {200, 10, 0};
RGBColour colourAffirmative { 20, 150, 0};
RGBColour colourYellow {50, 50, 0};

void lights::turnOff()
{
  setColour(0, colourBlack, true);
  // ledRing.setColor(colourBlack.red, colourBlack.green, colourBlack.blue);
  // ledRing.show();
}

void lights::setColour(int index, RGBColour colour, bool showColour)
{
  ledRing.setColor(index, colour.red, colour.green, colour.blue);
  if (showColour==true) ledRing.show();
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

void lights::signalVictim()
{
  for (int i=0;i<5;++i)
  {
    setColour(0, colourWhite, false);
    setColour(3, colourRed, false);
    setColour(6, colourRed, false);
    setColour(9, colourRed, false);
    setColour(12, colourRed, true);
    delay(500);
    turnOff();
    delay(500);
  }
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

// Stops all wheels
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

// Updates the robot angles (including the lastwallangle)
void updateRobotPose()
{
  WallSide wallToUse = wall_none;
  flushDistanceArrays();
  checkWallPresence();
  if (leftWallPresent && rightWallPresent) wallToUse = wall_both;
  else if (leftWallPresent) wallToUse = wall_left;
  else if (rightWallPresent) wallToUse = wall_right;
  else wallToUse = wall_none; // If no wall was detected
  updateRobotPose(wallToUse);
  g_lastWallAngle = g_robotAngle; // I do not know if this should be here

  // Debugging
  // Serial.print(g_lastWallAngle);
  // Serial.print("  ");
  // Serial.println(g_startWallAngle);
}


// Sets the global variables for angles and distances.
// Does not update the gyro itself
// Call the distance updating functions first!
void updateRobotPose(WallSide wallSide, double& secondaryWallDistance)
{
  double tmpGyroAngle = g_currentGyroAngle;
  double tmpGyroOffset = g_gyroOffset;

  // In case both walls are present
  double secondaryRobotAngle = 0;

  if (wallSide != wall_none)
  {
    calcRobotPose(wallSide, g_robotAngle, g_wallDistance, false);
    if (wallSide == wall_both)
    {
      calcRobotPose(wall_right, secondaryRobotAngle, secondaryWallDistance, false);
      g_robotAngle = (g_robotAngle + secondaryRobotAngle)/2.0;
    }
  }
  else
  {
    centerAngle180(tmpGyroAngle, tmpGyroOffset);
    g_robotAngle = g_startWallAngle + tmpGyroAngle - tmpGyroOffset; // Safe to do because we moved the angles to 180 degrees, meaning that there will not be a zero-cross
    calcRobotPose(wallSide, g_robotAngle, g_wallDistance, true); // Is this really needed? The angle should already be up to date and the wall distance is not relevant in this case

  }

}
// Alternative way of calling (for when both sides are present)
// Call distance updates first
void updateRobotPose(WallSide wallSide)
{
  double secondaryWallDistance = 0;
  updateRobotPose(wallSide, secondaryWallDistance);
}

// Returns the distance left to turn in degrees. When you get closer, it decreases. When you have overshot, it will be negative.
// zeroCross - if the turn will cross over 0
// turningdirection - which direction you will turn. 1 is counter-clockwise and -1 is clockwise (math angles)
// tarAng - the angle you want to turn to.
// curAng - the angle you are currently at.
// The maximum allowed difference between the angles is <180
double leftToTurn(bool zeroCross, int turningDirection, double tarAng, double curAng)
{

  double targetAngle = tarAng;
  double currentAngle = curAng;
  centerAngle180(targetAngle, currentAngle);
  if (turningDirection==1)
  {
    return targetAngle - currentAngle;
  }
  else if (turningDirection == -1)
  {
    return currentAngle - targetAngle;
  }

  return -100; // If something went wrong. Normally you should never get -100 (overshoot of 100 degrees)



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


double gyroDriveCorrectionCoeff = 0.1;
// Turn using the gyro
// turnAngle - the angle to turn, in degrees. Should not be greater than 90 (only tested with 90). Positive is counter-clockwise (math angle)
// stopMoving - Whether or not the robot should stop when the function is done. Set to false when driving continuously.
// baseSpeed - Optional argument for specifying the speed to move at while turning. cm/s
void gyroTurn(double turnAngle, bool stopMoving, double baseSpeed = 0)
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
    g_currentGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ()); // Sets positive to be counter-clockwise and makes all valid values between 0 and 360
    targetGyroAngle = (g_currentGyroAngle + turnAngle);
    if (targetGyroAngle<0) {
    targetGyroAngle +=360; // If the target angle is negative, add 360 to make it between 0 and 360 (no angle should be smaller than -360)
    crossingZero = true;
  } else if (targetGyroAngle >= 360) {
    targetGyroAngle -= 360; // Should bind the value to be between 0 and 360 for positive target angles ( no angle should be 720 degrees or greater, so this should work)
    crossingZero = true;
  }
    //double speedToRun = multiplier*1.5*BASE_SPEED_CMPS*CMPS_TO_RPM;
    double speedToRun = multiplier*35/CMPS_TO_RPM;
    if (baseSpeed != 0) speedToRun = baseSpeed + speedToRun*gyroDriveCorrectionCoeff;
    runWheelSide(wheels_left, -speedToRun);
    runWheelSide(wheels_right, speedToRun);

    double varLeftToTurn = leftToTurn(crossingZero, multiplier, targetGyroAngle, g_currentGyroAngle);

    while (varLeftToTurn > 15) {
      gyro.update();
      g_currentGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
      varLeftToTurn = leftToTurn(crossingZero, multiplier, targetGyroAngle, g_currentGyroAngle);
    }

    // Slowing down in the end of the turn.
    // All of this code could be placed inside of the first while-loop, but then every iteration would take more time because of checks and the gyro would become less accurate due to that.
    speedToRun = multiplier*20/CMPS_TO_RPM;
    if (baseSpeed != 0) speedToRun = baseSpeed + speedToRun*gyroDriveCorrectionCoeff; // If driving forward, make the correction smaller
    runWheelSide(wheels_left, -speedToRun);
    runWheelSide(wheels_right, speedToRun);

    while (varLeftToTurn > 2) {
      gyro.update();
      g_currentGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
      varLeftToTurn = leftToTurn(crossingZero, multiplier, targetGyroAngle, g_currentGyroAngle);
    }

    if (stopMoving==true) stopWheels();
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
  updateRobotPose();
  g_startWallAngle = g_lastWallAngle;

  if (doCorrection == true)
  {
    turnAngle = turnAngle - g_lastWallAngle;
    // g_lastWallAngle = 0; // You should have turned perfectly. Should be replaced by actually checking how far you have turned.
  }

  double startGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
  gyroTurn(turnAngle, true);
  gyro.update();
  double endGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
  centerAngle180(endGyroAngle, startGyroAngle);
  double angleDiff = endGyroAngle - startGyroAngle;
  g_gyroOffset = gyroAngleToMathAngle(gyro.getAngleZ());// The angle measured by the gyro (absolute angle) in the beginning.

  // Serial.print(g_startWallAngle);
  // Serial.print("  ");
  updateRobotPose();
  g_startWallAngle = g_lastWallAngle + angleDiff -multiplier*90; // This does not read the actual angle, but the intended turning angle
  updateRobotPose();
  // Serial.println(g_lastWallAngle);

}

void turnSteps(TurningDirection direction, int steps)
{
  gyroTurnSteps(direction, steps, true);
  flushDistanceArrays();
}



//--------------------- Sensors --------------------------------------//


void initColourSensor()
{
  colSensor.init();
}

// Pushes curDistanceData onto the specified array
void pushBackArray(double curDistanceData, double distanceArray[DISTANCE_MEASUREMENT_SIZE])
{
  for (int i=DISTANCE_MEASUREMENT_SIZE-1; i>0;--i)
  {
    distanceArray[i] = distanceArray[i-1];
  }
  distanceArray[0] = curDistanceData;
}

// Calculates the average (distance) for the specified array
double calcDistanceAverage(double distanceArray[DISTANCE_MEASUREMENT_SIZE])
{
  double sum = 0;
  for (int i=0;i<DISTANCE_MEASUREMENT_SIZE;++i)
  {
    sum += distanceArray[i];
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
  pushBackArray(ultrasonicLF.distanceCm(35), ultrasonicDistancesLF);
  ultrasonicDistanceLF = calcDistanceAverage(ultrasonicDistancesLF);
  loopEncoders();
  gyro.update();
  delay(2);


  pushBackArray(ultrasonicRB.distanceCm(35), ultrasonicDistancesRB);
  ultrasonicDistanceRB = calcDistanceAverage(ultrasonicDistancesRB);
  loopEncoders();
  gyro.update();
  delay(2);


}

void getUltrasonics2()
{
  pushBackArray(ultrasonicRF.distanceCm(35), ultrasonicDistancesRF);
  ultrasonicDistanceRF = calcDistanceAverage(ultrasonicDistancesRF);
  loopEncoders();
  gyro.update();
  delay(2);

  pushBackArray(ultrasonicLB.distanceCm(35), ultrasonicDistancesLB);
  ultrasonicDistanceLB = calcDistanceAverage(ultrasonicDistancesLB);
  loopEncoders();
  gyro.update();
  delay(2);


  pushBackArray(ultrasonicF.distanceCm(120), ultrasonicDistancesF);
  ultrasonicDistanceF = calcDistanceAverage(ultrasonicDistancesF);

}

// Get all ultrasonic sensors and update the gyro and encoders
// Perhaps return an array in the future (or take on as a mutable(?) argument?)
void getUltrasonics()
{
  //TODO: Add limit to how often you can call it (?)

  // It seems as if the time got worse when I added the while-loops and the functions contained within
  // This loop ran instead of the delays.

  // The order of calling should be optimized to minimize interference
  // The delays are to prevent interference. 2 was too short, 10 worked, 5 seems to be working (not extensively tested)


  pushBackArray(ultrasonicLF.distanceCm(35), ultrasonicDistancesLF);
  ultrasonicDistanceLF = calcDistanceAverage(ultrasonicDistancesLF);
  loopEncoders();
  gyro.update();
  delay(2);


  pushBackArray(ultrasonicRB.distanceCm(35), ultrasonicDistancesRB);
  ultrasonicDistanceRB = calcDistanceAverage(ultrasonicDistancesRB);
  loopEncoders();
  gyro.update();
  delay(2);

  pushBackArray(ultrasonicRF.distanceCm(35), ultrasonicDistancesRF);
  ultrasonicDistanceRF = calcDistanceAverage(ultrasonicDistancesRF);
  loopEncoders();
  gyro.update();
  delay(2);

  pushBackArray(ultrasonicLB.distanceCm(35), ultrasonicDistancesLB);
  ultrasonicDistanceLB = calcDistanceAverage(ultrasonicDistancesLB);
  loopEncoders();
  gyro.update();
  delay(2);


  pushBackArray(ultrasonicF.distanceCm(120), ultrasonicDistancesF);
  ultrasonicDistanceF = calcDistanceAverage(ultrasonicDistancesF);

  // Simple functions - no running average
  // ultrasonicDistanceF = ultrasonicF.distanceCm(120);
  // delay(5);
  // ultrasonicDistanceLF = ultrasonicLF.distanceCm(35);
  // delay(5);
  // ultrasonicDistanceLB = ultrasonicLB.distanceCm(35);
  // delay(5);
  // ultrasonicDistanceRF = ultrasonicRF.distanceCm(35);
  // delay(5);
  // ultrasonicDistanceRB = ultrasonicRB.distanceCm(35);

}
// For determining wall presence for individual sensors
bool wallPresentLF = false;
bool wallPresentRF = false;
bool wallPresentLB = false;
bool wallPresentRB = false;
bool wallPresentF = false;

void printUltrasonics()
{

  Serial.print("F:");Serial.print(ultrasonicDistanceF);
  Serial.print(" LF:");Serial.print(ultrasonicDistanceLF);
  Serial.print(" LB:");Serial.print(ultrasonicDistanceLB);
  Serial.print(" RF:");Serial.print(ultrasonicDistanceRF);
  Serial.print(" RB:");Serial.print(ultrasonicDistanceRB);
  // Serial.print(" LBWall:");Serial.print(wallPresentLB);
  // Serial.print(" RBWall: ");Serial.print(wallPresentRB);
  Serial.println("");
}


// Check if the walls are present. Uses raw distance data instead of "true" distance data.
void checkWallPresence()
{
  // Individual presence
  if (ultrasonicDistanceLF < wallPresenceTreshold) wallPresentLF = true;
  else wallPresentLF = false;
  if (ultrasonicDistanceRF < wallPresenceTreshold) wallPresentRF = true;
  else wallPresentRF = false;
  if (ultrasonicDistanceLB < wallPresenceTreshold) wallPresentLB = true;
  else wallPresentLB = false;
  if (ultrasonicDistanceRB < wallPresenceTreshold) wallPresentRB = true;
  else wallPresentRB = false;
  if (ultrasonicDistanceF < wallPresenceTreshold) wallPresentF = true; // The offset here will probably have to be adjusted
  else wallPresentF = false;

  // Debugging
  // Serial.print("LF:");
  // Serial.print(wallPresentLF);
  // Serial.print("  LB:");
  // Serial.print(wallPresentLB);
  // Serial.print("  RF:");
  // Serial.print(wallPresentRF);
  // Serial.print("  RB:");
  // Serial.print(wallPresentRB);
  // Serial.println("");

  // Wall presence
  if (wallPresentLF && wallPresentLB) leftWallPresent = true;
  else leftWallPresent = false;
  if (wallPresentRF && wallPresentRB) rightWallPresent = true;
  else rightWallPresent = false;
  frontWallPresent = wallPresentF;
}

// Sets the vairable for previous wall states to the current ones
void setPreviousWallStates()
{
  previousLFState = wallPresentLF;
  previousRFState = wallPresentRF;
  previousLBState = wallPresentLB;
  previousRBState = wallPresentRB;

}


// Update the "true" distance to the wall and the angle from upright
// Other code should call the getUltrasonics() beforehand
// wallSide - which wallSide to check. wall_left, wall_right or wall_both
// angle - the variable to return the calculated angle to. Returned in degrees.
// angle (if useGyroAngle == true) - the angle calculated by the gyro. Will be used in the calculation and not modified. Degrees. 0 is forward, positive against the left wall and negative against the right wall
// trueDistance - the variable to return the calculated distance to. Returned in cm.
// useGyroAngle - whether to use the angle calculated by the gyro (true) or calculate the angle youself (false)
void calcRobotPose(WallSide wallSide, double& angle, double& trueDistance, bool useGyroAngle)
{
  double d1 = 0;
  double d2 = 0;
  if (wallSide==wall_left || wallSide == wall_both) // Always calculate for the left wall if the right wall is not present
  {
    d1 = ultrasonicDistanceLF;
    d2 = ultrasonicDistanceLB;
  }
  else if (wallSide==wall_right)
  {
    // Should this be inverted from the left side (like it is now)?
    d1 = ultrasonicDistanceRB;
    d2 = ultrasonicDistanceRF;
  }

  if (useGyroAngle==false) angle = atan((d2 - d1)/ULTRASONIC_SPACING);
  else angle *= DEG_TO_RAD; // Convert the angle to radians to execute the calculation
  trueDistance = cos(angle) * ((d1 + d2)/(double)2);
  angle *= RAD_TO_DEG; // Convert the angle to degrees

  // Debugging
  //Serial.print(angle); Serial.print("    "); Serial.println(trueDistance);
}



// Returns the struct containing information about the presence of the walls.
// The reason for using a struct is that I do not know how else to return 3 values
int getWallStates()
{
  flushDistanceArrays();
  checkWallPresence();
  uint8_t wallStates = 0;
  if (frontWallPresent) wallStates |= 0b100;
  if (leftWallPresent) wallStates |= 0b010;
  if (rightWallPresent) wallStates |= 0b001;
  return wallStates;
}





// Checking for wall changes
// Needs to be more robust
WallChangeType checkWallChanges(UltrasonicGroup ultrasonicGroup)
{
  // sounds::tone(440, 42);
  if (ultrasonicGroup == ultrasonics_front)
  {
    if (wallPresentLF != previousLFState)
    {
      previousLFState = wallPresentLF;
      if (wallPresentLF == true)
      {
        // lights::setColour(1, colourBase, true);
        return wallchange_approaching;
      }
      else
      {
        // lights::setColour(1, colourOrange, true);
        return wallchange_leaving;
      }
    }
    if (wallPresentRF != previousRFState)
    {
      previousRFState = wallPresentRF;
      if (wallPresentRF == true)
      {
        // lights::setColour(5, colourBase, true);
        return wallchange_approaching;
      }
      else
      {
        // lights::setColour(5, colourOrange, true);
        return wallchange_leaving;
      }
    }
  }
  else if (ultrasonicGroup == ultrasonics_back)
  {
    if (wallPresentLB != previousLBState)
    {
      previousLBState = wallPresentLB;
      if (wallPresentLB == true)
      {
        // lights::setColour(11, colourBase, true);
        return wallchange_approaching;
      }
      else
      {
        // lights::setColour(11, colourBase, true);
        return wallchange_leaving;
      }
    }
    if (wallPresentRB != previousRBState)
    {
      previousRBState = wallPresentRB;
      if (wallPresentRB == true)
      {
        // lights::setColour(7, colourBase, true);
        return wallchange_approaching;
      }
      else
      {
        // lights::setColour(7, colourOrange, true);
        return wallchange_leaving;
      }
    }
  }
  return wallchange_none; // If the program made it here, no wall change was detected or an incorrect parameter was given.
}




//----------------------Driving----------------------------------------//

// Drive just using the encoders
// distance - the distance to drive
// The speed is adjusted globally using the BASE_SPEED_CMPS variable.
void driveBlind(double distance, bool stopWhenDone)
{
  moveWheelSide(wheels_left, distance, BASE_SPEED_CMPS);
  moveWheelSide(wheels_right, distance, BASE_SPEED_CMPS);
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
// Variables for derivative calculation
double lastDistance = 0; // Used to calculate derivative term
unsigned long lastExecutionTime = 0; // Used to calculate derivative term
WallSide lastWallSide = wall_none; // Used to determine if the lastDistance is valid

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

// Drive with wall following. Will do one iteration, so to actually follow the wall, call it multiple times in short succession.
// wallSide - which wall to follow. Can be wall_left, wall_right or wall_both. Directions relative to the robot.
// startAngle - The angle relative to the wall for the begin of the move (degrees, mathangle)
// gyroOffset  - The angle that the gyro read for the begin of the move (degrees, mathangle)
// The last two arguments are only used if the wallSide == wall_none
void pidDrive(WallSide wallSide)
{
  // gyro.update(); // May not be needed - already done a lot in getUltrasonics()
  double secondaryWallDistance = 0; // Only used if both walls are present. The secondary is for the right wall.
  double distanceError = 0; // positive means that we are to the right of where we want to be.
  double distanceDerivative = 0; // The rate of change for the derivative term. Initialized to 0, so that it is only used if actively changed
  g_currentGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());

  updateRobotPose(wallSide, secondaryWallDistance);


  if (wallSide==wall_left)
  {
    distanceError = g_wallDistance - ultrasonicDistanceToWall;
  }
  else if (wallSide==wall_right)
  {
    distanceError = ultrasonicDistanceToWall - g_wallDistance;
  }
  else if (wallSide==wall_both)
  {
    // distanceError = g_wallDistance - ultrasonicDistanceToWall; // Do as with left wall present
    distanceError = (g_wallDistance - secondaryWallDistance)/2.0; // (left - right)/2 . It should center the robot and also change the same amount as the other ones (hence the division by 2)
    // Debugging
    // Serial.print(wallSide);
    // Serial.print("    ");
    // Serial.print(g_wallDistance);
    // Serial.print("    ");
    // Serial.print(secondaryWallDistance);
    // Serial.print("    ");
    // Serial.print(g_robotAngle);
    // Serial.println("");
  }

  if (lastWallSide == wallSide && lastWallSide != wall_none) distanceDerivative = 1000.0*(g_wallDistance - lastDistance)/(millis()-lastExecutionTime); // Calculate the derivative. (The 1000 is to make the time in seconds)
  // Update variables for the next execution loop
  lastExecutionTime = millis();
  lastDistance = g_wallDistance;
  lastWallSide = wallSide;

  double goalAngle = 0;
  if (wallSide == wall_none) { // If you do not have a wall, continue driving straight forwards with correction
    goalAngle = 0;
  } else {
    goalAngle = distanceError*distanceP + distanceDerivative*distanceD; // Calculate the angle you want depending on the distance to the wall (error) and the speed at which you approach (derivative)
  }
  double angleError = goalAngle - g_robotAngle; // Calculate the correction needed in the wheels to get to the angle
  double correction = angleP*angleError; // Calculate the correction in the wheels. Positive is counter-clockwise (math)

  // Filter out the extreme cases
  if (correction > 10) correction = 10;
  else if (correction < -10) correction = -10;

  // For driving backwards
  int multiplier = 1;
  if (g_driveBack == true) multiplier = -1;

  runWheelSide(wheels_left, multiplier*BASE_SPEED_CMPS - correction);
  runWheelSide(wheels_right, multiplier*BASE_SPEED_CMPS + correction);
  loopEncoders(); // Could maybe remove - already done in getUltrasonics()
  g_lastWallAngle = g_robotAngle; // Update the g_lastWallAngle - okay to do because this will not be read during the execution loop of pidTurn. It will only be used before.
  // Serial.println(g_robotAngle);

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


// What to run inside of the driveStep loop (the driving forward-portion)
// Arguments have the same names as the variables they should accept in driveStep.
// wallToUse - which wall to follow/measure
// startAngle - what the angle is when starting the step (mathangle)
// gyroOffset - which angle the gyro indicates when the step starts (mathangle)
// dumbDistanceDriven - used to keep track of the distance travelled measured by the encoder
// stopReason - gives the reason for why the robot stopped moving
bool driveStepDriveLoop(WallSide& wallToUse, double& dumbDistanceDriven, StoppingReason& stopReason, bool& rampDriven)
{
  getUltrasonics1();
  ColourSensor::FloorColour g_floorColour = colSensor.checkFloorColour();
  if (g_driveBack == false)
  {
    switch (g_floorColour)
    {
      case ColourSensor::floor_notUpdated:
        break; // Do nothing
      case ColourSensor::floor_black:
        // Drive back to last point and exit the loop
        stopReason = stop_floorColour;
        return true; // Exit the loop
        break;
      case ColourSensor::floor_blue:
        // Go on driving and tell Marcus that there is a blue tile
        // stopReason = stop_floorColour;
        // return true; // Exit the loop
        break;
      default:
        // Do nothing (includes silver)
        break; // Potential problem with the last break statement?
    }
  }
  getUltrasonics2();
  // printUltrasonics();
  checkWallPresence();
  // Separate this out into its own function? (deciding what wall to follow)
  if (leftWallPresent && rightWallPresent) wallToUse = wall_both;
  else if (leftWallPresent) wallToUse = wall_left;
  else if (rightWallPresent) wallToUse = wall_right;
  else wallToUse = wall_none; // If no wall was detected
  // gyro.update();


  //pidDrive(wallToUse, startAngle, gyroOffset);
  pidDrive(wallToUse);

  int multiplier = 1;
  if (g_driveBack == true) multiplier = -1;
  // Serial.println(g_driveBack);

  // Updating the distances
  // Increment the true distance driven.
  // Done by calculating the leg parallell to the wall in the right triangle formed by the distance travelled and the lines parallell to walls in both directions (see notes on paper)
  double trueDistanceIncrement = multiplier*(getDistanceDriven()-dumbDistanceDriven) * cos(abs(g_robotAngle*DEG_TO_RAD)); // Because g_lastWallAngle is updated continuosly, it can be used here. Or I can just use robotAngle instead.
  g_trueDistanceDriven += trueDistanceIncrement;
  dumbDistanceDriven = getDistanceDriven();

  
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
    g_trueDistanceDrivenOnRamp += trueDistanceIncrement;
    if (g_trueDistanceDrivenOnRamp > 10) rampDriven = true;
    // RampDrive. Do nothing special?
    lights::onRamp();
    
  }
  else // What to do ONLY when NOT on a ramp ------------------------------------------------------
  {
    if (rampChange == true)
    {
      g_trueDistanceDriven = 15;
      lights::turnOff(); // Could cause problems?
    }
    useNormPID();
    // Checking if you are done
    if (ultrasonicDistanceF < (15 - ULTRASONIC_FRONT_OFFSET + 1) && g_driveBack == false) // If the robot is the correct distance away from the front wall. The goal is that ultrasonicDistanceF is 5.2 when the robot stops. Should not do when driving backwards.
    {
      // lights::setColour(3, colourBase, true);
      // g_trueDistanceDriven = 30; // The robot has arrived
      stopReason = stop_frontWallPresent;
      // stopReason = stop_floorColour; // For debugging driving backwards
      // g_floorColour = ColourSensor::floor_black; // Same as line above
      return true;
    }
    //Serial.println(ultrasonicDistanceF); // Debugging


    if (g_trueDistanceDriven >= g_targetDistance-2) // Should not drive based on encoders if you have driven up the ramp // && rampDriven == false
    {
      if (stopReason == stop_none) stopReason = stop_deadReckoning;
      g_driveBack = false; // Reset the driveBack variable (do not drive back the next step)
      return true;
    }
  } // Only do when not on ramp ends here

  // Checking for wallchanges (needs some more robustness!)
  WallChangeType backWallCheck = wallchange_none;
  WallChangeType frontWallCheck = wallchange_none;
  if (g_driveBack == false) // Normal
  {
  backWallCheck = checkWallChanges(ultrasonics_back);
  frontWallCheck = checkWallChanges(ultrasonics_front);
  }
  else // When driving backwards front becomes back
  {
  frontWallCheck = checkWallChanges(ultrasonics_back);
  backWallCheck = checkWallChanges(ultrasonics_front);
  }
  // Do as switch statement!
  if (backWallCheck == wallchange_leaving)
  {
    // buzzer.tone(440, 30); // Debugging
    // buzzer.tone(220, 30); // Debugging
    // Serial.println("Leaving back");
    g_trueDistanceDriven = 15 + ULTRASONIC_SPACING/2.0 + 3; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as below.
    stopReason = stop_backWallChangeLeaving;
    // Serial.println("Leaving back");
  }
  if (backWallCheck == wallchange_approaching)
  {
    // buzzer.tone(220, 30); // Debugging
    // buzzer.tone(440, 30); // Debugging
    // Serial.println("Approaching back");
    g_trueDistanceDriven = 15 + ULTRASONIC_SPACING/2.0; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as below.
    stopReason = stop_backWallChangeApproaching;
    // Serial.println("Approaching back");
  }
  if (frontWallCheck == wallchange_leaving)
  {
    // buzzer.tone(880, 30); // Debugging
    // buzzer.tone(440, 30); // Debugging
    // Serial.println("Leaving Front");
    g_trueDistanceDriven = 15 - ULTRASONIC_SPACING/2.0 + 3.5; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as above.
    stopReason = stop_frontWallChangeLeaving;
  }
  else if (frontWallCheck == wallchange_approaching)
  {
    // buzzer.tone(440, 30); // Debugging
    // buzzer.tone(880, 30); // Debugging
    // Serial.println("Approaching front");
    g_trueDistanceDriven = 15 - ULTRASONIC_SPACING/2 - 0.5; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as above.
    stopReason = stop_frontWallChangeApproaching;
  }

  // Serial.println(g_trueDistanceDriven);

  // printUltrasonics();

  // lights::turnOff();



    // Checking for interrupts
  if (serialcomm::checkInterrupt() == true)
  {
    stopWheels();
    serialcomm::clearBuffer();
    serialcomm::answerInterrupt();
    bool stepDriven = false;
    if (g_trueDistanceDriven > 15) stepDriven = true;
    handleVictim(true);
    serialcomm::returnAnswer(stepDriven);
    delay(100); // To prevent too rapid serial communication
    // serialcomm::returnSuccess();
    serialcomm::clearBuffer();
  }


  // Checking the front touch sensor
  if (frontSensorActivated() == true && g_driveBack == false)
  {
    stopReason = stop_frontSensor;
    return true; // Exit the loop
  }

  // Checking for ground colour (should perhaps only be done when not on ramp?)
  g_floorColour = colSensor.checkFloorColour();
  if (g_driveBack == false)
  {
    switch (g_floorColour)
    {
      case ColourSensor::floor_notUpdated:
        break; // Do nothing
      case ColourSensor::floor_black:
        // Drive back to last point and exit the loop
        stopReason = stop_floorColour;
        return true; // Exit the loop
        break;
      case ColourSensor::floor_blue:
        // Go on driving and tell Marcus that there is a blue tile
        // stopReason = stop_floorColour;
        // return true; // Exit the loop
        break;
      default:
        // Do nothing (includes silver)
        break; // Potential problem with the last break statement?
    }
  }


  g_previousOnRampState = onRamp; // Update for the next loop
  return false; // The default return - not finished
}


bool driveStep(ColourSensor::FloorColour& floorColourAhead, bool& rampDriven, bool& frontSensorDetected)
{
  WallSide wallToUse = wall_none; // Initialize a variable for which wall to follow
  startDistanceMeasure(); // Starts the distance measuring (encoders)
  double dumbDistanceDriven = 0;
  g_targetDistance = 30; // The distance that you want to drive. Normally 30
  g_startDistance = 0; // Where you start. Normally 0, but different when going backwards.
  if (g_driveBack == true)
  {
    // g_targetDistance = g_trueDistanceDriven + 2;
    // g_targetDistance = 15;
    g_startDistance = g_trueDistanceDriven;
  }
  g_trueDistanceDriven = 0;
  dumbDistanceDriven = 0;

  // Get sensor data for initial values
  if (g_driveBack == false) flushDistanceArrays();
  // getUltrasonics(); // Should not be needed
  checkWallPresence();
  setPreviousWallStates();
  if (leftWallPresent && rightWallPresent) wallToUse = wall_both;
  else if (leftWallPresent) wallToUse = wall_left;
  else if (rightWallPresent) wallToUse = wall_right;
  // Else no wall was detected which is the default value of wallToUse.


  // Calculate the angles for use by pidDrive
  if (wallToUse == wall_none) {

    g_startWallAngle = g_lastWallAngle; // The new wallAngle is the same as the previously set one
  } else {
    calcRobotPose(wallToUse, g_startWallAngle, g_wallDistance, false);
  }

  gyro.update();
  g_gyroOffset = gyroAngleToMathAngle(gyro.getAngleZ());// The angle measured by the gyro (absolute angle) in the beginning.


  lastWallSide = wall_none; // Tells pidDrive that derivative term should not be used



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
  // ++iterations;
  }
  // Serial.print("Time: ");
  // Serial.println((millis()-timerFlag)/double(iterations));


  // Checking how far you have driven
  // Get new sensor data
  getUltrasonics();
  checkWallPresence();
  // Reset drive variables
  startDistanceMeasure();
  dumbDistanceDriven = 0;
  double trueDistanceDrivenFlag = g_trueDistanceDriven;
  // Continue driving forward if necessary (close enough to the wall in front)
  if (stoppingReason != stop_frontWallPresent && stoppingReason != stop_floorColour && ultrasonicDistanceF < (15-ULTRASONIC_FRONT_OFFSET + 10) && (g_floorColour != ColourSensor::floor_black) && g_driveBack == false) // && g_floorColour != ColourSensor::floor_blue // Removed due to strategy change
  {
    bool throwaWayRampDriven = false; // Just to give driveStepDriveLoop someting. Is not used for anything.
    lights::setColour(3, colourOrange, true);
    shouldStop = false;
    while (ultrasonicDistanceF > (15 - ULTRASONIC_FRONT_OFFSET + 1)) // The part about trueDistance is a failsafe in case the sensor fails && (g_trueDistanceDriven-trueDistanceDrivenFlag) < 7
    {
      driveStepDriveLoop(wallToUse, dumbDistanceDriven, stoppingReason, throwaWayRampDriven);
      // Serial.println(ultrasonicDistanceF);
    }
    // lights::setColour(3, colourBase, true);
     stoppingReason = stop_frontWallPresentFaraway;
  }

  stopWheels();

  g_driveBack = false;

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
    case stop_frontSensor:
      g_driveBack = true;
      frontSensorDetected = true;
      lights::indicateFrontSensor();
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
  // }
  if (colSensor.lastKnownFloorColour == ColourSensor::floor_blue)
  {
    delay(5000);
  }

  // Give back the floor colour
  // Should update/double-check this before sending (but not always?)
  floorColourAhead = g_floorColour; // Or use last known floor colour?

  // Give an accurate angle measurement for the next step
  // This should probably be separated out into its own function
  // flushDistanceArrays();
  // checkWallPresence();
  // if (leftWallPresent && rightWallPresent) wallToUse = wall_both;
  // else if (leftWallPresent) wallToUse = wall_left;
  // else if (rightWallPresent) wallToUse = wall_right;
  // else wallToUse = wall_none; // If no wall was detected
  // updateRobotPose(wallToUse);
  // g_lastWallAngle = g_robotAngle;
  updateRobotPose(); // Replaces the previous code


  // Determine whether you have driven a step or not
  if (g_trueDistanceDriven > 15 && stoppingReason != stop_floorColour) return true;
  else return false;

  // The current angle to the wall is already stored by pidDrive in g_lastWallAngle (or not since my changes?)

}

bool driveStep()
{
  ColourSensor::FloorColour throwAwayColour;
  bool throwawayRampDriven = false;
  bool throwawayFrontSensorDetected = false;
  return driveStep(throwAwayColour, throwawayRampDriven, throwawayFrontSensorDetected);
}

// Make a navigation decision.
// Simple algorithm just used for testing when the maze-code is not present
Command nextAction = command_none;
void makeNavDecision(Command& action)
{
  if (nextAction == command_none) { // If the given nextAction was nothing (x, -1)
    getUltrasonics();
    checkWallPresence();
    if (leftWallPresent && frontWallPresent) action = command_turnRight;
    else if (leftWallPresent) action = command_driveStep; // If the left wall is there and not the front wall
    else { // If the left wall disappears for any reason, including when the front wall is present
      action = command_turnLeft;
      nextAction = command_driveStep;
    }
  } else {
      action = nextAction;
      nextAction = command_none;
    }

  }



void testWallChanges()
{
  getUltrasonics();
  checkWallPresence();
  WallChangeType backWallCheck = checkWallChanges(ultrasonics_back);
  WallChangeType frontWallCheck = checkWallChanges(ultrasonics_front);
  printUltrasonics();
  // Do as switch statement!
  if (backWallCheck == wallchange_leaving)
  {
    // buzzer.tone(440, 30); // Debugging
    // buzzer.tone(220, 30); // Debugging
    // Serial.println("Leaving back");
    g_trueDistanceDriven = 15 + ULTRASONIC_SPACING/2.0 + 7; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as below.
    // stopReason = stop_backWallChangeLeaving;
    // Serial.println("Leaving back");
    delay(500);
  }
  if (backWallCheck == wallchange_approaching)
  {
    // buzzer.tone(220, 30); // Debugging
    // buzzer.tone(440, 30); // Debugging
    // Serial.println("Approaching back");
    g_trueDistanceDriven = 15 + ULTRASONIC_SPACING/2.0 - 1; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as below.
    // stopReason = stop_backWallChangeApproaching;
    // Serial.println("Approaching back");
    delay(500);
  }
  if (frontWallCheck == wallchange_leaving)
  {
    // buzzer.tone(880, 30); // Debugging
    // buzzer.tone(440, 30); // Debugging
    // Serial.println("Leaving Front");
    g_trueDistanceDriven = 15 - ULTRASONIC_SPACING/2 + 7; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as above.
    // stopReason = stop_frontWallChangeLeaving;
  }
  else if (frontWallCheck == wallchange_approaching)
  {
    // buzzer.tone(440, 30); // Debugging
    // buzzer.tone(880, 30); // Debugging
    // Serial.println("Approaching front");
    g_trueDistanceDriven = 15 - ULTRASONIC_SPACING/2 - 1; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as above.
    // stopReason = stop_frontWallChangeApproaching;
  }
  printUltrasonics();

  lights::turnOff();
}


//------------------------------ Victims and rescue kits ------------------------------//


void signalVictim()
{
  lights::signalVictim();
}

void handleVictim(double fromInterrupt)
{
  if (fromInterrupt == true)
  {
  Command command = serialcomm::readCommand(true);
  serialcomm::clearBuffer();
  if (command != command_dropKit) return;
  }
  // Align the robot for deployment
  TurningDirection turnDirection = cw; // Default - if the kit is on the right
  if (g_dropDirection == 'r') turnDirection = ccw; // If the kit is on the left
  // Serial.print(g_dropDirection);
  if (g_kitsToDrop != 0) turnSteps(turnDirection, 1); // Only turn if you have to drop

  signalVictim();
  for (int i=0; i<g_kitsToDrop; ++i)
  {
    lights::setColour(i+2, colourRed, true);
    deployRescueKit();
    delay(500);
  }

  // Return the robot to original orientation
  if (turnDirection == ccw) turnDirection = cw; // Reverse direction
  else turnDirection = ccw; // Reverse direction
  if (g_kitsToDrop != 0) turnSteps(turnDirection, 1); // Only turn if you have to drop

  // Reset variables
  g_dropDirection = ' ';
  g_kitsToDrop = 0;
  lights::turnOff();
}

int servoPos = 10; // Servo position. Here set to starting position
const int servoLower = 5;
const int servoUpper = 170;

void servoSetup()
{
  servo.attach(4);
  servo.write(servoPos);
}

void deployRescueKit()
{

  for (servoPos = servoLower; servoPos<=servoUpper; servoPos += 2)
  {
    servo.write(servoPos);
    delay(15);
  }
  delay(500);
  for (servoPos = servoUpper; servoPos >= servoLower; servoPos -= 2)
  {
    servo.write(servoPos);
    delay(15);
  }

  // sounds::tone(440, 200);
  // sounds::tone(880, 200);
}



//----------------------------- Buttons and misc. sensors -------------------------//

// Front touch sensor buttons
const int pressPlateSW1 = 34;
const int pressPlateSW2 = 36;

void initSwitches()
{
  pinMode(pressPlateSW1, INPUT_PULLUP);
  pinMode(pressPlateSW2, INPUT_PULLUP);
}

bool frontSensorActivated()
{
  if (digitalRead(pressPlateSW1) == LOW || digitalRead(pressPlateSW2) == LOW)
  {
    return true;
  }
  else return false;
}

void serialcomm::sendLOP()
{
  Serial.println("!l");
}