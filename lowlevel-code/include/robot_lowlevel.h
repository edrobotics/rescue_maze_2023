// Header file

#ifndef ROBOT_LOWLEVEL_H
#define ROBOT_LOWLEVEL_H

// #include <Arduino.h>
// #include <MeAuriga.h>
// #include <MeEncoderOnBoard.h>
// #include <MeGyro.h>
#include <colour_sensor.h>

const int ON_RAMP_ARR_SIZE = 5;

enum UltrasonicSensorEnum
{
  ultrasonic_F,
  ultrasonic_LF,
  ultrasonic_LB,
  ultrasonic_RF,
  ultrasonic_RB,
  ULTRASONIC_NUM, // The number of ultrasonic sensors
};

enum US_MeasurementType
{
  usmt_raw,
  usmt_smooth,
  USMT_NUM,
};


enum WallChangeOffsets
{
  wcoff_frontLeaving,
  wcoff_frontApproaching,
  wcoff_backLeaving,
  wcoff_backApproaching,
  wcoff_num,
};

enum TouchSensorSide
{
  touch_none,
  touch_left,
  touch_right,
  touch_both
};


//////////////// Public functions /////////////////////////////

// Drives one step (30cm) forwards.
// Could take a speed argument
// continuing - if you should continue where you left off last time (keeping the truedistancedriven)
bool driveStep(ColourSensor::FloorColour& floorColourAhead, bool& rampDriven, TouchSensorSide& frontSensorDetectionType, double& xDistanceOnRamp, double& yDistanceOnRamp, bool continuing);

bool driveStep();

enum TurningDirection {
    cw,
    ccw,
};

// Turns the specified amount of steps.
// steps - the amount of steps (90 degrees) to turn. Positive is counter-clockwise, negative is clockwise.
// The direction may be separated into a separate argument and given its own type.
void turnSteps(TurningDirection direction, int steps, double turningSpeed);

// Returns an byte giving information about whether the walls are present
int getWallStates();


enum Command
{
  command_driveStep,
  command_driveBack, // Should not be used.
  command_turnLeft,
  command_turnRight,
  command_getWallStates,
  command_interrupt,
  command_resume,
  command_dropKit,
  command_light,
  command_invalid,
  command_none,
};

namespace serialcomm
{
    void returnSuccess();

    void returnFailure();

    void returnFloorColour(ColourSensor::FloorColour floorColour);

    void returnAnswer(int answer);

    // char readChar();
    // Read a command following the outlined standard
    // Returns data of type Command.
    // Command readCommand();

    Command readCommand(bool waitForSerial);
    Command readCommand (bool waitForSerial, int timeout);

    void clearBuffer();

    bool checkInterrupt();

    void answerInterrupt(int stepDriven);

    void cancelInterrupt();

    // Sends data that will be printed to the console / other debugging method
    // Should accept types in the same way that Serial.print() does.
    bool sendDebug();

    void sendLOP();

    void sendColourCal();
}

// Makes a navigation decision
// For simple testing without the maze-code present
void makeNavDecision(Command& action);



void lightsAndBuzzerInit();

struct RGBColour
{
    int red;
    int green;
    int blue;
};

namespace lights
{
    enum LedDirection
    {
        front = 0,
        left = 3,
        right = 1,
        back = 2,
    };

    void execLightCommand();

    // Turns all lights off
    void turnOff();
    void showCustom();

    void setColour(int index, RGBColour colour, bool showColour);
    void setColour(int index, RGBColour colour, double intensity, bool showColour);
    RGBColour safeMltp(RGBColour base, double multiplier);

    // Shows the 3 leds facing in the requested direction
    void showDirection(LedDirection direction);
    void showDirection(lights::LedDirection direction, RGBColour colour);

    void fastBlink(RGBColour colour);

    void affirmativeBlink();

    void errorBlink();

    void noComm();

    void negativeBlink();
    
    void floorIndicator(ColourSensor::FloorColour floorColour);
    
    void disabled();

    void activated();

    void checkPointRestored();

    void turnOnVictimLights(bool show);

    void indicateFrontSensor();

    void reversing();

    void onRamp();

    void indicateCheckpoint();

    void rampDriven();

    int safeIndex(int index);
    
    // Produces a spinning circle animation
    // colour is RGBColour
    // speed - the speed at which the ring travels in rounds per second
    void circleLoop(RGBColour colour1, RGBColour colour2, double speed);
    void circle(RGBColour colour1, RGBColour colour2, double speed, int duration);
    void indicateBlueCircle();

}

namespace sounds
{
    // Silence all sound
    void silence();

    // Beep indicating an error. Should maybe accept an error type as an argument?
    void errorBeep();

    void tone(int freq, int duration);
}

void initColourSensor();


/////////////////// Private functions /////////////////////////////





//---------------- Low level encoder motor functions ---------------------//


// Encoder interrupt processes:
void isr_encoderLF(void);
void isr_encoderLB(void);
void isr_encoderRF(void);
void isr_encoderRB(void);


