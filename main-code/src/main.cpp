//#include <robot_lowlevel.h>
#include <Arduino.h>
#include <MeAuriga.h>
#include <MeEncoderOnBoard.h>
#include <MeGyro.h>
#include <Wire.h>
#include <SPI.h>

// Encoder motor definitions
MeEncoderOnBoard encoderLF(SLOT2);
MeEncoderOnBoard encoderLB(SLOT3);
MeEncoderOnBoard encoderRF(SLOT1);
MeEncoderOnBoard encoderRB(SLOT4);

// Gyro definition
MeGyro gyro(0, 0x69);

enum TurningDirection {
    cw,
    ccw,
};
enum WheelSide {
    left,
    right,
};


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
// wheelSide - left or right (a group of 2 encoder motors)
// distance - the distance the wheel will move in cm. Positive is forward and negative is backwards
// speed - the speed the wheel will move at in cm/s. Always positive.
void moveWheelSide(WheelSide wheelSide, double distance, double speed)
{
  speed *= CMPS_TO_RPM;
  distance*=360/WHEEL_CIRCUMFERENCE; // Converts the distance from cm to degrees
  if (wheelSide==left) {
    encoderLF.speedMove(distance, speed);
    encoderLB.speedMove(distance, speed);
  } else if (wheelSide==right) {
    encoderRF.speedMove(-distance, speed);
    encoderRB.speedMove(-distance, speed);
  }
}

void letWheelsTurn()
{
  bool LFdone = false;
  bool LBdone = false;
  bool RFdone = false;
  bool RBdone = false;
  while(!LFdone || !LBdone || !RFdone || !RBdone) {
    if (abs(encoderLF.distanceToGo())<5) {
      encoderLF.runSpeed(0);
      LFdone = true;
    }
    if (abs(encoderLB.distanceToGo())<5) {
      encoderLB.runSpeed(0);
      LBdone = true;
    }
    if (abs(encoderRF.distanceToGo())<5) {
      encoderRF.runSpeed(0);
      RFdone = true;
    }
    if (abs(encoderRB.distanceToGo())<5) {
      encoderRB.runSpeed(0);
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
// turningdirection - which direction you will turn. 1 is clockwise and -1 is counter-clockwise.
double leftToTurn(bool zeroCross, int turningDirection, double tarAng, double curAng)
{

  if (zeroCross==false) { // Normal turn
    if (turningDirection == -1) {
      if (tarAng>300 && curAng<180) return 360 - tarAng + curAng; // If you have overshot so much that you passed zero
      else return -turningDirection*(tarAng-curAng); // Else turn like normal
    } else if (turningDirection == 1) {
      if (tarAng<60 && curAng >180) return 360 - curAng + tarAng; // if you have overshot so much that you passed zero
      else return -turningDirection*(tarAng-curAng); // Else turn like normal
    }
  } else if (zeroCross==true && turningDirection==-1) { // Turning that passes 0 and is counter-clockwise (positive direction)
    if (curAng>180) return 360 - curAng + tarAng; // Handles it until curAng is 0
    else return -turningDirection*(tarAng-curAng); // When curAng has passed 0, it is just lika a normal turn (like above)
  } else if (zeroCross==true && turningDirection==1) { // Turning that passes 0 and is clockwise (negative direction)
    if (curAng<180) return curAng + 360-tarAng; // Handles it until curAng is <360 (passes 0)
    else return -turningDirection*(tarAng-curAng); // When curAng has passed 0, it is just lika a normal turn (like above)
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
// The move angle is the angle to turn, in degrees. Positive is counter-clockwise.
// Improvement: Slow down when you get closer to turn more precisely
void gyroTurn(double turnAngle)
{
  // Used to determine turning direction of the wheels
    int multiplier = -1;
    bool crossingZero = false;
    if (turnAngle<0) multiplier=1;
    
    gyro.update();
    currentAngle = gyroAngleToMathAngle(gyro.getAngleZ()); // Sets positive to be counter-clockwise and makes all valid values between 0 and 360
    targetAngle = (currentAngle + turnAngle);
    if (targetAngle<0) {
    targetAngle = 360+targetAngle; // If the target angle is negative, subtract it from 360. As the move_angle cannot be greater than 360 (should always be 90), this will also bind the value between 0 and 360.
    crossingZero = true;
  } else if (targetAngle >= 360) {
    targetAngle = targetAngle - 360; // Should bind the value to be between 0 and 360 for positive target angles ( no angle should be 720 degrees or greater, so this should work)
    crossingZero = true;
  }
    //double speedToRun = multiplier*1.5*BASE_SPEED_CMPS*CMPS_TO_RPM;
    double speedToRun = multiplier*69;
    encoderLF.runSpeed(speedToRun);
    encoderLB.runSpeed(speedToRun);
    encoderRF.runSpeed(speedToRun);
    encoderRB.runSpeed(speedToRun);
    while (leftToTurn(crossingZero, multiplier, targetAngle, currentAngle) > 5) {
      gyro.update();
      currentAngle = gyroAngleToMathAngle(gyro.getAngleZ());
      loopEncoders();
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

//----------------------Driving----------------------------------------//

// Drive just using the encoders
// distance - the distance to drive
// The speed is adjusted globally using the BASE_SPEED_CMPS variable.
void driveBlind(double distance)
{
  moveWheelSide(left, distance, BASE_SPEED_CMPS);
  moveWheelSide(right, distance, BASE_SPEED_CMPS);
  letWheelsTurn();
}



void driveStep()
{

}


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
  /*
  //Drive in a square
  driveBlind(30);
  stopWheels();
  delay(500);
  turnSteps(cw, 1);
  delay(500);
  */
}