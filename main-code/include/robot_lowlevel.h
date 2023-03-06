#ifndef ROBOT_LOWLEVEL_H
#define ROBOT_LOWLEVEL_H

/*#include <Arduino.h>
#include <MeAuriga.h>
#include <MeEncoderOnBoard.h>
#include <MeGyro.h>*/





//////////////// Public functions /////////////////////////////

// Most of (all?) the code needed to initialize the encoders
// Should be run inside of setup()
void encodersInit();

// Drives one step (30cm) forwards.
// Could take a speed argument
void driveStep();

enum TurningDirection {
    cw,
    ccw,
};

// Turns the specified amount of steps.
// steps - the amount of steps (90 degrees) to turn. Positive is counter-clockwise, negative is clockwise.
// The direction may be separated into a separate argument and given its own type.
void turnSteps(TurningDirection direction, int steps);

/////////////////// Private functions /////////////////////////////

//---------------- Low level encoder motor functions ---------------------//


// Encoder interrupt processes:
void isr_encoderLF(void);
void isr_encoderLB(void);
void isr_encoderRF(void);
void isr_encoderRB(void);


// Loop all encoders
void loopEncoders();

//--------------------- Slightly higher level encoder motor functions --------------------------//

// Stop the specified wheel
// wheel - the wheels to stop. An integer for now, but could be a user defined type later. Take an object as argument?
void stopWheels();

enum WheelSide {
    left,
    right,
};

void moveWheelSide(WheelSide wheelSide, double distance, double speed);

void letWheelsTurn();

//---------------------------- Turning functions --------------------------------//


// Used to initialize the gyro.
// Should be run in setup()
void gyroInit();

// Info: the gyro gives values between -180 and 180. Positive is clockwise.
// 0 <= mathangle < 360 ( -180 < gyroangle <= 180)
// Converts angle from -180 to 180 degrees, positive clockwise, to 0 to 360 degrees, positive counterclockwise
double gyroAngleToMathAngle(double angle);
// Does the opposite of above. They are inverse of eachother
double mathAngleToGyroAngle(double angle);

// Returns the distance left to turn in degrees. When you get closer, it decreases. When you have overshot, it will be negative.
// Can we omit zerocross, and just give the turn angle instead?
// zeroCross - if the turn will cross over 0
// turningdirection - which direction you will turn. 1 is clockwise and -1 is counter-clockwise.
double leftToTurn(bool zeroCross, int turningDirection, double tarAng, double curAng);


// Turn using the gyro
// The move angle is the angle to turn, in degrees. Positive is counter-clockwise.
// Improvement: Slow down when you get closer to turn more precisely
// turnAngle - the angle to turn, in degrees. Should not be greater than 90 (only tested with 90)
void gyroTurn(double turnAngle);

// Turns the specified steps (90 degrees) in the direction specified above
// steps - the amount of steps to turn (positive int)
// direction - the direction to turn (cw or ccw)
void gyroTurnSteps(TurningDirection direction, int steps);

//-------------------- Driving functions --------------------------------//

// Drive using just the encoders, "blindly".
void driveBlind(double distance);





#endif