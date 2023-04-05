# Debugging the low-level robot code
This collection of programs will enable wireless debugging of low-level robot code.
An ESP8266 (ESP-01 module) will listen for serial data coming from the auriga. This data will then be relayed via websockets to a python program, which reads the data and formats it according to the standard defined in this document. It writes these forwarded messages to a log file, to enable easy debugging.

# Communication between auriga and ESP8266
## The data to send
- Everything being sent to the raspberry pi
- Loop times
- Sensors and pose:
    - Ultrasonic sensor distances
        - Raw
        - Smoothed
    - Robot pose
    - Floor colour
    - Wall presence variables
        - Raw individual
        - Smoothed individual
        - Actual (combined)
    - Distances driven:
        - Dumb distance driven
        - True distance driven
- Detected wallchanges
    - Raw
    - Smoothed
- Stoppingreason
- Blue tile delay
- Recognised checkpoint
- Rescue kit deployment

## How to format it
All commands will begin with \# and end with \\n.

- Pi commands: #pi,\<data>
- Sensors and pose: #s
    - Ultrasonic distances: #s,u\<type>,\<dataF>,\<dataLF>,...,\<dataRB> (F,LF,LB,RF,RB)
        - Raw: type = r
        - Smoothed: type = s
    - Robot pose - #s,p,\<angle>,\<walldistance>
    - Floor colour - "s,f,\<colour>
        - v - white
        - s - black
        - b - blue
        - r - reflective
        - u - unknown
        - n - not updated (maybe not needed)
    - Wall presence: #s,w\<type>,\<data>
        - Raw: type = r. Same data order as raw ultrasonic sensors
        - Smoothed: type = s. Same data order as Raw.
        - Actual (both sensors): type = a. Data order F,L,R
- Wallchanges: #w\<type>,\<data> - data is delivered in the same order as ultrasonic distances
    - Raw - type = r
    - Smoothed - type = s