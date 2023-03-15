#include <robot_lowlevel.h>
#include <colour_sensor.h>
// #include <ultrasonic_sensor.h>
#include <Arduino.h>
#include <MeAuriga.h>
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


// Ultrasonic sensor definitions
MeUltrasonicSensor ultrasonicLF(PORT_8); // Left front
MeUltrasonicSensor ultrasonicLB(PORT_9); // Left back
MeUltrasonicSensor ultrasonicRF(PORT_6); // Right front
MeUltrasonicSensor ultrasonicRB(PORT_10); // Right back
MeUltrasonicSensor ultrasonicF(PORT_7); // Front


// Colour sensor:
ColourSensor colourSensor;

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
const double WHEEL_DISTANCE = 13.2+0.4; // Diameter of the wheelbase (distance between the wheels)
const double WHEELBASE_CIRCUMFERENCE = PI*WHEEL_DISTANCE;

// Driving
const double CMPS_TO_RPM = 1.0/WHEEL_CIRCUMFERENCE*60.0; // Constant to convert from cm/s to rpm
const double BASE_SPEED_CMPS = 30; // The base speed of driving (cm/s)
const double BASE_SPEED_RPM = CMPS_TO_RPM*BASE_SPEED_CMPS; // The base speed of driving (rpm)
double trueDistanceDriven = 0; // The correct driven distance. Measured as travelling along the wall and also updated when landmarks are seen


// Sensor constants
const double ultrasonicSpacing = 14; // The distance between the centers two ultrasonic sensors.
const double ultrasonicFrontOffset = 10; // The distance from the front sensor to the center of the robot
const double ultrasonicDistanceToWall = 7.1; // The distance between the ultrasonic sensor (edge of the robot) and the wall when the robot is centered.
const double wallPresenceTreshold = 20; // Not calibrated !!!!!!!!!!!!!!!!!!!!!!!! Just a guess!!!!!!!!!!!!!!!!!!!!!!!

// Sensor data
// const int DISTANCE_MEASUREMENT_SIZE = 5; // The number of measurements in the distance arrays
// Distance arrays (maybe fill with function instead?)
double ultrasonicDistancesF[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0, 0, 0};
double ultrasonicDistancesLF[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0, 0, 0};
double ultrasonicDistancesLB[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0, 0, 0};
double ultrasonicDistancesRF[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0, 0, 0};
double ultrasonicDistancesRB[DISTANCE_MEASUREMENT_SIZE] = {0, 0, 0, 0, 0};
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
double currentGyroAngle = 0;
double targetGyroAngle = 0;
double lastWallAngle = 0; // Used to determine the angle in relation to the wall when a move ends


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
  Serial.write(answer);
  Serial.println("");
  // Serial.write('\n');
  Serial.flush();
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

Command serialcomm::readCommand()
{
  while (Serial.available() == 0) {} // Wait while no serial is available. When continuing serial will be available
  char recievedChar = readChar();
  if (recievedChar != '!') return command_invalid;
  recievedChar = readChar(); // Read the next byte (the command)
  switch (recievedChar)
  {
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
      break;
    
    case 'w': // get wall states
      return command_getWallStates;
      break;
    
    case 'i': // interrupt the current action
      break;
    
    case 'r': // resume the action interrupted by interrupt
      break;
    
    default:
      return command_invalid;
  }
  
}

//---------------------- Buzzer and lights (for debugging) ------------------//

void lightsAndBuzzerInit()
{
  buzzer.setpin(45); // Should not really be here but what other choice is there?
  ledRing.setpin(44);
  ledRing.fillPixelsBak(0, 2, 1);
  lights::turnOff();
}


RGBColour colourBlack {0, 0, 0};
RGBColour colourBase {5, 42, 0};
RGBColour colourOrange {42, 20, 0};
RGBColour colourError {200, 10, 0};
RGBColour colourAffirmative { 20, 150, 0};

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

void lights::showDirection(lights::LedDirection direction)
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
    setColour(ledIndex, colourBase, false);
    // ledRing.setColor(ledIndex, colourBase.red, colourBase.green, colourBase.blue);
  }
  ledRing.show();
}


