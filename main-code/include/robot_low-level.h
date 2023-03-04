#ifndef ROBOT_LOW-LEVEL_H
#define ROBOT_LOW-LEVEL_H

#include <Arduino.h>
#include <MeAuriga.h>
#include <MeEncoderOnBoard.h>


//////////////// Public functions /////////////////////////////

// Drives one step (30cm) forwards.
// Could take a speed argument
void driveStep();

// Turns the specified amount of steps.
// steps - the amount of steps (90 degrees) to turn. Positive is counter-clockwise, negative is clockwise.
// The direction may be separated into a separate argument and given its own type.
void turnSteps(int steps);

/////////////////// Private functions /////////////////////////////

// All (most of) the code needed to initialize the encoders
// Should be run inside of setup()
void encoders_init();


// Stop the specified wheel
// wheel - the wheels to stop. An integer for now, but could be a user defined type later. Take an object as argument?
void stopWheel(int wheel);

// Stop the specified wheelgroup.
// wheelGroup - the group of wheels to stop. Should be predefined. User defined type? Collection of encoder motor objects?
// By providing the wheelgroup containing all wheels, all wheels are stopped.
void stopWheelGroup(int wheelGroup);





void gyroTurn(double turnAngle);




#endif