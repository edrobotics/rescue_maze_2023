#include <colour_sensor.h>
#include <Arduino.h>
#include <Adafruit_TCS34725.h>
#include <Wire.h> // Needed for some reason?
#include <SPI.h> // Needed for some reason?
#include <EEPROM.h> // Potential include issues with the makeblock library.



Adafruit_TCS34725 colSens = Adafruit_TCS34725(TCS34725_INTEGRATIONTIME_60MS, TCS34725_GAIN_1X);
bool readState = false; // Keeps track of whether the sensor was read or not



void ColourSensor::init()
{
    if (colSens.begin() == false)
    {
        // Serial.println("Could not find sensor");
    }
    // else Serial.println("Sensor initialized");
    lastReadTime = millis();

    // Init buttons for calibration
    blackButton.init();
    blueButton.init();
    reflectiveButton.init();
    whiteButton.init();

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

void ColourSensor::printClearVal()
{
    Serial.print("C: "); Serial.print(sensorClear, DEC); Serial.print(" ");
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

        case floor_notUpdated:
            Serial.print("Not updated");
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
// Returns true if a value was read and false if it was not (too close to last time)
bool ColourSensor::readSensor()
{
    if ((millis()-lastReadTime) > INTEGRATION_TIME) // Check if enough time has passed
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

    if (readState == false)
    {
        return floor_notUpdated;
    }

    
    calcColourRatios(rgRatio, rbRatio, gbRatio);

    
    // For debugging/tuning¨
    // printValues();
    // printRatios();

    int boolSumBlack = lowerUpperEval(rgRatio, blackThresholds.rgLower, blackThresholds.rgUpper) + lowerUpperEval(rbRatio, blackThresholds.rbLower, blackThresholds.rbUpper) + lowerUpperEval(gbRatio, blackThresholds.gbLower, blackThresholds.gbUpper) + (sensorClear < blackThresholds.clearUpper);
    int boolSumBlue = lowerUpperEval(rgRatio, blueThresholds.rgLower, blueThresholds.rgUpper) + lowerUpperEval(rbRatio, blueThresholds.rbLower, blueThresholds.rbUpper) + lowerUpperEval(gbRatio, blueThresholds.gbLower, blueThresholds.gbUpper);
    int boolSumReflective = lowerUpperEval(rgRatio, reflectiveThresholds.rgLower, reflectiveThresholds.rgUpper) + lowerUpperEval(rbRatio, reflectiveThresholds.rbLower, reflectiveThresholds.rbUpper) + lowerUpperEval(gbRatio, reflectiveThresholds.gbLower, reflectiveThresholds.gbUpper);
    int boolSumWhite = lowerUpperEval(rgRatio, whiteThresholds.rgLower, whiteThresholds.rgUpper) + lowerUpperEval(rbRatio, whiteThresholds.rbLower, whiteThresholds.rbUpper) + lowerUpperEval(gbRatio, whiteThresholds.gbLower, whiteThresholds.gbUpper) + (sensorClear > whiteThresholds.clearLower);

    if (boolSumBlack >= 3) return floor_black;
    else if (boolSumBlue >=3) return floor_blue; // Could be lower (0.85?) for both
    else if (boolSumReflective >= 3 && (sensorClear > reflectiveThresholds.clearLower && sensorClear < reflectiveThresholds.clearUpper)) return floor_reflective;
    else if (boolSumWhite >= 3) return floor_white; // reflective falls (partly) into the same span, but because reflective would have returned all that is left in this area should be white
    else return floor_unknown;
    
    // return floor_unknown; // For debugging, disabling colour detection
}

ColourSensor::FloorColour ColourSensor::checkRawFloorColour()
{
    readState = readSensor();
    return identifyColour();
}

// Returns the last floor colour (eg. the one from the latest update).
ColourSensor::FloorColour ColourSensor::checkFloorColour()
{
    FloorColour identifiedColour = checkRawFloorColour();
    if (identifiedColour != floor_notUpdated)
    {
        if (identifiedColour != floor_unknown) lastKnownFloorColour = identifiedColour; // Maybe the floor_unknown should be handled separately?
        lastFloorColour = identifiedColour;
        return identifiedColour;
    }
    else return lastFloorColour;
    
}

char ColourSensor::floorColourAsChar(ColourSensor::FloorColour floorColour)
{
  switch (floorColour)
  {
    case ColourSensor::floor_black:
      return 's';
      break;
    case ColourSensor::floor_blue:
      return 'b';
      break;
    case ColourSensor::floor_reflective:
      return 'c';
      break;
    case ColourSensor::floor_white:
      return 'v';
      break;
    // default:
    //   return 'u'; // If some error occured
    //   break;
  }
}

// Reads the sensor and returns a colour sample. Will wait for the sensor to give values
ColourSample ColourSensor::getColourSample()
{
    ColourSample returnData {};
    while (readSensor() == false) {} // Read the sensor until a new measurement is recieved
    calcColourRatios(rgRatio, rbRatio, gbRatio);

    returnData.r = sensorRed;
    returnData.g = sensorGreen;
    returnData.b = sensorBlue;
    returnData.clear = sensorClear;
    returnData.rg = rgRatio;
    returnData.rb = rbRatio;
    returnData.gb = gbRatio;
    
    return returnData;
}

void ColourSensor::refreshThresholds()
{
    blackThresholds = blackSamples.read(0);
    blueThresholds = blueSamples.read(sizeof(ColourStorageData));
    reflectiveThresholds = reflectiveSamples.read(sizeof(ColourStorageData)*2);
    whiteThresholds = whiteSamples.read(sizeof(ColourStorageData)*3);
}


void ColourSensor::clearCalibrationData()
{
    blackSamples.resetIndex();
    blueSamples.resetIndex();
    reflectiveSamples.resetIndex();
    whiteSamples.resetIndex();
}

void ColourSensor::calibrationRoutineLoop()
{
    // Check for and collect samples
    if (blackButton.isPressed())
    {
        blackSamples.enterSample(getColourSample());
        blackSamples.calculate();
        blackSamples.write(0);
    }
    else if (blueButton.isPressed())
    {
        blueSamples.enterSample(getColourSample());
        blueSamples.calculate();
        blueSamples.write(sizeof(ColourStorageData));
    }
    else if (reflectiveButton.isPressed())
    {
        reflectiveSamples.enterSample(getColourSample());
        reflectiveSamples.calculate();
        reflectiveSamples.write(sizeof(ColourStorageData)*2);
    }
    else if (whiteButton.isPressed())
    {
        whiteSamples.enterSample(getColourSample());
        whiteSamples.calculate();
        whiteSamples.write(sizeof(ColourStorageData)*3);
    }

    // Set new thresholds in the identifying code (maybe only if previous step done?)
    refreshThresholds();

    // Identify the colour on the ground
    FloorColour floorColour = checkRawFloorColour();

    // Show info about colour on ground (which one, certainty etc.)
    switch (floorColour)
    {
        case floor_black:
            break;

        case floor_blue:
            break;

        case floor_reflective:
            break;

        case floor_white:
            break;

        default:
            break;
    }
    
}









// Write threshold data to the specified address
void ColourSampleCollection::write(int address)
{
    // I need to be able to set the startic address of writing and also know the total size of what I store in the EEPROM
    ColourStorageData writeData = thresholds; // The data being written
    EEPROM.put(address, writeData);
}

// Read stored threshold data at from specified address
ColourStorageData ColourSampleCollection::read(int address)
{
    ColourStorageData readData; // The data beign read
    EEPROM.get(address, readData);
    return readData;
}

bool ColourSampleCollection::enterSample(ColourSample sample)
{
    if ( 0 <= sampleIndex && sampleIndex < MAX_COLOUR_SAMPLES)
    {
        samples[sampleIndex] = sample;
        ++sampleIndex;
        return true;
    } else return false;
}

// Calculates the thresholds based on the samples taken so far
void ColourSampleCollection::calculate()
{
    
    if (sampleIndex == 0) return; // If no samples have been taken, return to avoid undefined behaviour (like division by 0)
    
    // Average calculation (maybe not necessary?)
    double rgSum = 0;
    double rbSum = 0;
    double gbSum = 0;
    double clearSum = 0;
    for (int i=0; i<sampleIndex;++i)
    {
        rgSum += samples[i].rg;
        rbSum += samples[i].rb;
        gbSum += samples[i].gb;
        clearSum += samples[i].clear;
    }
    double rgAvg = rgSum/(double)sampleIndex;
    double rbAvg = rbSum/(double)sampleIndex;
    double gbAvg = gbSum/(double)sampleIndex;
    double clearAvg = clearSum/(double)sampleIndex;

    // Calculate minimum and maximum values

    // Setting the actual values
    thresholds.rgLower = 0;
    thresholds.rgUpper = 0;

    thresholds.rbLower = 0;
    thresholds.rbUpper = 0;

    thresholds.gbLower = 0;
    thresholds.gbUpper = 0;

    thresholds.clearLower = 0;
    thresholds.clearUpper = 0;

    // Ideas for threshold calculation:
    //  - Calculate the min and max and set the thresholds a bit outside of that
    //    Maybe have special cases for certain colour where I know that the threshold can be set loosely (like low down on some ratios on blue)
    //  - Calculate the average and then set the thresholds and standard deviation and set the thresholds some amount of standard deviations outside
    //  - Generally, special cases should be handled where I know that the tolerances are looser so that we don't have thresholds that are stricter than they need to be


}

void ColourSampleCollection::resetIndex()
{
    sampleIndex = 0;
}







void HardwareButton::init()
{
    pinMode(pin, INPUT_PULLUP);
}

bool HardwareButton::isPressed()
{
    return (digitalRead(pin) == LOW);
}