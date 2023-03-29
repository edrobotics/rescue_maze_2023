#ifndef COLOUR_SENSOR
#define COLOUR_SENSOR

#include <Arduino.h> // WARNING!! May cause bugs because of multiple inclusion


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
        char floorColourAsChar(FloorColour floorColour);
        void printValues();
        void printRatios();
        void printColourName(FloorColour colourToPrint);

        FloorColour lastKnownFloorColour;
        FloorColour lastFloorColour;

    private:
        void getRawData(uint16_t *sensorRed, uint16_t *sensorGreen, uint16_t *sensorBlue, uint16_t *sensorClear);
        // int calcColourDistance(int sensorRed, int sensorGreen, int sensorBlue, int referenceRed, int referenceGreen, int referenceBlue);
        bool lowerUpperEval(double val, double lower, double upper);
        void calcColourRatios(double& rg, double& rb, double& gb);
        bool readSensor();
        FloorColour identifyColour();

        unsigned long lastReadTime = 0; // Keep track of the last time you read from the sensor
        int INTEGRATION_TIME = 120; // The integration time in milliseconds
        uint16_t sensorRed, sensorGreen, sensorBlue, sensorClear; // For raw colour values
        double rgRatio, rbRatio, gbRatio; // For ratios between colours

        // const int SAMPLES[4][3]; // For distance based colour recognition
        // int measuredSample[10][3]; // For storing measurements during calibration
        // const int SAMPLES_SIZE = sizeof(SAMPLES) / sizeof(SAMPLES[0]);

};



#endif