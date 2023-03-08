#include <robot_lowlevel.h>
#include <Arduino.h>
#include <MeAuriga.h>
#include <MeEncoderOnBoard.h>
#include <MeGyro.h>
#include <Wire.h>
#include <SPI.h>

// Encoder motor definitions
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


// Gyro definition
MeGyro gyro(0, 0x69);

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
const double WHEEL_DIAMETER = 6.4; 
const double WHEEL_CIRCUMFERENCE = PI*WHEEL_DIAMETER;
const double WHEEL_DISTANCE = 13.2+0.4; // Diameter of the wheelbase (distance between the wheels)
const double WHEELBASE_CIRCUMFERENCE = PI*WHEEL_DISTANCE;

// Driving
const double CMPS_TO_RPM = 1.0/WHEEL_CIRCUMFERENCE*60.0; // Constant to convert from cm/s to rpm
const double BASE_SPEED_CMPS = 20; // The base speed of driving (cm/s)
const double BASE_SPEED_RPM = CMPS_TO_RPM*BASE_SPEED_CMPS; // The base speed of driving (rpm)


// Sensor constants
const double ultrasonicSpacing = 14; // The distance between the centers two ultrasonic sensors.
const double ultrasonicDistanceToWall = 6; // The distance between the ultrasonic sensor (edge of the robot) and the wall when the robot is centered. Not calibrated !!!!! just a guess!!!
const double wallPresenceTreshold = 20; // Not calibrated !!!!!!!!!!!!!!!!!!!!!!!! Just a guess!!!!!!!!!!!!!!!!!!!!!!!

// Sensor data
double ultrasonicDistanceLF = 0;
double ultrasonicDistanceLB = 0;
double ultrasonicDistanceRF = 0;
double ultrasonicDistanceRB = 0;
double ultrasonicDistanceF = 0;


// Wall presence
bool leftWallPresent = false;
bool rightWallPresent = false;
bool frontWallPresent = false;

// For gyro turning
double currentAngle = 0;
double targetAngle = 0;


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

// Returns the distance left to turn in degrees. When you get closer, it decreases. When you have overshot, it will be negative.
// Can we omit zerocross, and just give the turn angle instead?
// zeroCross - if the turn will cross over 0
// turningdirection - which direction you will turn. 1 is counter-clockwise and -1 is clockwise (math angles)
double leftToTurn(bool zeroCross, int turningDirection, double tarAng, double curAng)
{

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

  
  /* Fungerar ej korrekt (varför?). Kan dock vara inspiration.
  if (curAng>=270 && turningDirection==-1) return 360 - curAng + tarAng;
  else if (curAng<90 && turningDirection==1) return curAng + 360-tarAng;
  return -turningDirection*(tarAng-curAng);
  */

 // Alternative method: Calculate the difference between any two angles (maybe constrain to some range).
}

// Turn using the gyro
// The move angle is the angle to turn, in degrees. Positive is counter-clockwise (math angle)
void gyroTurn(double turnAngle)
{
  // Used to determine turning direction of the wheels
    int multiplier = 1; // Positive is ccw
    bool crossingZero = false;
    if (turnAngle<0) multiplier=-1; // Negative is cw
    
    gyro.update();
    currentAngle = gyroAngleToMathAngle(gyro.getAngleZ()); // Sets positive to be counter-clockwise and makes all valid values between 0 and 360
    targetAngle = (currentAngle + turnAngle);
    if (targetAngle<0) {
    targetAngle = 360+targetAngle; // If the target angle is negative, subtract it from 360. As the turnAngle cannot be greater than 360 (should always be 90), this will also bind the value between 0 and 360.
    crossingZero = true;
  } else if (targetAngle >= 360) {
    targetAngle = targetAngle - 360; // Should bind the value to be between 0 and 360 for positive target angles ( no angle should be 720 degrees or greater, so this should work)
    crossingZero = true;
  }
    //double speedToRun = multiplier*1.5*BASE_SPEED_CMPS*CMPS_TO_RPM;
    double speedToRun = multiplier*69/CMPS_TO_RPM;
    runWheelSide(wheels_left, -speedToRun);
    runWheelSide(wheels_right, speedToRun);

    double varLeftToTurn = leftToTurn(crossingZero, multiplier, targetAngle, currentAngle);

    while (varLeftToTurn > 15) {
      gyro.update();
      currentAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
      varLeftToTurn = leftToTurn(crossingZero, multiplier, targetAngle, currentAngle);
    }

    // Slowing down in the end of the turn.
    // All of this code could be placed inside of the first while-loop, but then every iteration would take more time because of checks and the gyro would become less accurate due to that.
    speedToRun = multiplier*30/CMPS_TO_RPM;
    runWheelSide(wheels_left, -speedToRun);
    runWheelSide(wheels_right, speedToRun);

    while (varLeftToTurn > 2) {
      gyro.update();
      currentAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
      varLeftToTurn = leftToTurn(crossingZero, multiplier, targetAngle, currentAngle);
    }
    
    stopWheels();
}

