#include "indi_defaultdevice.h"
#include <cstring>
#include <termios.h>
#include <glob.h>

class FlatPanelCover : public INDI::DefaultDevice
{
public:
    FlatPanelCover();
    virtual ~FlatPanelCover();

    const char *getDefaultName() override; // Define default name

protected:
    virtual bool initProperties() override;
    virtual bool updateProperties() override;
    virtual bool Connect() override;
    virtual bool Disconnect() override;
    virtual void TimerHit() override;
    virtual bool ISNewSwitch(const char *dev, const char *name, ISState *states, char *names[], int n) override;
    virtual bool ISNewNumber(const char *dev, const char *name, double values[], char *names[], int n) override;

private:
    bool findArduinoPort();
    bool sendCommand(const char *cmd);
    bool readResponse(char *response, int maxLength);
    int serialFD;
    std::string serialPort;

    INDI::PropertySwitch CoverControl;
    ISwitch CoverOptions[2];

    INDI::PropertyNumber BrightnessControl;
    INumber BrightnessValue[1];

    INDI::PropertyText StatusFeedback;
    IText StatusMessages[1];
};

// Constructor
FlatPanelCover::FlatPanelCover()
{
    setVersion(1, 1);
}

// Set default name and vendor
const char *FlatPanelCover::getDefaultName()
{
    return "PrometheusAstro Flat Panel Cover";
}

FlatPanelCover::~FlatPanelCover()
{
    if (serialFD >= 0)
        close(serialFD);
}

bool FlatPanelCover::initProperties()
{
    INDI::DefaultDevice::initProperties();

    setDeviceName("PrometheusAstro Flat Panel Cover");

    CoverOptions[0].name = "OPEN";
    CoverOptions[1].name = "CLOSE";
    CoverOptions[0].label = "Open Cover";
    CoverOptions[1].label = "Close Cover";
    CoverOptions[0].s = ISS_OFF;
    CoverOptions[1].s = ISS_OFF;

    CoverControl.initSwitch("Cover Control", "Open/Close Flat Panel Cover", MAIN_CONTROL_TAB, CoverOptions, 2, IP_RW, ISR_1OFMANY);
    defineSwitch(&CoverControl);

    BrightnessValue[0].name = "BRIGHTNESS";
    BrightnessValue[0].label = "Brightness Level";
    BrightnessValue[0].min = 0;
    BrightnessValue[0].max = 4095;
    BrightnessValue[0].step = 1;
    BrightnessValue[0].value = 0;

    BrightnessControl.initNumber("Brightness Control", "Set LED Brightness", MAIN_CONTROL_TAB, BrightnessValue, 1, IP_RW, 60);
    defineNumber(&BrightnessControl);

    StatusMessages[0].name = "STATUS";
    StatusMessages[0].label = "Device Status";
    strcpy(StatusMessages[0].text, "Disconnected");

    StatusFeedback.initText("Device Status", "Real-time Status", MAIN_CONTROL_TAB, StatusMessages, 1, IP_RO);
    defineText(&StatusFeedback);

    return true;
}

bool FlatPanelCover::updateProperties()
{
    if (isConnected())
    {
        defineSwitch(&CoverControl);
        defineNumber(&BrightnessControl);
        defineText(&StatusFeedback);
    }
    else
    {
        deleteProperty(CoverControl.name);
        deleteProperty(BrightnessControl.name);
        deleteProperty(StatusFeedback.name);
    }

    return true;
}

bool FlatPanelCover::findArduinoPort()
{
    glob_t glob_result;
    if (glob("/dev/ttyUSB*", 0, NULL, &glob_result) == 0)
    {
        for (size_t i = 0; i < glob_result.gl_pathc; ++i)
        {
            serialPort = glob_result.gl_pathv[i];
            IDLog("Trying port: %s\n", serialPort.c_str());

            serialFD = open(serialPort.c_str(), O_RDWR | O_NOCTTY);
            if (serialFD >= 0)
            {
                globfree(&glob_result);
                return true;
            }
        }
    }

    globfree(&glob_result);
    return false;
}

bool FlatPanelCover::Connect()
{
    if (!findArduinoPort())
    {
        IDLog("No valid serial port found for Arduino.\n");
        return false;
    }

    struct termios options;
    tcgetattr(serialFD, &options);
    cfsetispeed(&options, B9600);
    cfsetospeed(&options, B9600);
    options.c_cflag |= (CLOCAL | CREAD);
    tcsetattr(serialFD, TCSANOW, &options);

    IDLog("Connected to Arduino at %s\n", serialPort.c_str());
    return true;
}

bool FlatPanelCover::Disconnect()
{
    if (serialFD >= 0)
    {
        close(serialFD);
        serialFD = -1;
    }
    return true;
}

void FlatPanelCover::TimerHit()
{
    if (!isConnected())
        return;

    char response[128];
    if (readResponse(response, sizeof(response)))
    {
        if (strstr(response, "STATE OPEN"))
        {
            CoverOptions[0].s = ISS_ON;
            CoverOptions[1].s = ISS_OFF;
            strcpy(StatusMessages[0].text, "Cover Open");
        }
        else if (strstr(response, "STATE CLOSED"))
        {
            CoverOptions[0].s = ISS_OFF;
            CoverOptions[1].s = ISS_ON;
            strcpy(StatusMessages[0].text, "Cover Closed");
        }
        else if (strstr(response, "STATE MOVING"))
        {
            strcpy(StatusMessages[0].text, "Cover Moving...");
        }
        else if (strstr(response, "BRIGHTNESS"))
        {
            BrightnessValue[0].value = atoi(response + 11);
        }

        CoverControl.update();
        BrightnessControl.update();
        StatusFeedback.update();
    }

    SetTimer(1000);
}

bool FlatPanelCover::ISNewSwitch(const char *dev, const char *name, ISState *states, char *names[], int n)
{
    if (!isConnected() || strcmp(dev, getDeviceName()) != 0)
        return false;

    if (strcmp(name, CoverControl.name) == 0)
    {
        if (strcmp(names[0], "OPEN") == 0 && states[0] == ISS_ON)
        {
            sendCommand("OPEN");
        }
        else if (strcmp(names[0], "CLOSE") == 0 && states[1] == ISS_ON)
        {
            sendCommand("CLOSE");
        }

        CoverControl.update();
        return true;
    }

    return INDI::DefaultDevice::ISNewSwitch(dev, name, states, names, n);
}

bool FlatPanelCover::ISNewNumber(const char *dev, const char *name, double values[], char *names[], int n)
{
    if (!isConnected() || strcmp(dev, getDeviceName()) != 0)
        return false;

    if (strcmp(name, BrightnessControl.name) == 0)
    {
        int brightness = static_cast<int>(values[0]);
        if (brightness < 0) brightness = 0;
        if (brightness > 4095) brightness = 4095;

        char command[32];
        snprintf(command, sizeof(command), "BRIGHTNESS %d", brightness);
        sendCommand(command);

        BrightnessValue[0].value = brightness;
        BrightnessControl.update();
        return true;
    }

    return INDI::DefaultDevice::ISNewNumber(dev, name, values, names, n);
}