#include <colour_sensor.h>
#include <Arduino.h>
#include <MeRGBLed.h>
#include <Adafruit_TCS34725.h>
#include <Wire.h> // Needed for some reason?
#include <SPI.h> // Needed for some reason?
#include <EEPROM.h> // Potential include issues with the makeblock library.



Adafruit_TCS34725 colSens = Adafruit_TCS34725(TCS34725_INTEGRATIONTIME_60MS, TCS34725_GAIN_1X);
bool readState = false; // Keeps track of whether the sensor was read or not

MeRGBLed newLed(0, 12);
newLights::RGBColour newColourBlack {0, 0, 0};
newLights::RGBColour newColourWhite {100, 100, 100};
newLights::RGBColour newColourBase {5, 42, 0};
newLights::RGBColour newColourOrange {42, 5, 0};
newLights::RGBColour newColourRed {150, 0, 0};
newLights::RGBColour newColourBlue {0, 0, 150};
newLights::RGBColour newColourError {200, 10, 0};
newLights::RGBColour newColourAffirmative { 20, 150, 0};
newLights::RGBColour newColourYellow {50, 50, 0};
newLights::RGBColour newColourPurple {100, 0, 150};

void newLights::turnOff()
{
  setColour(0, newColourBlack, true);
  // ledRing.setColor(colourBlack.red, colourBlack.green, colourBlack.blue);
  // ledRing.show();
}

void newLights::setColour(int index, RGBColour colour, bool showColour)
{
  newLed.setColor(index, colour.red, colour.green, colour.blue);
  if (showColour==true) newLed.show();
}

void newLights::blink(newLights::RGBColour colour)
{
  turnOff();
  for (int i=0;i<3;++i)
  {
    delay(200);
    setColour(0, colour, true);
    // ledRing.setColor(colourBase.red, colourBase.green, colourBase.blue);
    // ledRing.show();
    delay(60);
    turnOff();
  }
}

void newLights::errorBlink()
{
    blink(newColourError);
}




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
    newLed.setpin(44);
    newLed.fillPixelsBak(0, 2, 1);
    newLights::turnOff();

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
    returnData.ratios[ColourSample::clear] = sensorClear;
    returnData.ratios[ColourSample::rg] = rgRatio;
    returnData.ratios[ColourSample::rb] = rbRatio;
    returnData.ratios[ColourSample::gb] = gbRatio;
    
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
    const int buttonDebounceDelay = 1000;
    // Check for and collect samples
    bool changeDetected = false;
    char readChar = ' ';
    if (Serial.available()>0)
    {
        readChar = Serial.read();
    }
    
    if (readChar == 's') //blackButton.isPressed()
    {
        if (blackSamples.enterSample(getColourSample()) == false)
        {
            newLights::errorBlink();
        }
        else
        {
        blackSamples.calculate();
        blackSamples.write(0);
        changeDetected = true;
        for (int i=0;i<blackSamples.getIndex();++i)
            {
                newLights::setColour(i+1, newColourRed, false);
            }
        newLed.show();
        delay(buttonDebounceDelay);
        newLights::turnOff();
        }
    }
    else if (readChar == 'b') // blueButton.isPressed() || 
    {
        if (blueSamples.enterSample(getColourSample()) == false)
        {
            newLights::errorBlink();
        }
        else
        {
        blueSamples.calculate();
        blueSamples.write(sizeof(ColourStorageData));
        changeDetected = true;
        for (int i=0;i<blueSamples.getIndex();++i)
            {
                newLights::setColour(i+1, newColourBlue, false);
            }
        newLed.show();
        delay(buttonDebounceDelay);
        newLights::turnOff();
        }
    }
    else if (readChar == 'r') // reflectiveButton.isPressed() || 
    {
        if (reflectiveSamples.enterSample(getColourSample()) == false)
        {
            newLights::errorBlink();
        }
        else
        {
        reflectiveSamples.calculate();
        reflectiveSamples.write(sizeof(ColourStorageData)*2);
        changeDetected = true;
        for (int i=0;i<reflectiveSamples.getIndex();++i)
            {
                newLights::setColour(i+1, newColourPurple, false);
            }
        newLed.show();
        delay(buttonDebounceDelay);
        newLights::turnOff();
        }
    }
    else if (readChar == 'v') // whiteButton.isPressed() || 
    {
        if (whiteSamples.enterSample(getColourSample())==false)
        {
            newLights::errorBlink();
        }
        else
        {
        whiteSamples.calculate();
        whiteSamples.write(sizeof(ColourStorageData)*3);
        changeDetected = true;
        for (int i=0;i<whiteSamples.getIndex();++i)
            {
                newLights::setColour(i+1, newColourWhite, false);
            }
        newLed.show();
        delay(buttonDebounceDelay);
        newLights::turnOff();
        }
    }

    // Set new thresholds in the identifying code (maybe only if previous step done?)
    if (changeDetected==true)
    {
        refreshThresholds();
        changeDetected = false;
    }

    // Identify the colour on the ground and do not accept not updated as an answer
    FloorColour floorColour = floor_notUpdated;
    while (floorColour == floor_notUpdated)
    {
        floorColour = checkRawFloorColour();
    }

    // Show info about colour on ground (which one, certainty etc.)
    switch (floorColour)
    {
        case floor_black:
            for (int i=0;i<blackSamples.getIndex();++i)
            {
                newLights::setColour(i+1, newColourRed, false);
            }
            newLed.show();
            break;

        case floor_blue:
            for (int i=0;i<blueSamples.getIndex();++i)
            {
                newLights::setColour(i+1, newColourBlue, false);
            }
            newLed.show();
            break;

        case floor_reflective:
            for (int i=0;i<reflectiveSamples.getIndex();++i)
            {
                newLights::setColour(i+1, newColourPurple, false);
            }
            newLed.show();
            break;

        case floor_white:
            for (int i=0;i<whiteSamples.getIndex();++i)
            {
                newLights::setColour(i+1, newColourWhite, false);
            }
            newLed.show();
            break;
        case floor_notUpdated:
            break;

        default:
            newLights::turnOff();
            break;
    }
    
}






