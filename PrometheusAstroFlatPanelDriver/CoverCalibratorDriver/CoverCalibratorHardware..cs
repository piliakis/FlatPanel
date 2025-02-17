using ASCOM;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.IO.Ports;

namespace ASCOM.PrometheusAstroFlatPanelCover.CoverCalibrator
{
    /// <summary>
    /// Hardware interface for the Prometheus Astro Flat Panel Cover.
    /// Implements the necessary ASCOM CoverCalibrator interface methods and properties.
    /// </summary>
    public static class CoverCalibratorHardware
    {
        // Constants for ASCOM Profile persistence
        public const string comPortProfileName = "COM Port";
        public const string comPortDefault = "COM1";
        public const string traceStateProfileName = "Trace Level";
        public const string traceStateDefault = "false";

        // Driver identification
        public static readonly string DriverProgId = "ASCOM.PrometheusAstroFlatPanelCover.CoverCalibrator";
        public static readonly string DriverDescription = "Prometheus Astro Flat Panel Cover ASCOM Driver v1.0";
        public static readonly string Name = "Prometheus Astro Flat Panel Cover";

        // Configuration
        public static string comPort = comPortDefault;

        // Connection state
        private static bool connectedState = false;
        private static SerialPort serialPort;
        public static TraceLogger tl;

        // Cover and calibrator states
        public static CoverStatus coverState = CoverStatus.Unknown;
        public static CalibratorStatus calibratorState = CalibratorStatus.Off;
        private static int brightnessLevel = 0;
        public const int maxBrightness = 4096;

        /// <summary>
        /// Static constructor initializes the TraceLogger and reads the ASCOM profile.
        /// </summary>
        static CoverCalibratorHardware()
        {
            tl = new TraceLogger("", "PrometheusAstroFlatPanelCover.Hardware");
            ReadProfile();
        }

        /// <summary>
        /// Initializes the hardware connection parameters.
        /// </summary>
        public static void InitialiseHardware()
        {
            if (serialPort == null)
            {
                serialPort = new SerialPort
                {
                    PortName = comPort,
                    BaudRate = 9600,
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    ReadTimeout = 2000,
                    WriteTimeout = 2000
                };
                serialPort.DataReceived += SerialPort_DataReceived;
            }

            connectedState = false;
        }

        /// <summary>
        /// Displays the Setup Dialog for the driver.
        /// </summary>
        public static void SetupDialog()
        {
            using (SetupDialogForm form = new SetupDialogForm(tl))
            {
                form.ShowDialog();
            }
        }


