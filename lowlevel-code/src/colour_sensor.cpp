// Provides code for the colour sensor


#include <colour_sensor.h>
#include <Arduino.h>
#include <MeRGBLed.h>
#include <Adafruit_TCS34725.h>
#include <Wire.h> // Needed for some reason?
#include <SPI.h> // Needed for some reason?
#include <EEPROM.h> // Potential include issues with the makeblock library.



Adafruit_TCS34725 colSens = Adafruit_TCS34725(TCS34725_INTEGRATIONTIME_60MS, TCS34725_GAIN_1X);
bool readState = false; // Keeps track of whether the sensor was read or not

double MAX_DETECTION_DISTANCES[5] {50, 20, 70, 70, 0}; // White, black, blue, reflective, unknown
double STANDARD_RADIUSES[5] {30, 0, 0, 20, 0};// White, black, blue, reflective, unknown

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
newLights::RGBColour newColourPurple {150, 0, 130};

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
    // refreshReferences();

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

void ColourSensor::printColourName(FloorColour colourToPrint)
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

// Returns the euclidean distance between two colour samples.
// Uses only raw sensor data, that is, not ratios
double ColourSensor::getColDistance(ColourSample ref, ColourSample comp)
{
    return sqrt(square(comp.values[ColourSample::r]-ref.values[ColourSample::r]) + square(comp.values[ColourSample::g]-ref.values[ColourSample::g]) + square(comp.values[ColourSample::b]-ref.values[ColourSample::b]) + square(comp.values[ColourSample::clear]-ref.values[ColourSample::clear]));
}

double ColourSensor::getColDistance(FloorColour ref, ColourSample comp)
{
    return getColDistance(ref, comp, 0);   
}

double ColourSensor::getColDistance(FloorColour ref, ColourSample comp, int reflectiveIndex)
{
    ColourSample refSample;

    switch (ref)
    {
        case floor_black:
            return getColDistance(blackReference.s, comp);
            break;
        case floor_blue:
            return getColDistance(blueReference.s, comp);
            break;
        case floor_reflective:
            #ifdef REFLECTIVE_SPLIT
            return getColDistance(reflectiveReferences[reflectiveIndex].s, comp);
            #else
            return getColDistance(reflectiveReference.s, comp);
            #endif
            break;
        case floor_white:
            return getColDistance(whiteReference.s, comp);
            break;
        default:
            return 0;
            break;
    }
}

FloorColour ColourSensor::getClosestColour(ColourSample compare)
{
    double distances[4];
    distances[floor_black] = getColDistance(blackReference.s, compare) - blackReference.radius;
    distances[floor_blue] = getColDistance(blueReference.s, compare) - blueReference.radius;
    distances[floor_white] = getColDistance(whiteReference.s, compare) - whiteReference.radius;
    // Serial.print("Black: ");Serial.print(distances[floor_black] + blackReference.radius);Serial.print(" - ");Serial.print(blackReference.radius);Serial.print(" = ");Serial.println(distances[floor_black]);
    // Serial.print("Blue: ");Serial.print(distances[floor_blue] + blueReference.radius);Serial.print(" - ");Serial.print(blueReference.radius);Serial.print(" = ");Serial.println(distances[floor_blue]);
    // Serial.print("White: ");Serial.print(distances[floor_white] + whiteReference.radius);Serial.print(" - ");Serial.print(whiteReference.radius);Serial.print(" = ");Serial.println(distances[floor_white]);
    
    #ifdef REFLECTIVE_SPLIT
    double reflectiveDistances[REFLECTIVE_REFERENCE_NUM];
    for (int i=0; i<usedReflectiveReferences; ++i)
    {
        reflectiveDistances[i] = getColDistance(reflectiveReferences[i].s, compare) - reflectiveReferences[i].radius;
        // Serial.print("Reflective");Serial.print(i);Serial.print(": ");Serial.print(reflectiveDistances[i] + reflectiveReferences[i].radius);Serial.print(" - ");Serial.print(reflectiveReferences[i].radius);Serial.print(" = ");Serial.println(reflectiveDistances[i]);
    }
    #else
    distances[floor_reflective] = getColDistance(reflectiveReference.s, compare) - reflectiveReference.radius;
    // Serial.print("Reflective: ");Serial.print(distances[floor_reflective] + reflectiveReference.radius);Serial.print(" - ");Serial.print(reflectiveReference.radius);Serial.print(" = ");Serial.println(distances[floor_reflectiveReference]);
    #endif
    // Serial.println("");Serial.println("");
    
    FloorColour minCol = floor_black;
    if (distances[floor_blue] < distances[minCol]) minCol = floor_blue;
    if (distances[floor_white] < distances[minCol]) minCol = floor_white;

    #ifdef REFLECTIVE_SPLIT
    for (int i=0; i<usedReflectiveReferences; ++i)
    {
        if (minCol != floor_reflective)
        {
            if (reflectiveDistances[i] < distances[minCol])
            {
                minCol = floor_reflective;
                minReflectiveIndex = i;
            }
        }
        else
        {
            if (reflectiveDistances[i] < reflectiveDistances[minCol])
            {
                minCol = floor_reflective;
                minReflectiveIndex = i;
            }
        }
    }
    #else
    if (distances[floor_reflective] < distances[minCol]) minCol = floor_reflective;
    #endif

    return minCol;
}