// Loop all encoders
void loopEncoders();

// Most of (all?) the code needed to initialize the encoders
// Should be run inside of setup()
void encodersInit();

//--------------------- Slightly higher level encoder motor functions --------------------------//

// Stop the specified wheel
// wheel - the wheels to stop. An integer for now, but could be a user defined type later. Take an object as argument?
void stopWheels();

enum WheelSide {
    wheels_left,
    wheels_right,
};


// wheelSide - wheels_left or wheels_right (a group of 2 encoder motors)
// distance - the distance the wheel will move in cm. Positive is forward and negative is backwards
// speed - the speed the wheel will move at in cm/s. Always positive.
void moveWheelSide(WheelSide wheelSide, double distance, double speed);

// Runs the specified wheel side at the specified speed
// wheelSide - wheels_left or wheels_right
// speed - the speed in cm/s to run the side at. Positive is forward and negative is backwards.
void runWheelSide(WheelSide wheelSide, double speed);

// Let the wheels finish their motion.
// stopWhenDone - if true, the wheels will stop when done. If false, they will continue rotating.
void letWheelsTurn(bool stopWhenDone);

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


//// Offsets the angles so that one is 180 degrees.
// refAngle - the angle that will become 180 degrees.
// calcAngle - the angle that will be adjusted in accordance.
// The angles should conform to the mathangle standard outlined above. Both input and output should conform.
// Should only be used when the difference between the angles is < 180 degrees
void centerAngle180(double& refAngle, double& calcAngle);

enum WallSide {
    wall_left,
    wall_right,
    wall_front,
    wall_both,
    wall_none,
};

class RobotPose
{
  public:
    double xDist; // Distance (cm) counted from the left wall (or imaginary left wall). Positive goes right.
    double yDist; // Distance (cm) counted from the centre of one tile to the next. Positive goes forward
    double xDistIncrement;
    double yDistIncrement; // Increment for yDist
    double angle; // Angle (degrees) relative to the wall. Positive is counter-clockwise. Mathangle.
    double lastAngle;
    double startAngle; // Perhaps not needed?
    double targetAngle;
    double gyroAngle; // Angle measured by the gyro. Saved as mathangle.
    double targetGyroAngle;
    double gyroOffset; // Gyro angle when standing parallel to the wall. Saved as mathangle.
    double distOnRamp;
    double xDistOnRamp;
    double yDistOnRamp;
    RobotPose()
    {
      xDist = 15;
      yDist = 0;
      angle = 0;
    }
    void update(); // Update the robot pose, with new ultrasonics and all.
    void update(WallSide wallToUse);
    void update(double distanceIncrement);
    void update(WallSide wallToUse, double distanceIncrement);
    void gyroUpdate(double distanceIncrement);
    void updateGyroOffset();
    void updateOnRamp(WallSide wallToUse, double distanceIncrement);
    void print();
    WallSide getSafeWallToUse();
    WallSide getNormalWallToUse();
    // Functions for determining which wall to follow. In here or outside?
  private:
    void calculate2(WallSide wallSide, double& angle, bool useGyroAngle, double distanceIncrement);
    void calculate1(WallSide wallSide, double& angle, double& distance, bool useGyroAngle);
};

// // Sets the global variables for angles and distances.
// // Does not update the gyro itself
// // secondaryWallDistance is supplied when both walls are present
// void updateRobotPose(WallSide wallSide, double& secondaryWallDistance);
// // Alternate way of calling
// void updateRobotPose(WallSide wallSide);
// // Updates the robot angles (including the lastwallangle)
// void updateRobotPose();

// void printRobotPose();

// // Update the "true" distance to the wall and the angle from upright
// // Other code should call the getUltrasonics() beforehand
// // wallSide - which wallSide to check. wall_left, wall_right or wall_both
// // angle - the variable to return the calculated angle to. Returned in degrees.
// // angle (if useGyroAngle == true) - the angle calculated by the gyro. Will be used in the calculation and not modified. Degrees.
// // trueDistance - the variable to return the calculated distance to. Returned in cm.
// // useGyroAngle - whether to use the angle calculated by the gyro (true) or calculate the angle youself (false)
// void calcRobotPose(WallSide wallSide, double& angle, double& trueDistance, bool useGyroAngle);

// Returns the distance left to turn in degrees. When you get closer, it decreases. When you have overshot, it will be negative.
// Can we omit zerocross, and just give the turn angle instead?
// zeroCross - if the turn will cross over 0
// turningdirection - which direction you will turn. 1 is clockwise and -1 is counter-clockwise.
double leftToTurn(bool zeroCross, int turningDirection, double tarAng, double curAng);


// Turn using the gyro
// turnAngle - the angle to turn, in degrees. Should not be greater than 90 (only tested with 90). Positive is counter-clockwise (math angle)
// stopMoving - Whether or not the robot should stop when the function is done. Set to false when driving continuously.
// baseSpeed - Optional argument for specifying the speed to move at while turning. cm/s
// If baseSpeed != 0, the function will update trueDistanceDriven.
void gyroTurn(double turnAngle, bool stopMoving, double turnSpeed, bool aware = 0, double baseSpeed = 0);