// Turns the specified steps (90 degrees) in the direction specified above.
// direction - cw (clockwise) or ccw (counter-clockwise) turn.
// steps - the amount of 90-degree turns to do in the chosen direction.
void gyroTurnSteps(TurningDirection direction, int steps)
{
  int multiplier=-1;
  if (direction==ccw) multiplier=1;

  gyroTurn(multiplier*90);

}

void turnSteps(TurningDirection direction, int steps)
{
  gyroTurnSteps(direction, steps);
}



//--------------------- Sensors --------------------------------------//

// Get all ultrasonic sensors
// Perhaps return an array in the future (or take on as a mutable(?) argument?)
void getUltrasonics()
{
  //TODO: Add limit to how quickly you can call it

  ultrasonicDistanceF = ultrasonicF.distanceCm();
  ultrasonicDistanceLF = ultrasonicLF.distanceCm();
  ultrasonicDistanceLB = ultrasonicLB.distanceCm();
  ultrasonicDistanceRF = ultrasonicRF.distanceCm();
  ultrasonicDistanceRB = ultrasonicRB.distanceCm();
}

void printUltrasonics()
{
  Serial.print(ultrasonicDistanceF); Serial.print(", ");
  Serial.print(ultrasonicDistanceLF); Serial.print(", ");
  Serial.print(ultrasonicDistanceLB); Serial.print(", ");
  Serial.print(ultrasonicDistanceRF); Serial.print(", ");
  Serial.println(ultrasonicDistanceRB);
}

// Check if the walls are present. Uses raw distance data instead of "true" distance data.
void checkWallPresence()
{
  leftWallPresent = false;
  rightWallPresent = false;
  frontWallPresent = false;
  if (ultrasonicDistanceLF < wallPresenceTreshold && ultrasonicDistanceLB < wallPresenceTreshold) leftWallPresent = true;
  if (ultrasonicDistanceRF < wallPresenceTreshold && ultrasonicDistanceRB < wallPresenceTreshold) rightWallPresent = true;
  if (ultrasonicDistanceF < wallPresenceTreshold) frontWallPresent = true;
}


// Update the "true" distance to the wall and the angle from upright
// wallSide - which wallSide to check. wall_left, wall_right or wall_both
// Other code should call the getUltrasonics() beforehand
// How do I get the return values?
void calcRobotPose(WallSide wallSide, double& angle, double& trueDistance)
{
  double d1 = 0;
  double d2 = 0;
  if (wallSide==wall_left)
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
  else if (wallSide == wall_both)
  {
    // Do nothing for now? Make it like if the left wall was followed?
  }

  angle = atan((d2 - d1)/ultrasonicSpacing);
  trueDistance = cos(angle) * ((d1 + d2)/(double)2);
  angle *= RAD_TO_DEG; // Convert the angle to degrees
  
  // Debugging
  //Serial.print(angle); Serial.print("    "); Serial.println(trueDistance);
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

// PID coefficients for wall following.
double angleP = 1;
double distanceP = 1;

// Drive with wall following. Will do one iteration, so to actually follow the wall, call it multiple times in short succession.
// wallSide - which wall to follow. Can be wall_left, wall_right or wall_both. Directions relative to the robot.
void pidDrive(WallSide wallSide)
{
  double wallDistance = 0;
  double robotAngle = 0;
  getUltrasonics();
  calcRobotPose(wallSide, robotAngle, wallDistance); // Can be used regardless of which side is being used
  double distanceError = 0; // positive means that we are to the right of where we want to be.
  
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
    // Case not handled yet
  }

  double targetAngle = distanceError*distanceP; // Calculate the angle you want depending on the distance to the wall (error)
  double angleError = targetAngle-robotAngle; // Calculate the correction needed in the wheels to get to the angle
  double correction = angleP*angleError; // Calculate the correction in the wheels. Positive is counter-clockwise (math)


  runWheelSide(wheels_left, BASE_SPEED_CMPS - correction);
  runWheelSide(wheels_right, BASE_SPEED_CMPS + correction);
  loopEncoders();  

}



void driveStep()
{
  WallSide wallToFollow; // Declare a variable for which wall to follow
  startDistanceMeasure(); // Starts the distance measuring
  
  while (getDistanceDriven() < 30) { // Drive while you have travelled less than 30cm
    getUltrasonics();
    checkWallPresence();
    if (leftWallPresent && rightWallPresent) wallToFollow = wall_both;
    else if (leftWallPresent) wallToFollow = wall_left;
    else if (rightWallPresent) wallToFollow = wall_right;
    else wallToFollow = wall_none; // If no wall was detected

    if (wallToFollow==wall_none) {
      // Drive at base speed
      driveSpeed(BASE_SPEED_CMPS);
      loopEncoders();
    } else {
      pidDrive(wallToFollow);
    }
  }

  stopWheels();
  

}