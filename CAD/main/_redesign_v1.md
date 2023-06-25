# Redesign v1
This is the first redesign after the swedish competition

# Problems with old design
- Robot jumping/skipping when turning
- Turning was not really around center point.
- High CG - the robot is constantly close to tipping over when driving on ramps
- Difficult to work on the robot
    - Wires inaccessible -> cannot secure them, among other things
    - Time-consuming to disassemble
    - Annoying to change battery
- Lack of space for rescue kit mechanism
- Rescue kit mechanism prone to breaking
- Battery cable touching wheel while driving
- Electrical connections not always shielded
- Not enough space for raspberry pi camera ribbon cable

Unsure/speculation/potential problems
- Front sensor may be positioned too low and may be too narrow and too unstable
- Too small FOV for the camera

# General improvements
Certain:
- Lower CG
    - Power the raspberry off of the main battery
    - Put the pi lower down
    - Try to sink everything down a bit into the baseplate.
    - Place dummy weights down low?
    - Longer wheelbase - would make turning worse
    - Alternative solution to lower CG: Add some kind of support wheels which engage only when the robot is tipping too much. A challenge to keep them away from the walls during normal operation.
        - Wild idea: Use servos or similar to control the arms
    - Alternative solution to lower CG: Drive slower up the ramp (if even possible?)
    - Move voltage converters lower down.
- Make the robot easier to work on
    - Improve ease of (dis)assembly - not everything should be connected by the same screws. Not too many components per screw.
    - Make the top come off -> can access everything easier
        - Slice the pillars somewhere above the ultrasonics
        - Mount the mirror to the top, with arms comming down
- Mount the encoder motor drivers better. Remove pins and make housing.
- Add possibility to stabilize the rescue kit tower by connecting it to the pillars
- Add lighting to the camera

Maybe:




# New notes

Hold the mirror with two arms going down on each side, fastening where the support legs meet the rest of the body.
