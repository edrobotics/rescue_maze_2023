#ifndef COLOUR_SENSOR
#define COLOUR_SENSOR

#include <Arduino.h> // WARNING!! May cause bugs because of multiple inclusion

class HardwareButton
{
public:
  const int pin; // The pin the switch is on
  void init();
  bool isPressed();
};

struct ColourSample // Should maybe be inside one of the other classes?
{
    double r;
    double g;
    double b;
    double clear;
    double rg;
    double rb;
    double gb;
};

struct ColourStorageData
{
    double rgLower;
    double rgUpper;

    double rbLower;
    double rbUpper;

    double gbLower;
    double gbUpper;

    double clearLower;
    double clearUpper;
};

const int MAX_COLOUR_SAMPLES = 5; // Outside of class due to error

class ColourSampleCollection
{
    public:
        bool enterSample(ColourSample sample); // For inputting the coloursamples
        void calculate(); // Calculate the thresholds
        void write(int address); // Should maybe be done outside of the class
        ColourStorageData read(int address); // Should maybe be done outside of the class
        void resetIndex();
    private:
        ColourSample samples[MAX_COLOUR_SAMPLES]; // Sample values
        
        int sampleIndex = 0; // Keeping track of which sample we are on. Will be equivalent to the number of samples taken

        ColourStorageData thresholds {}; // Calculated thresholds
        
        // Functions to aid in computation of the thresholds
        // I want to be able to give rg, rb or gb as an argument (or equivalent) and iterate through an array of colour samples while keeping the ending the same. I do not know how to do this.
        double averageRatio();
        double minValue();

};


class ColourSensor
{   
    public:
        void init();
        enum FloorColour {
            floor_white,
            floor_black,
            floor_blue,
            floor_reflective,
            floor_unknown,
            floor_notUpdated,
        };

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
        
        void clearCalibrationData(); // Prepares for a new calibration
        void calibrationRoutineLoop();
        void refreshThresholds();

    private:
        void getRawData(uint16_t *sensorRed, uint16_t *sensorGreen, uint16_t *sensorBlue, uint16_t *sensorClear);
        // int calcColourDistance(int sensorRed, int sensorGreen, int sensorBlue, int referenceRed, int referenceGreen, int referenceBlue);
        bool lowerUpperEval(double val, double lower, double upper);
        void calcColourRatios(double& rg, double& rb, double& gb);
        bool readSensor();
        FloorColour identifyColour();

        unsigned long lastReadTime = 0; // Keep track of the last time you read from the sensor
        int INTEGRATION_TIME = 60; // The integration time in milliseconds
        uint16_t sensorRed, sensorGreen, sensorBlue, sensorClear; // For raw colour values
        double rgRatio, rbRatio, gbRatio; // For ratios between colours

        // Buttons for calibration routine
        HardwareButton blackButton {0};
        HardwareButton blueButton {0};
        HardwareButton reflectiveButton {0};
        HardwareButton whiteButton {0};

        // Colour sample collections for calibration routine
        ColourSampleCollection blackSamples;
        ColourSampleCollection blueSamples;
        ColourSampleCollection reflectiveSamples;
        ColourSampleCollection whiteSamples;

        // Thresholds for colour recognition
        ColourStorageData blackThresholds {1.5, 6 , 1.7, 6 , 1.05, 2.0 , 0, 100};
        ColourStorageData blueThresholds {0, 1.05 , 0, 1 , 0, 0.99 , 0, 2000};
        ColourStorageData reflectiveThresholds {1.0, 1.4 , 1.1, 1.45 , 1.1, 1.3 , 200, 500};
        ColourStorageData whiteThresholds {0.9, 1.1 , 1.0, 1.3 , 1.0, 1.2 , 500, 2000};
};



#endif