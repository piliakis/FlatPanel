using ASCOM.Utilities;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ASCOM.PrometheusAstroFlatPanelCover.CoverCalibrator
{
    [ComVisible(false)] // Form not registered for COM!
    public partial class SetupDialogForm : Form
    {
        const string NO_PORTS_MESSAGE = "No COM ports found";
        TraceLogger tl; // Holder for a reference to the driver's trace logger

        public SetupDialogForm(TraceLogger tlDriver)
        {
            InitializeComponent();

            // Save the provided trace logger for use within the setup dialog
            tl = tlDriver;

            // Initialize current values of user settings from the ASCOM Profile
            InitUI();
        }

        private void CmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            // Update the trace log checkbox value
            tl.Enabled = chkTrace.Checked;

            // Update the COM port variable if one has been selected
            if (comboBoxComPort.SelectedItem is null) // No COM port selected
            {
                tl.LogMessage("SetupDialog", "No COM port selected.");
                CoverCalibratorHardware.comPort = null;
            }
            else if (comboBoxComPort.SelectedItem.ToString() == NO_PORTS_MESSAGE)
            {
                tl.LogMessage("SetupDialog", "No COM ports available on this PC.");
                CoverCalibratorHardware.comPort = null;
            }
            else // A valid COM port has been selected
            {
                CoverCalibratorHardware.comPort = comboBoxComPort.SelectedItem.ToString();
                tl.LogMessage("SetupDialog", $"Selected COM Port: {CoverCalibratorHardware.comPort}");
            }

            // Persist the configuration changes to the profile
            CoverCalibratorHardware.WriteProfile();

            Close();
        }

        private void CmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("https://ascom-standards.org/");
            }
            catch (Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void InitUI()
        {
            // Set the trace checkbox to the current value
            chkTrace.Checked = tl.Enabled;

            // Populate the COM port dropdown with available COM ports
            comboBoxComPort.Items.Clear();
            using (Serial serial = new Serial())
            {
                comboBoxComPort.Items.AddRange(serial.AvailableCOMPorts);
            }

            // Add a message if no ports are found
            if (comboBoxComPort.Items.Count == 0)
            {
                comboBoxComPort.Items.Add(NO_PORTS_MESSAGE);
                comboBoxComPort.SelectedItem = NO_PORTS_MESSAGE;
            }
            else
            {
                // Select the current port if it exists in the list
                if (!string.IsNullOrEmpty(CoverCalibratorHardware.comPort) &&
                    comboBoxComPort.Items.Contains(CoverCalibratorHardware.comPort))
                {
                    comboBoxComPort.SelectedItem = CoverCalibratorHardware.comPort;
                }
            }

            // Log the UI initialization
            tl.LogMessage("InitUI", $"Trace: {chkTrace.Checked}, Selected COM Port: {comboBoxComPort.SelectedItem}");
        }

        private void SetupDialogForm_Load(object sender, EventArgs e)
        {
            // Bring the setup dialog to the front of the screen
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            else
            {
                TopMost = true;
                Focus();
                BringToFront();
                TopMost = false;
            }
        }

        private void comboBoxComPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Log changes to the selected COM port
            if (comboBoxComPort.SelectedItem != null)
            {
                tl.LogMessage("COMPort Selected", $"COM Port changed to: {comboBoxComPort.SelectedItem}");
            }
        }
    }
}