void lights::affirmativeBlink()
{
  turnOff();
  for (int i=0;i<3;++i)
  {
    delay(130);
    setColour(0, colourBase, true);
    // ledRing.setColor(colourBase.red, colourBase.green, colourBase.blue);
    // ledRing.show();
    delay(60);
    turnOff();
  }
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
    if (turnAngle > 170) turnAngle = 90;
    if (turnAngle < -170) turnAngle = -90;
    
    gyro.update();
    currentGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ()); // Sets positive to be counter-clockwise and makes all valid values between 0 and 360
    targetGyroAngle = (currentGyroAngle + turnAngle);
    if (targetGyroAngle<0) {
    targetGyroAngle +=360; // If the target angle is negative, add 360 to make it between 0 and 360 (no angle should be smaller than -360)
    crossingZero = true;
  } else if (targetGyroAngle >= 360) {
    targetGyroAngle -= 360; // Should bind the value to be between 0 and 360 for positive target angles ( no angle should be 720 degrees or greater, so this should work)
    crossingZero = true;
  }
    //double speedToRun = multiplier*1.5*BASE_SPEED_CMPS*CMPS_TO_RPM;
    double speedToRun = multiplier*69/CMPS_TO_RPM;
    if (baseSpeed != 0) speedToRun = baseSpeed + speedToRun*gyroDriveCorrectionCoeff;
    runWheelSide(wheels_left, -speedToRun);
    runWheelSide(wheels_right, speedToRun);

    double varLeftToTurn = leftToTurn(crossingZero, multiplier, targetGyroAngle, currentGyroAngle);

    while (varLeftToTurn > 15) {
      gyro.update();
      currentGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
      varLeftToTurn = leftToTurn(crossingZero, multiplier, targetGyroAngle, currentGyroAngle);
    }

    // Slowing down in the end of the turn.
    // All of this code could be placed inside of the first while-loop, but then every iteration would take more time because of checks and the gyro would become less accurate due to that.
    speedToRun = multiplier*30/CMPS_TO_RPM;
    if (baseSpeed != 0) speedToRun = baseSpeed + speedToRun*gyroDriveCorrectionCoeff; // If driving forward, make the correction smaller
    runWheelSide(wheels_left, -speedToRun);
    runWheelSide(wheels_right, speedToRun);

    while (varLeftToTurn > 2) {
      gyro.update();
      currentGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
      varLeftToTurn = leftToTurn(crossingZero, multiplier, targetGyroAngle, currentGyroAngle);
    }
    
    if (stopMoving==true) stopWheels();
}

// Turns the specified steps (90 degrees) in the direction specified above.
// Automatic correction for the last angle to the wall can be specified by the last argument. Make sure that the lastWallAngle is up to date!
// direction - cw (clockwise) or ccw (counter-clockwise) turn.
// steps - the amount of 90-degree turns to do in the chosen direction. (NOT YET IMPLEMENTED!)
// doCorrection - Whether or not you should correct for the lastWallAngle
void gyroTurnSteps(TurningDirection direction, int steps, bool doCorrection)
{
  int multiplier=-1;
  if (direction==ccw) multiplier=1;
  double turnAngle = multiplier*90;

  if (doCorrection == true)
  {
    turnAngle = turnAngle - lastWallAngle;
    lastWallAngle = 0; // You should have turned perfectly. Should be replaced by actually checking how far you have turned.
  }


  gyroTurn(turnAngle, true);

}

void turnSteps(TurningDirection direction, int steps)
{
  gyroTurnSteps(direction, steps, true);
  flushDistanceArrays();
}



//--------------------- Sensors --------------------------------------//


void initColourSensor()
{
  colourSensor.init();
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
      delay(20);
    }
}

// Get all ultrasonic sensors
// Perhaps return an array in the future (or take on as a mutable(?) argument?)
void getUltrasonics()
{
  //TODO: Add limit to how often you can call it (?)

  // The order of calling should be optimized to minimize interference
  pushBackArray(ultrasonicLF.distanceCm(), ultrasonicDistancesLF);
  ultrasonicDistanceLF = calcDistanceAverage(ultrasonicDistancesLF);
  
  pushBackArray(ultrasonicRB.distanceCm(), ultrasonicDistancesRB);
  ultrasonicDistanceRB = calcDistanceAverage(ultrasonicDistancesRB);
  
  pushBackArray(ultrasonicF.distanceCm(), ultrasonicDistancesF);
  ultrasonicDistanceF = calcDistanceAverage(ultrasonicDistancesF);

  pushBackArray(ultrasonicLB.distanceCm(), ultrasonicDistancesLB);
  ultrasonicDistanceLB = calcDistanceAverage(ultrasonicDistancesLB);

  pushBackArray(ultrasonicRF.distanceCm(), ultrasonicDistancesRF);
  ultrasonicDistanceRF = calcDistanceAverage(ultrasonicDistancesRF);

}

