#include <colour_sensor.h>
#include <Arduino.h>
#include <Adafruit_TCS34725.h>
#include <Wire.h> // Needed for some reason?
#include <SPI.h> // Needed for some reason?



Adafruit_TCS34725 colSens = Adafruit_TCS34725(TCS34725_INTEGRATIONTIME_240MS, TCS34725_GAIN_1X);



void ColourSensor::init()
{
    colSens.begin();
}

void ColourSensor::printValues()
{
    Serial.print("R: "); Serial.print(sensorRed, DEC); Serial.print(" ");
    Serial.print("G: "); Serial.print(sensorGreen, DEC); Serial.print(" ");
    Serial.print("B: "); Serial.print(sensorBlue, DEC); Serial.print(" ");
    Serial.print("C: "); Serial.print(sensorClear, DEC); Serial.print(" ");
    Serial.println(" ");
}

void ColourSensor::printRatios()
{
    Serial.print("RG: "); Serial.print(rgRatio, 4); Serial.print(" ");
    Serial.print("RB: "); Serial.print(rbRatio, 4); Serial.print(" ");
    Serial.print("GB: "); Serial.print(gbRatio, 4); Serial.print(" ");
    // Serial.println(" ");
}

void ColourSensor::printColourName(ColourSensor::FloorColour colourToPrint)
{
    switch (colourToPrint)
    {
        case floor_black:
            Serial.print("black");
            break;
        
        case floor_reflective:
            Serial.print("reflective");
            break;
        
        case floor_white:
            Serial.print("white");
            break;
        
        case floor_blue:
            Serial.print("blue");
            break;
        
        default:
            Serial.print("unknown");
    }
}


// int ColourSensor::calcColourDistance(int sensorRed, int sensorGreen, int sensorBlue, int referenceRed, int referenceGreen, int referenceBlue)
// {
//     return sqrt(pow(sensorRed-referenceRed, 2) + pow(sensorGreen-referenceGreen, 2) + pow(sensorBlue-referenceBlue, 2));
// }

// Gets the raw data without delay
void ColourSensor::getRawData(uint16_t *sensorRed, uint16_t *sensorGreen, uint16_t *sensorBlue, uint16_t *sensorClear)
{
    // Copied from the libraries code (function getRawData())

    *sensorClear = colSens.read16(TCS34725_CDATAL);
    *sensorRed = colSens.read16(TCS34725_RDATAL);
    *sensorGreen = colSens.read16(TCS34725_GDATAL);
    *sensorBlue = colSens.read16(TCS34725_BDATAL);
}


bool ColourSensor::lowerUpperEval(double val, double lower, double upper)
{
    if (val > lower && val < upper) return true;
    else return false;
}

void ColourSensor::calcColourRatios(double& rg, double& rb, double& gb)
{
    rg = double(sensorRed)/sensorGreen;
    rb = double(sensorRed)/sensorBlue;
    gb = double(sensorGreen)/sensorBlue;
}


// Reads the sensor data into global variables if new sensor data is present.
// Could return true if a value was read and false if it was not updated
bool ColourSensor::readSensor()
{
    if (millis()-lastReadTime > INTEGRATION_TIME) // Check if enough time has passed
    {
        getRawData(&sensorRed, &sensorGreen, &sensorBlue, &sensorClear);
        lastReadTime = millis(); // Update the timeflag
        return true;
    }
    else return false;
}

// Identify the colour read by readSensor()
ColourSensor::FloorColour ColourSensor::identifyColour()
{

    // Identification using ratios

    if (readSensor() == false)
    {
        return floor_notUpdated;
    }

    
    calcColourRatios(rgRatio, rbRatio, gbRatio);

    
    // For debugging/tuning¨
    // printValues();
    // printRatios();

    if (lowerUpperEval(rbRatio, 0, 0.95) && lowerUpperEval(gbRatio, 0, 0.95)) return floor_blue; // Could be lower (0.85?) for both

    else if (lowerUpperEval(rgRatio, 1.19, 1.5) && lowerUpperEval(rbRatio, 1.35, 1.8) && lowerUpperEval(gbRatio, 1.05, 1.3)) return floor_reflective;

    else if (lowerUpperEval(rgRatio, 1.05, 1.2) && lowerUpperEval(rbRatio, 1.25, 1.5) && lowerUpperEval(gbRatio, 1.05, 1.3)) return floor_white; // reflective falls (partly) into the same span, but because reflective would have returned all that is left in this area should be white
    
    else if (lowerUpperEval(rgRatio, 1.7, 6) && lowerUpperEval(rbRatio, 2.3, 6) && lowerUpperEval(gbRatio, 1.2, 1.5)) return floor_black;

    else return floor_unknown;





    
    
    // Identification using colour distance:

    // int colourDistance = 0;

    // for (int i=0; i< ColourSensor::SAMPLES_SIZE; ++i)
    // {
    //     colourDistance = calcColourDistance(sensorRed, sensorGreen, sensorBlue, SAMPLES[i][0], SAMPLES[i][1], SAMPLES[i][2]);

    //     if (colourDistance < 50)
    //     {
    //         // Maybe do differently? With this method, only the first recognised colour will be reported and it will not check if multiple colours match
    //         if (i==0) return black;
    //         else if (i==1) return white;
    //     }
    // }
    // return unknown; // If no sample matched
}


ColourSensor::FloorColour ColourSensor::checkFloorColour()
{
    readSensor();
    FloorColour identifiedColour = identifyColour();
    if (identifiedColour != floor_notUpdated || floor_unknown) lastKnownFloorColour = identifiedColour; // Maybe the floor_unknown should be handled separately?
    return identifiedColour;
    
}