        /// <summary>
        /// Sets the connection state of the hardware.
        /// </summary>
        /// <param name="uniqueId">Unique identifier for the driver instance.</param>
        /// <param name="newState">True to connect, false to disconnect.</param>
        public static void SetConnected(Guid uniqueId, bool newState)
        {
            if (newState)
            {
                Connect(uniqueId);
            }
            else
            {
                Disconnect(uniqueId);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the hardware is currently connected.
        /// </summary>
        public static bool Connecting => connectedState;


        /// <summary>
        /// Gets the list of supported ASCOM actions.
        /// Currently returns an empty list as no custom actions are implemented.
        /// </summary>
        public static ArrayList SupportedActions => new ArrayList();

        /// <summary>
        /// Executes a specified ASCOM action.
        /// Currently not implemented.
        /// </summary>
        /// <param name="actionName">Name of the action to execute.</param>
        /// <param name="actionParameters">Parameters for the action.</param>
        /// <returns>Result of the action.</returns>
        public static string Action(string actionName, string actionParameters)
        {
            throw new MethodNotImplementedException($"Action '{actionName}' is not implemented.");
        }

        /// <summary>
        /// Sends a command to the device without waiting for a response.
        /// Not implemented.
        /// </summary>
        /// <param name="command">Command string to send.</param>
        /// <param name="raw">If true, sends the command as-is.</param>
        public static void CommandBlind(string command, bool raw)
        {
            throw new MethodNotImplementedException("CommandBlind is not implemented.");
        }

        /// <summary>
        /// Sends a command to the device and expects a boolean response.
        /// Not implemented.
        /// </summary>
        /// <param name="command">Command string to send.</param>
        /// <param name="raw">If true, sends the command as-is.</param>
        /// <returns>Boolean response from the device.</returns>
        public static bool CommandBool(string command, bool raw)
        {
            throw new MethodNotImplementedException("CommandBool is not implemented.");
        }

        /// <summary>
        /// Sends a command to the device and expects a string response.
        /// Not implemented.
        /// </summary>
        /// <param name="command">Command string to send.</param>
        /// <param name="raw">If true, sends the command as-is.</param>
        /// <returns>String response from the device.</returns>
        public static string CommandString(string command, bool raw)
        {
            throw new MethodNotImplementedException("CommandString is not implemented.");
        }

        /// <summary>
        /// Connects to the hardware device.
        /// </summary>
        /// <param name="uniqueId">Unique identifier for the connection instance.</param>
        public static void Connect(Guid uniqueId)
        {
            if (connectedState) return;

            try
            {
                serialPort.Open();
                connectedState = true;
                tl.LogMessage("Connect", "Hardware connected.");
                SendCommand("STATE"); // Query initial state
            }
            catch (Exception ex)
            {
                tl.LogMessage("Connect", $"Exception: {ex.Message}");
                throw new ASCOM.NotConnectedException("Unable to connect to hardware.", ex);
            }
        }

        /// <summary>
        /// Disconnects from the hardware device.
        /// </summary>
        /// <param name="uniqueId">Unique identifier for the connection instance.</param>
        public static void Disconnect(Guid uniqueId)
        {
            if (!connectedState) return;

            try
            {
                serialPort.Close();
                connectedState = false;
                tl.LogMessage("Disconnect", "Hardware disconnected.");
            }
            catch (Exception ex)
            {
                tl.LogMessage("Disconnect", $"Exception: {ex.Message}");
                throw new ASCOM.InvalidOperationException("Unable to disconnect from hardware.", ex);
            }
        }

        /// <summary>
        /// Gets a description of the driver.
        /// </summary>
        public static string Description => DriverDescription;

        /// <summary>
        /// Gets driver information, including version.
        /// </summary>
        public static string DriverInfo => "Prometheus Astro Flat Panel Cover Driver. Version 1.0.";

        /// <summary>
        /// Gets the driver version.
        /// </summary>
        public static string DriverVersion => "1.0";

        /// <summary>
        /// Gets the ASCOM interface version.
        /// </summary>
        public static short InterfaceVersion => (short)2;

        /// <summary>
        /// Gets the name of the driver.
        /// </summary>

        /// <summary>
        /// Gets the current state of the cover.
        /// </summary>
        public static CoverStatus CoverState => coverState;

        /// <summary>
        /// Gets a value indicating whether the cover is moving.
        /// </summary>
        public static bool CoverMoving => coverState == CoverStatus.Moving;

        /// <summary>
        /// Opens the cover.
        /// </summary>
        public static void OpenCover()
        {
            CheckConnected("OpenCover");
            SendCommand("OPEN");
            coverState = CoverStatus.Moving;
        }

        /// <summary>
        /// Closes the cover.
        /// </summary>
        public static void CloseCover()
        {
            CheckConnected("CloseCover");
            SendCommand("CLOSE");
            coverState = CoverStatus.Moving;
        }

        /// <summary>
        /// Halts any ongoing cover movement.
        /// </summary>
        public static void HaltCover()
        {
            CheckConnected("HaltCover");
            SendCommand("HALT");
            coverState = CoverStatus.Unknown;
        }

        /// <summary>
        /// Gets the current calibrator state.
        /// </summary>
        public static CalibratorStatus CalibratorState => calibratorState;

        /// <summary>
        /// Gets a value indicating whether the calibrator state is changing.
        /// </summary>
        public static bool CalibratorChanging => calibratorState == CalibratorStatus.NotReady;

        /// <summary>
        /// Gets the current brightness level of the calibrator.
        /// </summary>
        public static int Brightness => brightnessLevel;

        /// <summary>
        /// Gets the maximum brightness level of the calibrator.
        /// </summary>
        public static int MaxBrightness => maxBrightness;

        /// <summary>
        /// Turns the calibrator on at the specified brightness level.
        /// </summary>
        /// <param name="brightness">Brightness level (0 to MaxBrightness).</param>
        public static void CalibratorOn(int brightness)
        {
            CheckConnected("CalibratorOn");
            if (brightness < 0 || brightness > maxBrightness)
                throw new InvalidValueException($"Brightness must be between 0 and {maxBrightness}.");

            SendCommand($"BRIGHTNESS {brightness}");
            calibratorState = CalibratorStatus.Ready;
            brightnessLevel = brightness;
        }

        /// <summary>
        /// Turns the calibrator off.
        /// </summary>
        public static void CalibratorOff()
        {
            CheckConnected("CalibratorOff");
            SendCommand("BRIGHTNESS 0");
            calibratorState = CalibratorStatus.Off;
            brightnessLevel = 0;
        }

        /// <summary>
        /// Writes the current configuration to the ASCOM Profile Store.
        /// </summary>
        public static void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "CoverCalibrator";
                driverProfile.WriteValue(DriverProgId, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(DriverProgId, comPortProfileName, comPort);
            }
        }

        /// <summary>
        /// Reads the configuration from the ASCOM Profile Store.
        /// </summary>
        public static void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "CoverCalibrator";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(DriverProgId, traceStateProfileName, string.Empty, traceStateDefault));
                comPort = driverProfile.GetValue(DriverProgId, comPortProfileName, string.Empty, comPortDefault);
            }
        }

        /// <summary>
        /// Logs a message to the Trace Logger.
        /// </summary>
        /// <param name="identifier">Identifier for the log message.</param>
        /// <param name="message">The log message.</param>
        public static void LogMessage(string identifier, string message)
        {
            if (tl != null)
                tl.LogMessage(identifier, message);
        }

        /// <summary>
        /// Checks if the hardware is connected before performing an action.
        /// </summary>
        /// <param name="action">The action being performed.</param>
        private static void CheckConnected(string action)
        {
            if (!connectedState || serialPort == null || !serialPort.IsOpen)
            {
                throw new NotConnectedException($"Cannot execute {action}, hardware not connected.");
            }
        }

        /// <summary>
        /// Sends a command to the Arduino device via the serial port.
        /// </summary>
        /// <param name="command">The command to send.</param>
        private static void SendCommand(string command)
        {
            if (!serialPort.IsOpen)
            {
                throw new NotConnectedException("Serial port not open.");
            }

            tl.LogMessage("SendCommand", $"Sending: {command}");
            serialPort.WriteLine(command);
        }

        /// <summary>
        /// Handles data received from the serial port.
        /// Updates the cover and calibrator states based on the response.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Data received event arguments.</param>
        private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string response = serialPort.ReadLine().Trim();
                LogMessage("SerialPort_DataReceived", $"Response: {response}");

                // Parse response and update states
                if (response.StartsWith("STATE"))
                {
                    if (response.Contains("OPEN")) coverState = CoverStatus.Open;
                    else if (response.Contains("CLOSED")) coverState = CoverStatus.Closed;
                    else if (response.Contains("MOVING")) coverState = CoverStatus.Moving;
                }
                else if (response.StartsWith("BRIGHTNESS"))
                {
                    string[] parts = response.Split(' ');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int brightness))
                    {
                        brightnessLevel = brightness;
                        calibratorState = brightness > 0 ? CalibratorStatus.Ready : CalibratorStatus.Off;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage("SerialPort_DataReceived", $"Exception: {ex.Message}");
            }
        }
    }
}