// GyroTurn but it is updating the robot pose variables accordingly.
// The code is copied from gyroTurnSteps(), so I should probably make gyroTurnSteps() make use of awareGyroTurn().
// turnAngle - the angle to turn
// stopMoving - whether to stop when done or not
// baseSpeed - the speed at which to drive (0 for just turning)
void awareGyroTurn(double turnAngle, bool stopMoving, double turnSpeed, bool ultrasonicUpdate, double baseSpeed = 0);

// Turns the specified steps (90 degrees) in the direction specified above.
// Automatic correction for the last angle to the wall can be specified by the last argument. Make sure that the lastWallAngle is up to date!
// direction - cw (clockwise) or ccw (counter-clockwise) turn.
// steps - the amount of 90-degree turns to do in the chosen direction.
// doCorrection - Whether or not you should correct for the lastWallAngle
void gyroTurnSteps(TurningDirection direction, int steps, bool doCorrection, double turningSpeed);

// Aligns the robot to the grid (parallel/normal to the walls)
void straighten();

// Wiggles the robot in a sidesays motion
void sideWiggleCorrection(WallSide direction);

// Automatically determines the direction of the wiggle
void sideWiggleCorrection();

//--------------------- Sensors ----------------------------------------//



enum UltrasonicGroup {
  ultrasonics_front,
  ultrasonics_back,
  ultrasonics_left,
  ultrasonics_right,
};

// For smoothing out the recieved distance measurements

const int DISTANCE_MEASUREMENT_SIZE = 3;

void updateDistanceArray(UltrasonicSensorEnum sensorToUse);

double calcDistanceAverage(double distanceArray[DISTANCE_MEASUREMENT_SIZE]);

void flushDistanceArrays();

// Get all ultrasonic sensors
// Perhaps return an array in the future (or take on as a mutable(?) argument?)
void getUltrasonics();

void ultrasonicUpdateLoop(UltrasonicSensorEnum sensor, double maxDistance, bool waitForInterference);

void ultrasonicIdle();

// Print all sensor data for debugging
void printUltrasonics();

// Check if the walls are present. Uses raw distance data instead of "true" distance data. What does this mean?
void checkWallPresence();

void printWallPresence();

void updateLastKnownDistances();



struct PotWallChange
{
  double shadowDistanceDriven {};
  long timestamp {};
  bool detected {false};

};

enum WallChangeType {
  wallchange_approaching,
  wallchange_leaving,
  wallchange_potApproaching,
  wallchange_potLeaving,
  wallchange_confApproaching,
  wallchange_confLeaving,
  wallchange_none,
};

// Checks for wallchanges in smooth sensor data
void checkSmoothWallChanges();

void checkPotWallChanges();






//-------------------- Driving functions --------------------------------//

// Drive using just the encoders, "blindly".
void driveBlind(double distance, bool stopWhenDone);

void driveSpeed(double speed);

void startDistanceMeasure();

void testDistanceMeasureLeft();

void testDistanceMeasureRight();

// Returns the distance driven by the robot since startDistanceMeasure() was called. Return is in cm.
// Idea: Handle if one encoder is very off?
double getDistanceDriven();

// Increments the "ghost" distances for potential wallchanges.
void incrementShadowDistances(double incrementDistance);


// Drive with wall following. Will do one iteration, so to actually follow the wall, call it multiple times in short succession.
// wallSide - which wall to follow. Can be wall_left, wall_right or wall_both. Directions relative to the robot.
void pidDrive(WallSide wallSide);

enum StoppingReason
{
  stop_none,
  stop_frontWallPresent,
  stop_frontWallPresentFaraway,
  stop_frontWallChangeApproaching,
  stop_frontWallChangeLeaving,
  stop_backWallChangeApproaching,
  stop_backWallChangeLeaving,
  stop_deadReckoning,
  stop_floorColour,
  stop_frontTouchBoth,
  stop_frontTouchLeft,
  stop_frontTouchRight
};


// For ramp driving
void fillRampArrayFalse();

// What to run inside of the driveStep loop (the driving forward-portion)
// wallToUse - which wall to follow/measure
// dumbDistanceDriven - used to keep track of the distance travelled measured by the encoder
bool driveStepDriveLoop(WallSide& wallToUse, double& dumbDistanceDriven, StoppingReason& stopReason, bool& rampDriven);


//------------------------- Rescue kits -----------------------------------//

void handleVictim(double fromInterrupt);

// Deploy a rescue kit in the current position
void deployRescueKit();

void servoSetup();






void testWallChanges();


//--------------------------- Switches and misc. sensors -----------------------------//


// Returns true if the front sensor is activated (pressed in)
TouchSensorSide frontSensorActivated();



#endif