// Colour sensor header file

#ifndef COLOUR_SENSOR
#define COLOUR_SENSOR

#include <Arduino.h> // WARNING!! May cause bugs because of multiple inclusion

class HardwareButton
{
public:
    // Constructor with initialization
    HardwareButton(int inputPin, bool buttonReversed=false)
    {
        pin = inputPin;
        reversed = buttonReversed;
        init();
    }
    int pin; // The pin the switch is on
    bool isPressed();
private:
    void init();
    bool reversed = false;

};

// MeRGBLed newLed(0, 12);
namespace newLights
{

    struct RGBColour
    {
        int red;
        int green;
        int blue;
    };

    // Turns all lights off
    void turnOff();

    void setColour(int index, RGBColour colour, bool showColour);

    void blink(RGBColour colour);

    void errorBlink();
}

// For colour detection
extern double MAX_DETECTION_DISTANCES[5]; // {50, 30, 60, 70, 0}; // White, black, blue, reflective, unknown
extern double STANDARD_RADIUSES[5]; // {0, 0, 0, 20, 0};// White, black, blue, reflective, unknown


enum FloorColour {
    floor_white,
    floor_black,
    floor_blue,
    floor_reflective,
    floor_unknown,
    floor_notUpdated,
};

struct ColourSample // Should maybe be inside one of the other classes?
{

    ColourSample()
    {
        values[r] = 0;
        values[g] = 0;
        values[b] = 0;
        values[clear] = 0;
        values[rg] = 0;
        values[rb] = 0;
        values[gb] = 0;
    }

    double rVar, gVar, bVar, clearVar, rgVar, rbVar, gbVar;
    
    enum Values
    {
        r,
        g,
        b,
        clear,
        rg,
        rb,
        gb,
        ratios_num,
    };
    double values[ratios_num] {};
    // double rg;
    // double rb;
    // double gb;
};

struct ColourStorageData
{
    ColourStorageData()
    {
        radius = 0;
    }
    ColourStorageData(double rad)
    {
        radius = rad;
    }
    ColourSample s; // The sample itself
    double radius; // Maybe for how close it has to be.
};

void printColSample(ColourSample printSample, bool newLine);
void printColStorData(ColourStorageData printData);


const int MAX_COLOUR_SAMPLES = 8; // Outside of class due to error
const int REFLECTIVE_REFERENCE_NUM = 5;

#define REFLECTIVE_SPLIT // Using individual reflective samples
#ifndef REFLECTIVE_SPLIT
#define REFLECTIVE_COMBINED // Using a single calculated threshold sample like all the other colours do
#endif

class ColourSampleCollection
{
    public:
        ColourSampleCollection()
        {
            ColourSampleCollection(floor_unknown);
        }
        ColourSampleCollection(FloorColour colour)
        {
            colourToBe = colour;
            thresholds.radius = STANDARD_RADIUSES[colourToBe];
        }
        void setReflective(bool newReflectiveState);
        // void setType(FloorColour floorColour);
        bool enterSample(ColourSample sample); // For inputting the coloursamples
        void calculate(); // Calculate the thresholds
        void updateReflective();
        void write(int addresses[REFLECTIVE_REFERENCE_NUM]); // For reflective tiles
        void write(int address); // Should maybe be done outside of the class
        ColourStorageData read(int address); // Should maybe be done outside of the class
        int readReflectiveNum(int addr);
        ColourStorageData readReflective(int addr);
        void resetIndex();
        int getIndex();
        ColourStorageData thresholds {}; // Calculated thresholds
    private:
        ColourSample samples[MAX_COLOUR_SAMPLES]; // Sample values
        
        int sampleIndex = 0; // Keeping track of which sample we are on. Will be equivalent to the number of samples taken

        const double stdDevsToUse = 1; // How many standard deviations that should be used when computing the thresholds

        bool isReflective = false;
        FloorColour colourToBe;
        
        // Functions to aid in computation of the thresholds
        // I want to be able to give rg, rb or gb as an argument (or equivalent) and iterate through an array of colour samples while keeping the ending the same. I do not know how to do this.
        double averageValue(ColourSample::Values value);
        double stdDev(ColourSample::Values value);
        double stdDev(ColourSample::Values value, double average);
        double minValue(ColourSample::Values value);
        double maxValue(ColourSample::Values value);

};