void printUltrasonics()
{
  Serial.print(ultrasonicDistanceF); Serial.print(", ");
  Serial.print(ultrasonicDistanceLF); Serial.print(", ");
  Serial.print(ultrasonicDistanceLB); Serial.print(", ");
  Serial.print(ultrasonicDistanceRF); Serial.print(", ");
  Serial.print(ultrasonicDistanceRB);
  Serial.println("");
}

// For determining wall presence for individual sensors
bool wallPresentLF = false;
bool wallPresentRF = false;
bool wallPresentLB = false;
bool wallPresentRB = false;
bool wallPresentF = false;

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

  //Serial.print(wallPresentLF); Serial.print(" "); Serial.print(wallPresentLB); Serial.print(" "); Serial.print(wallPresentRF); Serial.print(" "); Serial.println(wallPresentRB); // Debugging

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

  if (useGyroAngle==false) angle = atan((d2 - d1)/ultrasonicSpacing);
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
  getUltrasonics();
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

  if (ultrasonicGroup == ultrasonics_front)
  {
    if (wallPresentLF != previousLFState)
    {
      previousLFState = wallPresentLF;
      if (wallPresentLF == true)
      {
        lights::setColour(1, colourBase, true);
        return wallchange_approaching;
      }
      else
      {
        lights::setColour(1, colourOrange, true);
        return wallchange_leaving;
      } 
    }
    if (wallPresentRF != previousRFState)
    {
      previousRFState = wallPresentRF;
      if (wallPresentRF == true)
      {
        lights::setColour(5, colourBase, true);
        return wallchange_approaching;
      }
      else
      {
        lights::setColour(5, colourOrange, true);
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
        lights::setColour(11, colourBase, true);
        return wallchange_approaching;
      }
      else
      {
        lights::setColour(11, colourBase, true);
        return wallchange_leaving;
      } 
    }
    if (wallPresentRB != previousRBState)
    {
      previousRBState = wallPresentRB;
      if (wallPresentRB == true) 
      {
        lights::setColour(7, colourBase, true);
        return wallchange_approaching;
      }
      else
      {
        lights::setColour(7, colourOrange, true);
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

double startPositionEncoderLF = 0;
double startPositionEncoderLB = 0;
double startPositionEncoderRF = 0;
double startPositionEncoderRB = 0;


void startDistanceMeasure()
{
  startPositionEncoderLF = encoderLF.getCurPos();
  startPositionEncoderLB = encoderLB.getCurPos();
  startPositionEncoderRF = encoderRF.getCurPos();
  startPositionEncoderRB = encoderRB.getCurPos();
}

long turns = 0;

void testDistanceMeasureLeft()
{
  double distanceEncoderLF = encoderLF.getCurPos()-startPositionEncoderLF;
  Serial.println(distanceEncoderLF);
}

void testDistanceMeasureRight()
{
  double distanceEncoderRF = encoderRF.getCurPos()-startPositionEncoderRF;
  Serial.println(distanceEncoderRF);
}

// Returns the distance driven by the robot since startDistanceMeasure() was called. Return is in cm.
// Idea: Handle if one encoder is very off?
double getDistanceDriven()
{
  double distanceEncoderLF = encoderLF.getCurPos()-startPositionEncoderLF;
  double distanceEncoderLB = encoderLB.getCurPos()-startPositionEncoderLB;
  double distanceEncoderRF = -(encoderRF.getCurPos()-startPositionEncoderRF);
  double distanceEncoderRB = -(encoderRB.getCurPos()-startPositionEncoderRB);

  return ((distanceEncoderLF+distanceEncoderLB+distanceEncoderRF+distanceEncoderRB)/4.0)/360*WHEEL_CIRCUMFERENCE; // Returns the average

}

// PID coefficients for wall following (in the process of tuning)
// The comments after the coefficients are a history of coefficients that worked allright
double angleP = 1; //    1  , 1, 1  ,
double distanceP = 3; // 2.2, 3, 3  ,
double distanceD = 1; // 0, 1, 0.5,
// Variables for derivative calculation
double lastDistance = 0; // Used to calculate derivative term
unsigned long lastExecutionTime = 0; // Used to calculate derivative term
WallSide lastWallSide = wall_none; // Used to determine if the lastDistance is valid

// Drive with wall following. Will do one iteration, so to actually follow the wall, call it multiple times in short succession.
// wallSide - which wall to follow. Can be wall_left, wall_right or wall_both. Directions relative to the robot.
// startAngle - The angle relative to the wall for the begin of the move (degrees, mathangle)
// gyroOffset  - The angle that the gyro read for the begin of the move (degrees, mathangle)
// The last two arguments are only used if the wallSide == wall_none
void pidDrive(WallSide wallSide, double startAngle, double gyroOffset)
{
  gyro.update();
  double wallDistance = 0;
  double robotAngle = 0;
  double secondaryWallDistance = 0; // Only used if both walls are present. The secondary is for the right wall.
  double secondaryRobotAngle = 0; // Only used if both walls are present. The secondary is for the right wall.
  double distanceError = 0; // positive means that we are to the right of where we want to be.
  double distanceDerivative = 0; // The rate of change for the derivative term. Initialized to 0, so that it is only used if actively changed
  currentGyroAngle = gyroAngleToMathAngle(gyro.getAngleZ());
  double tmpGyroOffset = gyroOffset;
  getUltrasonics();
  if (wallSide != wall_none)
  {
    calcRobotPose(wallSide, robotAngle, wallDistance, false); // Can be used regardless of which side is being used
    if (wallSide == wall_both)
    {
      calcRobotPose(wall_right, secondaryRobotAngle, secondaryWallDistance, false);
    }
  }
  else // If there are not walls, calculate the angles to be able to drive with them
  {
    centerAngle180(currentGyroAngle, tmpGyroOffset);
    robotAngle = startAngle + currentGyroAngle - tmpGyroOffset; // Safe to do because we moved the angles to 180 degrees, meaning that there will not be a zero-cross
    calcRobotPose(wallSide, robotAngle, wallDistance, true);
  }
  
  
  if (wallSide==wall_left)
  {
    distanceError = wallDistance - ultrasonicDistanceToWall;
  }
  else if (wallSide==wall_right)
  { 
    distanceError = ultrasonicDistanceToWall - wallDistance;
  }
  else if (wallSide==wall_both)
  {
    robotAngle = (robotAngle + secondaryRobotAngle)/2.0; // Take the average angle of the two
    distanceError = (wallDistance - secondaryWallDistance)/2.0; // (left - right)/2 . It should center the robot and also change the same amount as the other ones (hence the division by 2)
  }

  if (lastWallSide == wallSide && lastWallSide != wall_none) distanceDerivative = 1000.0*(wallDistance - lastDistance)/(millis()-lastExecutionTime); // Calculate the derivative. (The 1000 is to make the time in seconds)
  // Update variables for the next execution loop
  lastExecutionTime = millis();
  lastDistance = wallDistance;
  lastWallSide = wallSide;

  double goalAngle = 0;
  if (wallSide == wall_none) { // If you do not have a wall, continue driving straight forwards with correction
    goalAngle = 0;
  } else {
    goalAngle = distanceError*distanceP + distanceDerivative*distanceD; // Calculate the angle you want depending on the distance to the wall (error) and the speed at which you approach (derivative)
  }
  double angleError = goalAngle-robotAngle; // Calculate the correction needed in the wheels to get to the angle
  double correction = angleP*angleError; // Calculate the correction in the wheels. Positive is counter-clockwise (math)

  // Filter out the extreme cases
  if (correction > 10) correction = 10;
  else if (correction < -10) correction = -10;


  runWheelSide(wheels_left, BASE_SPEED_CMPS - correction);
  runWheelSide(wheels_right, BASE_SPEED_CMPS + correction);
  loopEncoders();
  lastWallAngle = robotAngle; // Update the lastWallAngle - okay to do because this will not be read during the execution loop of pidTurn. It will only be used before.

  //Serial.println(correction); // Debugging

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

// What to run inside of the driveStep loop (the driving forward-portion)
// Arguments have the same names as the variables they should accept in driveStep.
// wallToUse - which wall to follow/measure
// startAngle - what the angle is when starting the step (mathangle)
// gyroOffset - which angle the gyro indicates when the step starts (mathangle)
// dumbDistanceDriven - used to keep track of the distance travelled measured by the encoder
void driveStepDriveLoop(WallSide& wallToUse, double& startAngle, double& gyroOffset, double& dumbDistanceDriven)
{
  gyro.update();
  getUltrasonics();
  // printUltrasonics();
  checkWallPresence();
  // Separate this out into its own function? (deciding what wall to follow)
  if (leftWallPresent && rightWallPresent) wallToUse = wall_both;
  else if (leftWallPresent) wallToUse = wall_left;
  else if (rightWallPresent) wallToUse = wall_right;
  else wallToUse = wall_none; // If no wall was detected
  gyro.update();


  //pidDrive(wallToUse, startAngle, gyroOffset);
  pidDrive(wallToUse, startAngle, gyroOffset);

  // Updating the distances
  // Increment the true distance driven.
  // Done by calculating the leg parallell to the wall in the right triangle formed by the distance travelled and the lines parallell to walls in both directions (see notes on paper)
  trueDistanceDriven += (getDistanceDriven()-dumbDistanceDriven) * cos(abs(lastWallAngle*DEG_TO_RAD));
  dumbDistanceDriven = getDistanceDriven();

  // Checking if you are done
  if (ultrasonicDistanceF < (15 - ultrasonicFrontOffset + 3)) // If the robot is the correct distance away from the front wall. The goal is that ultrasonicDistanceF is 5.2 when the robot stops.
  {
    lights::setColour(3, colourBase, true);
    trueDistanceDriven = 30; // The robot has arrived
  }
  //Serial.println(ultrasonicDistanceF); // Debugging

  // Checking for wallchanges (needs some more robustness!)
  WallChangeType backWallCheck = checkWallChanges(ultrasonics_back);
  WallChangeType frontWallCheck = checkWallChanges(ultrasonics_front);
  if (backWallCheck == wallchange_leaving)
  {
    // buzzer.tone(440, 30); // Debugging
    // buzzer.tone(220, 30); // Debugging
    // Serial.println("Leaving back");
    trueDistanceDriven = 15 + ultrasonicSpacing/2.0 + 7; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as below.
  }
  else if (backWallCheck == wallchange_approaching)
  {
    // buzzer.tone(220, 30); // Debugging
    // buzzer.tone(440, 30); // Debugging
    // Serial.println("Approaching back");
    trueDistanceDriven = 15 + ultrasonicSpacing/2.0 - 1; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as below.
  }
  if (frontWallCheck == wallchange_leaving)
  {
    // buzzer.tone(880, 30); // Debugging
    // buzzer.tone(440, 30); // Debugging
    // Serial.println("Leaving Front");
    trueDistanceDriven = 15 - ultrasonicSpacing/2 + 7; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as above.
  }
  else if (frontWallCheck == wallchange_approaching)
  {
    // buzzer.tone(440, 30); // Debugging
    // buzzer.tone(880, 30); // Debugging
    // Serial.println("Approaching front");
    trueDistanceDriven = 15 - ultrasonicSpacing/2 - 1; // The math should be correct, but the robot drives too far without the addition. The same amount of wrong as above.
  }

  lights::turnOff();

  // Checking for ground colour
  ColourSensor::FloorColour identifiedColour = colourSensor.checkFloorColour();

  // switch (identifiedColour)
  // {
  //   case ColourSensor::floor_notUpdated:
  //     break; // Do nothing
  //   case ColourSensor::floor_black:
  //     // Exit the loop somehow
  //     break;
  //   case ColourSensor::floor_blue:
  //     // Exit the loop somehow
  //     break;
  //   default:
  //     // Do nothing
  //     break; // Potential problem with the last break statement?
  // }
  
}


bool driveStep()
{
  WallSide wallToUse; // Declare a variable for which wall to follow
  startDistanceMeasure(); // Starts the distance measuring
  trueDistanceDriven = 0;
  double dumbDistanceDriven = 0;

  // Get sensor data for initial values
  flushDistanceArrays();
  getUltrasonics();
  checkWallPresence();
  setPreviousWallStates();
  if (leftWallPresent && rightWallPresent) wallToUse = wall_both;
  else if (leftWallPresent) wallToUse = wall_left;
  else if (rightWallPresent) wallToUse = wall_right;
  else wallToUse = wall_none; // If no wall was detected

  
  // Calculate the angles for use by pidDrive
  double startAngle = 0;
  // Calculate the current values precisely
  // Maybe I need to add a delay in the loop because of ultrasonic measurements time limits? (it wont make multiple measurements if the polling speed is too fast)
  double angleMeasurements[10];
  double distanceMeasurements[10];
  if (wallToUse == wall_none) {
    for (int i=0; i<10; ++i) { // Fill the measurements with 0
      angleMeasurements[i] = 0;
      distanceMeasurements[i] = 0;
    }
    startAngle = lastWallAngle;
  } else {
    for (int i=0; i<10; ++i) { // Get 10 measurements
      getUltrasonics();
      calcRobotPose(wallToUse, angleMeasurements[i], distanceMeasurements[i], false); // If both walls are present, this only uses the left wall. Could fix in the future.
    }
    startAngle =  measurementAverage(angleMeasurements); // Angle in relation to the wall at the beginning of the move. If no wall is present, the angle is set to 0 (meaning the current angle will be the goal)
  }
  gyro.update();
  double gyroOffset = gyroAngleToMathAngle(gyro.getAngleZ());// The angle measured by the gyro (absolute angle) in the beginning.
  

  lastWallSide = wall_none; // Tells pidDrive that derivative term should not be used
  

  unsigned long timerFlag = millis();
  int iterations = 0;
  
  // Drive until the truDistanceDriven is 30 or greater. This is the original way I did it, but the alternative way below may be used if the later parts of this code are changed.
  while (trueDistanceDriven < 30)
  {
  driveStepDriveLoop(wallToUse, startAngle, gyroOffset, dumbDistanceDriven); // There does not seem to be a time difference between calling the function like this and pasting in the code
  ++iterations;
  }
  Serial.println((millis()-timerFlag)/double(iterations));

  /* Alternative way of doing it. Because it does some of the things from down below, those have to be removed if this code is used:
  // If the driven distance exceeds 30 or the front distance gets too small, the loop will end if the robot is not in the specified interval to the right.
  while ((trueDistanceDriven < 30 && (ultrasonicDistanceF > (15-ultrasonicFrontOffset))) || ((ultrasonicDistanceF < (15-ultrasonicFrontOffset + 5)) && (ultrasonicDistanceF > (15-ultrasonicFrontOffset)))) {
    driveStepDriveLoop(wallToUse, startAngle, gyroOffset, dumbDistanceDriven);
  }
  */

  
  // Checking how far you have driven
  // Get new sensor data
  getUltrasonics();
  checkWallPresence();
  // Reset drive variables
  startDistanceMeasure();
  dumbDistanceDriven = 0;
  trueDistanceDriven = 0;
  // Continue driving forward if necessary (close enough to the wall in front)
  if (ultrasonicDistanceF < (15-ultrasonicFrontOffset + 10))
  {
    lights::setColour(3, colourOrange, true);
    while (ultrasonicDistanceF < (15-ultrasonicFrontOffset + 3) && trueDistanceDriven < 7) // The part about trueDistance is a failsafe in case the sensor fails
    {
      driveStepDriveLoop(wallToUse, startAngle, gyroOffset, dumbDistanceDriven);
    }
    lights::setColour(3, colourBase, true);
  }

  stopWheels();
  if (colourSensor.lastKnownFloorColour == ColourSensor::floor_reflective)
  {
    delay(300);
    colourSensor.checkFloorColour();
    // Handle checkpoints.
  }

  lights::turnOff();

  return true; // Change later to depend on whether the move was executed or not

  // The current angle to the wall is already stored by pidDrive in lastWallAngle

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