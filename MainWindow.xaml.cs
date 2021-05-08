//////////////////////////////////////////////////////////////////////
///Main window widget for createMlcFields script//
///
///--version 1.0.0.1
///Becket Hui 2021/05
//////////////////////////////////////////////////////////////////////
using ImageProcessor;
using ImageProcessor.Imaging.Filters.Photo;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Threading;
using System.Threading;

namespace createMLCPicture
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : UserControl
    {
        private ExternalPlanSetup currPln;
        private ExternalBeamMachineParameters currMachParam;
        public MainWindow(ScriptContext context)
        {
            InitializeComponent();
            // Copy machine information from current beam //
            currPln = context.ExternalPlanSetup;
            Beam currBm = currPln.Beams.FirstOrDefault();
            String energy = currBm.EnergyModeDisplayName;
            Match EMode = Regex.Match(currBm.EnergyModeDisplayName, @"^([0-9]+[A-Z]+)-?([A-Z]+)?", RegexOptions.IgnoreCase);  //format is... e.g. 6X(-FFF)
            if (EMode.Success)
            {
                if (EMode.Groups[2].Length > 0)  // fluence mode, this algorithm only takes flattened beam
                {
                    energy = EMode.Groups[1].Value;
                }
            }
            currMachParam = new ExternalBeamMachineParameters(currBm.TreatmentUnit.Id.ToString(), energy, currBm.DoseRate, "STATIC", null);
        }
        //numeric input only for picture height and width
        private void NumberValidation(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !(Regex.IsMatch(e.Text, "^[0-9]+$"));
        }
        //file browser
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openImgDiaglog = new OpenFileDialog
            {
                Title = "Select Image File",

                CheckFileExists = true,
                CheckPathExists = true,

                Filter = "Image files(*.bmp,*.jpg,*.png,*.tiff)|*.bmp;*.jpg;*.png;*.tiff;*.tif|All files(*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true
            };
            if (openImgDiaglog.ShowDialog() == true)
            {
                txtbFilePath.Text = openImgDiaglog.FileName;
                ShowStatusBar("Ready.");
            }
        }
        private void txtbFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            ShowStatusBar("Ready.");
        }
        private void btnCalc_Click(object sender, RoutedEventArgs e)
        {
            // Load image //
            if (String.IsNullOrEmpty(txtbFilePath.Text))
            {
                ShowStatusBar("No file to load.");
                return;
            }
            ImageFactory imgFcty = new ImageFactory();
            try
            {
                imgFcty.Load(txtbFilePath.Text);  //Read image file
            }
            catch (Exception err)
            {
                ShowStatusBar("Cannot load image file.");
            }
            // Check picture dimension //
            double picWidth = Convert.ToDouble(txtbWidth.Text);
            double picLength = Convert.ToDouble(txtbLength.Text);
            if (Math.Min(picWidth, picLength) < 25)
            {
                ShowStatusBar("Min dimension needs to be larger than 2.5 cm.");
                return;
            }
            if (Math.Min(picWidth, picLength) > 320)
            {
                ShowStatusBar("Min dimension needs to be smaller than 32 cm.");
                return;
            }
            if (Math.Max(picWidth, picLength) > 400)
            {
                ShowStatusBar("Max dimension needs to be smaller than 40 cm.");
                return;
            }
            // Create fields for the following collimator angles //
            CreateMLCFields mlcFld = new CreateMLCFields(currPln, currMachParam, imgFcty, picWidth, picLength);
            Single[] collAngs = mlcFld.getRotateAngles();
            foreach (float ang in collAngs)
            {
                ShowStatusBar("Setting " + ang + " deg beam fluence...");
                mlcFld.createField(ang);
            }
            ShowStatusBar("Computing dose...");
            mlcFld.computeDose();
            ShowStatusBar("Complete. Normalize to keep max dose to film below 8 Gy.");
        }
        private void ShowStatusBar(String msg)
        {
            txtbStat.Text = msg;
            // Make status bar update using a new thread
            txtbStat.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate { }));
        }
    }

}
