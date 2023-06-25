# Communication protocol - sockets
Communication is always initialized by the client sending a request.
After the client has sent a request, serialServer will either fetch the requested data or execute the given command.

In general, every request should be answered with the requested data (if there is any) and then some message signalling that the command was executed.

Maybe every request should be terminated by some special character (like \n)?

## Requests

### Drive step
Drives one step in the maze.
- Arduino function - driveStep()
- Arguments - none
- Return - whether or not a full step was driven. Returned as '1' or '0' (as strings/chars), as well as the ground colour of the tile that would have been driven on. Also the reason for not driving a full step?
  - Reasons can be odometry error, ground colour, error detecting the wall with the ultrasonic etc.
  - TODO: What should the maze program do if there has been an error in the odometry? Does it need any other data from the auriga?
- Request - "!driveStep"

### Turn steps
Turns the specified amount of steps (not yet implemented) in the specified direction.
- Arduino function - turnSteps()
- Arguments - the direction to turn (cw or ccw)
- Return - none
- Request - "!turnSteps, *direction*"
    - *direction* - cw (clockwise) or ccw (counterclockwise)

### Get wall states
Returns the current state of the walls around the robot
- Arduino function - getWallStates()
- Arguments - none
- Return - String containing comma-separated 0 and 1 representing if the walls are present (1 is present) in the format: "*front*, *left*, *right*", where front, left and right are the values for the corresponding walls
- Request - "!getWallStates"

### Rescue kits (to be done)

### Get victim state
Returns whether or not a victim has been detected (and how long ago the response was?)
- Arguments - none (maybe maximum time since last discovery?)
- Return - The victim type and whether it is on the right or left side of the robot.
  - "*victimType*, *victimSide*"
- Request - "!getVictimState"

Note: The code will probably check if it has a victimState stored and how long ago it was stored. Then there will be another process/thread (?) which responds to the vision program and updates these variables which can then be used in other ways.

One problem with storing the victimstate is that if the robot turns, the directions will no longer be correct. Maybe this program should also keep track of absolute direction (given by the maze-code in every command altering direction?). Maybe it will not be a problem if we never use victim data that is too old.

General about vision - The program on the auriga will need to be able to stop in the middle of a driveStep to detect a victim, and the maze-code (and this server) will need to be able to accomodate that as well.

### Update victim state
Gives data to serialServer about victim type and position. Sent from the vision-code.
- Arguments - "*victimType*, *victimSide*"
- Return - None
- Request - "!updateVictimState, *victimType*, *victimSide*"

# Communication protocol - serial
This section handles the communication between serialServer and the auriga.

## General communication format
Data will either be sent as chars and the termination character will be '\n'.

Commands: !\<command>,\<data>'\n'

Return: !a,\<data>

Return !s (success) or !f (failed) when done with an action

!b may be used for sending debugging information from the auriga to the raspberry (?).

## Commands
- !d - driveStep
  - Return:
- !t,\<direction> - turn
  - direction - which way to turn. 'r' (right), 'l' (left)
- !k,\<amount> - drop rescue kit
  - amount - the amount of rescue kits to deploy (maybe not used?)
- !w - getWallStates
  - !a,\<walldata> - walldata is a byte where the 3 last bits represent the wall states. 0b\<front>\<left>\<right>. 1 is present, 0 is not present.
- !i - interrupt the current action
- !r - resume the current action after an interrupt