class ColourSensor
{   
    public:
        ColourSensor()
        {
            #ifdef REFLECTIVE_SPLIT
            reflectiveAddresses[0] = reflectiveNumAddr + sizeof(reflectiveNumAddr);
            for (int i=1;i<REFLECTIVE_REFERENCE_NUM;++i)
            {
                reflectiveAddresses[i] = reflectiveAddresses[i-1]+sizeof(ColourStorageData);
            }
            for (int i=0;i<REFLECTIVE_REFERENCE_NUM;++i)
            {
                reflectiveSamples.setReflective(true);
            }
            #endif
        }
        void init();

        FloorColour checkFloorColour();
        FloorColour checkRawFloorColour();

        char floorColourAsChar(FloorColour floorColour);
        void printValues();
        void printRatios();
        void printClearVal();
        void printColourName(FloorColour colourToPrint);

        FloorColour lastKnownFloorColour;
        FloorColour lastFloorColour;

        ColourSample getColourSample();
        
        void clearCalibrationData(); // Prepares for a new calibration by resetting indecies
        void calibrationRoutineLoop();
        void refreshReferences(); // Read colour samples from EEPROM

    private:
        void getRawData(uint16_t *sensorRed, uint16_t *sensorGreen, uint16_t *sensorBlue, uint16_t *sensorClear);
        // int calcColourDistance(int sensorRed, int sensorGreen, int sensorBlue, int referenceRed, int referenceGreen, int referenceBlue);
        bool readSensor();
        FloorColour identifyColour();
        bool blockReflective = false;
        
        // Ratios
        bool lowerUpperEval(double val, double lower, double upper);
        void calcColourRatios(double& rg, double& rb, double& gb);

        // Colour distances
        double getColDistance(ColourSample ref, ColourSample comp);
        double getColDistance(FloorColour ref, ColourSample comp);
        double getColDistance(FloorColour ref, ColourSample comp, int reflectiveIndex);
        int getMinValIndex(double val1, double val2, double val3, double val4);
        FloorColour getClosestColour(ColourSample compare);
        // double MAX_DETECTION_DISTANCE = 50;
        // double REFLECTIVE_MAX_DETECTION_DISTANCE = 100;

        unsigned long lastReadTime = 0; // Keep track of the last time you read from the sensor
        int INTEGRATION_TIME = 60; // The integration time in milliseconds
        uint16_t sensorRed, sensorGreen, sensorBlue, sensorClear; // For raw colour values
        double rgRatio, rbRatio, gbRatio; // For ratios between colours
        ColourSample reading; // For storing the reading of the sensor

        // Buttons for calibration routine
        // HardwareButton blackButton {0, false};
        // HardwareButton blueButton {0, false};
        // HardwareButton reflectiveButton {0, false};
        // HardwareButton whiteButton {0, false};

        // Colour sample collections for calibration routine
        ColourSampleCollection blackSamples {floor_black};
        ColourSampleCollection blueSamples {floor_blue};
        ColourSampleCollection reflectiveSamples {floor_reflective};
        ColourSampleCollection whiteSamples {floor_white};

        // Thresholds for colour recognition
        // ColourStorageData blackReference {1.5, 6 , 1.7, 6 , 1.05, 2.0 , 0, 100};
        // ColourStorageData blueReference {0, 1.05 , 0, 1 , 0, 0.99 , 0, 2000};
        // ColourStorageData reflectiveReference1 {1.0, 1.4 , 1.1, 1.45 , 1.1, 1.3 , 200, 500};
        // ColourStorageData whiteReference {0.9, 1.1 , 1.0, 1.3 , 1.0, 1.2 , 500, 2000};
        const int blackAddr = 0;
        const int blueAddr = sizeof(ColourStorageData);
        const int whiteAddr = 2*sizeof(ColourStorageData);
        ColourStorageData blackReference;
        ColourStorageData blueReference;
        ColourStorageData whiteReference;

        #ifdef REFLECTIVE_SPLIT
        const int reflectiveNumAddr = 3*sizeof(ColourStorageData);
        int reflectiveAddresses[REFLECTIVE_REFERENCE_NUM];
        int usedReflectiveReferences = 0;
        int minReflectiveIndex = 0; // The index for the closest reflective colour
        ColourStorageData reflectiveReferences[REFLECTIVE_REFERENCE_NUM] {true};
        #else
        const int reflectiveAddr = 3*sizeof(ColourStorageData);
        ColourStorageData reflectiveReference;
        #endif
};



#endif