// Reads the sensor data into global variables if new sensor data is present.
// Returns true if a value was read and false if it was not (too close to last time)
bool ColourSensor::readSensor()
{
    if ((millis()-lastReadTime) > INTEGRATION_TIME) // Check if enough time has passed
    {
        getRawData(&sensorRed, &sensorGreen, &sensorBlue, &sensorClear);
        reading.values[ColourSample::r] = sensorRed;
        reading.values[ColourSample::g] = sensorGreen;
        reading.values[ColourSample::b] = sensorBlue;
        reading.values[ColourSample::clear] = sensorClear;
        lastReadTime = millis(); // Update the timeflag
        return true;
    }
    else return false;
    
}

bool ColourSensor::isSpike()
{
    // double spikeThreshold = whiteReference.s.values[ColourSample::clear] + 200;
    // if (reading.values[ColourSample::clear] > spikeThreshold)
    // {
    //     return true;
    // }
    // else
    // {
    //     return false;
    // }
    return false; // For now, as it is not tuned correclty
}

// Identify the colour read by readSensor()
FloorColour ColourSensor::identifyColour()
{

    // Identification using ratios

    if (readState == false)
    {
        return floor_notUpdated;
    }
    
    // // Calculation using thresholds

    // calcColourRatios(rgRatio, rbRatio, gbRatio);

    // For debugging/tuning¨
    // printValues();

    // int boolSumBlack = lowerUpperEval(rgRatio, blackReference.rgLower, blackReference.rgUpper) + lowerUpperEval(rbRatio, blackReference.rbLower, blackReference.rbUpper) + lowerUpperEval(gbRatio, blackReference.gbLower, blackReference.gbUpper) + (sensorClear < blackReference.clearUpper);
    // int boolSumBlue = lowerUpperEval(rgRatio, blueReference.rgLower, blueReference.rgUpper) + lowerUpperEval(rbRatio, blueReference.rbLower, blueReference.rbUpper) + lowerUpperEval(gbRatio, blueReference.gbLower, blueReference.gbUpper);
    // int boolSumReflective = lowerUpperEval(rgRatio, reflectiveReference1.rgLower, reflectiveReference1.rgUpper) + lowerUpperEval(rbRatio, reflectiveReference1.rbLower, reflectiveReference1.rbUpper) + lowerUpperEval(gbRatio, reflectiveReference1.gbLower, reflectiveReference1.gbUpper);
    // int boolSumWhite = lowerUpperEval(rgRatio, whiteReference.rgLower, whiteReference.rgUpper) + lowerUpperEval(rbRatio, whiteReference.rbLower, whiteReference.rbUpper) + lowerUpperEval(gbRatio, whiteReference.gbLower, whiteReference.gbUpper) + (sensorClear > whiteReference.clearLower);

    // if (boolSumBlack >= 3) return floor_black;
    // else if (boolSumBlue >=3) return floor_blue; // Could be lower (0.85?) for both
    // else if (boolSumReflective >= 3 && (sensorClear > reflectiveReference1.clearLower && sensorClear < reflectiveReference1.clearUpper)) return floor_reflective;
    // else if (boolSumWhite >= 3) return floor_white; // reflective falls (partly) into the same span, but because reflective would have returned all that is left in this area should be white
    // else return floor_unknown;
    
    // // return floor_unknown; // For debugging, disabling colour detection

    // Calculation using colour distances

    // #warning Debugging
    // return floor_unknown; // For debugging. Uncomment to disable colour detection

    FloorColour closestCol = getClosestColour(reading);
    ColourStorageData reference;
    switch (closestCol)
    {
        case floor_black:
            reference = blackReference;
            break;
        case floor_blue:
            reference = blueReference;
            break;
        case floor_reflective:
            #ifdef REFLECTIVE_SPLIT
            reference = reflectiveReferences[minReflectiveIndex];
            #else
            reference = reflectiveReference;
            #endif
            // If both white and reflective are detected, make sure to detect white
            if (getColDistance(whiteReference.s, reading)-whiteReference.radius <= MAX_DETECTION_DISTANCES[floor_white])
            {
                newLights::setColour(3, newColourWhite, true);
                closestCol = floor_white;
                reference = whiteReference;
            }
            else
            {
                // Change nothing, as you should detect reflective
            }
            break;
        case floor_white:
            reference = whiteReference;
            break;
        default:
            reference = blackReference; // Just to have it initialized, but I do not know what to do with it. It should not give problems
            break;
    }
    double colDistance = getColDistance(reference.s, reading);
    double maxDetectDist = MAX_DETECTION_DISTANCES[closestCol];
    if (colDistance <= maxDetectDist + reference.radius) return closestCol;
    else return floor_unknown;
    
    // Old colour distance matching
    // if (getColDistance(blackReference.s, reading) <= MAX_DETECTION_DISTANCE + blackReference.radius) return floor_black;
    // else if (getColDistance(blueReference.s, reading) <= MAX_DETECTION_DISTANCE + blueReference.radius) return floor_blue;
    // else if (getColDistance(reflectiveReference1.s, reading) <= MAX_DETECTION_DISTANCE + reflectiveReference1.radius) return floor_reflective;
    // else if (getColDistance(whiteReference.s, reading) <= MAX_DETECTION_DISTANCE + whiteReference.radius) return floor_white;
    // else return floor_unknown;


    
}