void printColourStorageData(ColourStorageData printData)
{
    Serial.print("rg: ");Serial.print(printData.rgLower);Serial.print(" - ");Serial.print(printData.rgUpper);Serial.println("");
    Serial.print("rb: ");Serial.print(printData.rbLower);Serial.print(" - ");Serial.print(printData.rbUpper);Serial.println("");
    Serial.print("gb: ");Serial.print(printData.gbLower);Serial.print(" - ");Serial.print(printData.gbUpper);Serial.println("");
    Serial.print("clear: ");Serial.print(printData.clearLower);Serial.print(" - ");Serial.print(printData.clearUpper);Serial.println("");
}


// Write threshold data to the specified address
void ColourSampleCollection::write(int address)
{
    Serial.println("Writing...");
    // I need to be able to set the startic address of writing and also know the total size of what I store in the EEPROM
    ColourStorageData writeData = thresholds; // The data being written
    printColourStorageData(writeData);
    EEPROM.put(address, writeData);
    Serial.println("Done");
}

// Read stored threshold data at from specified address
ColourStorageData ColourSampleCollection::read(int address)
{
    Serial.println("Reading...");
    ColourStorageData readData; // The data beign read
    EEPROM.get(address, readData);
    printColourStorageData(readData);
    Serial.println("Done");
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
    double rgAvg = averageRatio(ColourSample::rg);
    double rbAvg = averageRatio(ColourSample::rb);
    double gbAvg = averageRatio(ColourSample::gb);
    double clearAvg = averageRatio(ColourSample::clear);

    // Calculate minimum and maximum values

    // Setting the actual values
    thresholds.rgLower = rgAvg - stdDevsToUse*stdDev(ColourSample::rg, rgAvg);
    thresholds.rgUpper = rgAvg + stdDevsToUse*stdDev(ColourSample::rg, rgAvg);

    thresholds.rbLower = rbAvg - stdDevsToUse*stdDev(ColourSample::rb, rbAvg);
    thresholds.rbUpper = rbAvg + stdDevsToUse*stdDev(ColourSample::rb, rbAvg);

    thresholds.gbLower = gbAvg - stdDevsToUse*stdDev(ColourSample::gb, gbAvg);
    thresholds.gbUpper = gbAvg + stdDevsToUse*stdDev(ColourSample::gb, gbAvg);

    thresholds.clearLower = clearAvg - stdDevsToUse*stdDev(ColourSample::clear, clearAvg);
    thresholds.clearUpper = clearAvg + stdDevsToUse*stdDev(ColourSample::clear, clearAvg);

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

int ColourSampleCollection::getIndex()
{
    return sampleIndex;
}

double ColourSampleCollection::averageRatio(ColourSample::Ratios ratio)
{
    double sum = 0;
    for (int i=0;i<sampleIndex;++i)
    {
        sum += samples[i].ratios[ratio];
    }
    return sum/double(sampleIndex);
}

double squareCustom(double x)
{
    return x*x;
}

double ColourSampleCollection::stdDev(ColourSample::Ratios ratio, double average)
{
    double sum = 0;
    for (int i=0;i<sampleIndex;++i)
    {
        sum += squareCustom(samples[i].ratios[ratio]-average);
    }

    return sqrt(sum/(sampleIndex-1));
}

double ColourSampleCollection::stdDev(ColourSample::Ratios ratio)
{
    return stdDev(ratio, averageRatio(ratio));
}





void HardwareButton::init()
{
    pinMode(pin, INPUT_PULLUP);
}

bool HardwareButton::isPressed()
{
    return (digitalRead(pin) == LOW);
}