FloorColour ColourSensor::checkRawFloorColour()
{
    readState = readSensor();
    return identifyColour();
}

// Returns the last floor colour (eg. the one from the latest update).
FloorColour ColourSensor::checkFloorColour()
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

char ColourSensor::floorColourAsChar(FloorColour floorColour)
{
  switch (floorColour)
  {
    case floor_black:
      return 's';
      break;
    case floor_blue:
      return 'b';
      break;
    case floor_reflective:
      return 'c';
      break;
    case floor_white:
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

    returnData.values[ColourSample::r] = sensorRed;
    returnData.values[ColourSample::g] = sensorGreen;
    returnData.values[ColourSample::b] = sensorBlue;
    returnData.values[ColourSample::clear] = sensorClear;
    returnData.values[ColourSample::rg] = rgRatio;
    returnData.values[ColourSample::rb] = rbRatio;
    returnData.values[ColourSample::gb] = gbRatio;
    
    return returnData;
}

// Reads thresholds from EEPROM
void ColourSensor::refreshReferences()
{
    // Serial.println("---Refreshing---");
    blackReference = blackSamples.read(blackAddr);
    // Serial.print("Black: ");printCorefreshlStorData(blackReference);
    blueReference = blueSamples.read(blueAddr);
    // Serial.print("Blue: ");printColStorData(blueReference);
    whiteReference = whiteSamples.read(whiteAddr);
    // Serial.print("White: ");printColStorData(whiteReference);

    #ifdef REFLECTIVE_SPLIT
    usedReflectiveReferences = reflectiveSamples.readReflectiveNum(reflectiveNumAddr);
    // Serial.print("Used reflective references is: ");Serial.println(usedReflectiveReferences);
    for (int i=0;i<usedReflectiveReferences;++i)
    {
        reflectiveReferences[i] = reflectiveSamples.readReflective(reflectiveAddresses[i]);
        // Serial.print("Reflective");Serial.print(i);Serial.print(" ");printColStorData(reflectiveReferences[i]);
    }
    #else
    reflectiveReference = reflectiveSamples.read(reflectiveAddr);
    Serial.print("Reflective: ");printColStorData(reflectiveReference);
    #endif;
}

// Resets the indecies for samples
void ColourSensor::clearCalibrationData()
{
    blackSamples.resetIndex();
    blueSamples.resetIndex();
    reflectiveSamples.resetIndex();
    whiteSamples.resetIndex();
}

void ColourSensor::calibrationRoutineLoop()
{
    const int buttonDebounceDelay = 1000; // For button debounce and to give time to see the light indications
    // Check for and collect samples
    bool changeDetected = false;
    char readChar = ' ';
    if (Serial.available()>0)
    {
        readChar = Serial.read();
        Serial.println("");
        Serial.println("");
        Serial.println("");
        Serial.println("");
    }
    else
    {
        // printValues();
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
        blackSamples.write(blackAddr);
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
        blueSamples.write(blueAddr);
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
            #ifdef REFLECTIVE_SPLIT
            reflectiveSamples.updateReflective();
            EEPROM.put(reflectiveNumAddr, reflectiveSamples.getIndex()); // Write the number of reflective samples stored
            Serial.print("Writing reflective number: ");Serial.println(reflectiveSamples.getIndex());
            reflectiveSamples.write(reflectiveAddresses); // Write the samples themselves
            #else
            reflectiveSamples.calculate();
            reflectiveSamples.write(reflectiveAddr);
            #endif

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
        // if (whiteSamples.thresholds.radius < 30);
        whiteSamples.write(whiteAddr);
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
    else if (readChar==' ')
    {
        delay(20);
    }
    else
    {
        newLights::errorBlink();
    }

    // Set new thresholds in the identifying code (maybe only if previous step done?)
    if (changeDetected==true)
    {
        refreshReferences();
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






// void printColourStorageData(ColourStorageData printData)
// {
//     Serial.print("rg: ");Serial.print(printData.rgLower, 4);Serial.print(" - ");Serial.print(printData.rgUpper, 4);Serial.println("");
//     Serial.print("rb: ");Serial.print(printData.rbLower, 4);Serial.print(" - ");Serial.print(printData.rbUpper, 4);Serial.println("");
//     Serial.print("gb: ");Serial.print(printData.gbLower, 4);Serial.print(" - ");Serial.print(printData.gbUpper, 4);Serial.println("");
//     Serial.print("clear: ");Serial.print(printData.clearLower, 4);Serial.print(" - ");Serial.print(printData.clearUpper, 4);Serial.println("");
// }

void ColourSampleCollection::setReflective(bool newReflectiveState)
{
    isReflective = newReflectiveState;
}

// For reflective colour
void ColourSampleCollection::write(int addresses[REFLECTIVE_REFERENCE_NUM])
{
    if (isReflective==true)
    {
        EEPROM.put(addresses[sampleIndex-1], thresholds);
        Serial.print("Writing reflective number: ");Serial.print(sampleIndex);Serial.print(" With data: ");printColStorData(thresholds);
    }
    else
    {
        // Do nothing
        Serial.println("Not reflective!");
    }
}

// Write threshold data to the specified address
void ColourSampleCollection::write(int address)
{
    // Serial.println("Writing...");
    // I need to be able to set the startic address of writing and also know the total of what I store in the EEPROM
    ColourStorageData writeData = thresholds; // The data being written
    Serial.print("Writing: ");printColStorData(writeData);
    EEPROM.put(address, writeData);
    // Serial.println("Done");
}

// Read stored threshold data at from specified address
ColourStorageData ColourSampleCollection::read(int address)
{
    // Serial.println("Reading...");
    ColourStorageData readData; // The data beign read
    EEPROM.get(address, readData);
    // printColStorData(readData);
    // Serial.println("Done");
    return readData;
}

int ColourSampleCollection::readReflectiveNum(int addr)
{
    int num = 0;
    EEPROM.get(addr, num);
    // Serial.println("Reflective num is: ");Serial.print(num);Serial.println("");
    return num;
}

ColourStorageData ColourSampleCollection::readReflective(int addr)
{
    ColourStorageData readData;
    EEPROM.get(addr, readData);
    // printColStorData(readData);
    return readData;
}

bool ColourSampleCollection::enterSample(ColourSample sample)
{
    Serial.print("Sample entered: ");printColSample(sample, true);
    if ( 0 <= sampleIndex && ((sampleIndex < MAX_COLOUR_SAMPLES && isReflective==false) || (sampleIndex < REFLECTIVE_REFERENCE_NUM && isReflective==true)))
    {
        samples[sampleIndex] = sample;
        ++sampleIndex;
        return true;
    } else return false;
}

const double THRESHOLD_MINMAX_MARGIN = 0.02;

// Calculates the thresholds based on the samples taken so far
void ColourSampleCollection::calculate()
{
    
    if (sampleIndex == 0) return; // If no samples have been taken, return to avoid undefined behaviour (like division by 0)
    
    // Average calculation (maybe not necessary?)
    double rAvg = averageValue(ColourSample::r);
    double gAvg = averageValue(ColourSample::g);
    double bAvg = averageValue(ColourSample::b);
    double clearAvg = averageValue(ColourSample::clear);

    thresholds.s.values[ColourSample::r] = rAvg;
    thresholds.s.values[ColourSample::g] = gAvg;
    thresholds.s.values[ColourSample::b] = bAvg;
    thresholds.s.values[ColourSample::clear] = clearAvg;
    
    double rDist = abs(maxValue(ColourSample::r) - minValue(ColourSample::r));
    double gDist = abs(maxValue(ColourSample::g) - minValue(ColourSample::g));
    double bDist = abs(maxValue(ColourSample::b) - minValue(ColourSample::b));
    double clearDist = abs(maxValue(ColourSample::clear) - minValue(ColourSample::clear));

    // Calculate minimum and maximum values

    // Setting the actual values
    double potRadius = (rDist+gDist+bDist+clearDist)/4.0;
    if (potRadius > STANDARD_RADIUSES[colourToBe]) // Set it so you that the radius cannot be smaller than the standard one
    {
        thresholds.radius = potRadius; // Average of half of min-max distances
    }
    else
    {
        thresholds.radius = STANDARD_RADIUSES[colourToBe];
    }

    // Ideas for threshold calculation:
    //  - Calculate the min and max and set the thresholds a bit outside of that
    //    Maybe have special cases for certain colour where I know that the threshold can be set loosely (like low down on some ratios on blue)
    //  - Calculate the average and then set the thresholds and standard deviation and set the thresholds some amount of standard deviations outside
    //  - Generally, special cases should be handled where I know that the tolerances are looser so that we don't have thresholds that are stricter than they need to be


}

void ColourSampleCollection::updateReflective()
{
    if (sampleIndex==0) return;

    thresholds.s.values[ColourSample::r] = samples[sampleIndex-1].values[ColourSample::r];
    thresholds.s.values[ColourSample::g] = samples[sampleIndex-1].values[ColourSample::g];
    thresholds.s.values[ColourSample::b] = samples[sampleIndex-1].values[ColourSample::b];
    thresholds.s.values[ColourSample::clear] = samples[sampleIndex-1].values[ColourSample::clear];

    thresholds.radius = STANDARD_RADIUSES[floor_reflective];
}

void ColourSampleCollection::resetIndex()
{
    sampleIndex = 0;
}

int ColourSampleCollection::getIndex()
{
    return sampleIndex;
}

double ColourSampleCollection::averageValue(ColourSample::Values value)
{
    double sum = 0;
    for (int i=0;i<sampleIndex;++i)
    {
        sum += samples[i].values[value];
    }
    return sum/double(sampleIndex);
}

double squareCustom(double x)
{
    return x*x;
}

double ColourSampleCollection::stdDev(ColourSample::Values value, double average)
{
    double sum = 0;
    for (int i=0;i<sampleIndex;++i)
    {
        sum += squareCustom(samples[i].values[value]-average);
    }

    return sqrt(sum/(sampleIndex-1));
}

double ColourSampleCollection::stdDev(ColourSample::Values value)
{
    return stdDev(value, averageValue(value));
}

double ColourSampleCollection::minValue(ColourSample::Values value)
{
    if (sampleIndex==0) return -1; // Should not need to, but just to be safe (already done in calling function)
    double minVal = samples[0].values[value];
    for (int i=1;i<sampleIndex;++i)
    {
        double comparer = samples[i].values[value];
        if (comparer < minVal) minVal = comparer;
    }
    return minVal;
}

double ColourSampleCollection::maxValue(ColourSample::Values value)
{
    if (sampleIndex==0) return -1; // Should not need to, but just to be safe (already done in calling function)
    double maxVal = samples[0].values[value];
    for (int i=1;i<sampleIndex;++i)
    {
        double comparer = samples[i].values[value];
        if (comparer > maxVal) maxVal = comparer;
    }
    return maxVal;
}


void printColStorData(ColourStorageData printData)
{
    printColSample(printData.s, false);
    Serial.print("Radius: ");Serial.print(printData.radius);Serial.print("  ");
    Serial.println("");
}

void printColSample(ColourSample printSample, bool newLine)
{
    Serial.print("R: "); Serial.print(printSample.values[ColourSample::r], 1); Serial.print(" ");
    Serial.print("G: "); Serial.print(printSample.values[ColourSample::g], 1); Serial.print(" ");
    Serial.print("B: "); Serial.print(printSample.values[ColourSample::b], 1); Serial.print(" ");
    Serial.print("C: "); Serial.print(printSample.values[ColourSample::clear], 1); Serial.print(" ");
    if (newLine) Serial.println(" ");
}


void HardwareButton::init()
{
    pinMode(pin, INPUT_PULLUP);
}

bool HardwareButton::isPressed()
{
    if (reversed==false) return (digitalRead(pin) == LOW); // Normal
    else return (digitalRead(pin) == HIGH); // Reversed
}