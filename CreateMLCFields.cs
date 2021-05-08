//////////////////////////////////////////////////////////////////////
///Class and functions to create the mlc beams
/// Include functions:
///     CreateMLCFields(ExternalPlanSetup, ExternalBeamMachineParameters, ImageFactory, picWidth, picLength) -- class initiation
///     getRotateAngles() -- compute collimator rotation angles
///     createField(rotAng) -- create the MLC field, will call convertImgToArr, createFluenceMatrix & createMLCField
///     createFluenceMatrix(imgArr, window lv) -- create fluence from 2D array
///     createMlcField(flncMtx, rotAng) -- create the MLC field from fluence matrix and collimator angle
///     computeDose() -- compute dose
///     convertImgToArr(Bitmap img) -- convert image to 2D array
///  
///--version 1.0.0.1
///Becket Hui 2020/05
//////////////////////////////////////////////////////////////////////
using ImageProcessor;
using ImageProcessor.Imaging.Filters.Photo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace createMLCPicture
{
    class CreateMLCFields
    {
        private static ExternalPlanSetup currPln;
        private static ExternalBeamMachineParameters currMachParam;
        private static LMCVOptions vlmcOpt = new LMCVOptions(false);  
        private ImageFactory imgFcty;
        private double flncRes = 2.5; // fluence resolution in mm
        public double maxGy = 0.5; // max dose at 5 cm depth for each field
        public double pddFctr = 0.75; // conversion factor to convert dose from 5 cm depth to 10 cm depth
        public int xDim, yDim; // pixel dimensions of the fluence map
        public CreateMLCFields(ExternalPlanSetup pln, ExternalBeamMachineParameters machParam, ImageFactory img, double picWidth, double picLength)
        {
            currPln = pln;
            currMachParam = machParam;
            imgFcty = img;
            // Resize image based on input pic length and width //
            double nPxWidth = Math.Floor(picWidth / flncRes);  // number of 2.5mm pixels in planned picture width
            double nPxLength = Math.Floor(picLength / flncRes);  // number of 2.5mm pixels in planned picture length
            double mag = Math.Min(nPxWidth / imgFcty.Image.Width, nPxLength / imgFcty.Image.Height);
            xDim = (int)Math.Floor(mag * imgFcty.Image.Width);
            yDim = (int)Math.Floor(mag * imgFcty.Image.Height);
            // Remove all existing beams //
            int nBeams = currPln.Beams.Count();
            for (int i = 0; i < nBeams; i++)
            {
                Beam currBm = currPln.Beams.First();
                currPln.RemoveBeam(currBm);
            }
        }
        public float[] getRotateAngles()
        {
            // Compute the 4 collimator angles to compute the MLC fluence fields //
            float[] collAngs = new float[4] { 0, 30, 60, 90 };  // ideal case where MLC can accomodate all these 4 angles
            Double maxMLCDim = 315.0;
            Double picXDim = (double)xDim * flncRes;
            Double picYDim = (double)yDim * flncRes;
            Double r = Math.Sqrt((picXDim * picXDim) + (picYDim * picYDim));
            if (maxMLCDim < r)  // This mean MLC cannot create fluence that accomodates all collimator angles
            {
                // Compute max angle to rotate //                
                Double maxAng = (180 / Math.PI) * (-Math.Acos(maxMLCDim / r) + Math.Atan2(Math.Max(picXDim, picYDim), Math.Min(picXDim, picYDim)));
                maxAng = Math.Min(maxAng, 30.0);
                if (picXDim <= picYDim)  // vertical length is longer than horizontal width //
                {
                    if (picYDim > maxMLCDim)
                    {
                        collAngs[0] = 360 - (float)Math.Floor(maxAng);
                        collAngs[1] = 360 - (float)Math.Floor(maxAng / 2);
                        collAngs[2] = (float)Math.Floor(maxAng / 2);
                        collAngs[3] = (float)Math.Floor(maxAng);
                    }
                    else
                    {
                        collAngs[0] = 360 - (float)Math.Floor(maxAng);
                        collAngs[1] = 0;
                        collAngs[2] = (float)Math.Floor(maxAng);
                        collAngs[3] = 90;
                    }
                }
                else // horizontal width is longer than vertical length //
                {
                    if (picXDim > maxMLCDim)
                    {
                        collAngs[0] = 90 - (float)Math.Floor(maxAng);
                        collAngs[1] = 90 - (float)Math.Floor(maxAng / 2);
                        collAngs[2] = 90 + (float)Math.Floor(maxAng / 2);
                        collAngs[3] = 90 + (float)Math.Floor(maxAng);
                    }
                    else
                    {
                        collAngs[0] = 90 - (float)Math.Floor(maxAng);
                        collAngs[1] = 90;
                        collAngs[2] = 90 + (float)Math.Floor(maxAng);
                        collAngs[3] = 0;
                    }
                }
            }
            return collAngs;
        }
        public void createField(float rotAng)
        {
            // Prepare image to be delivered as flunence //
            imgFcty.Reset();
            imgFcty.Filter(MatrixFilters.GreyScale);  // convert to greyscale
            imgFcty.Resize(new System.Drawing.Size(xDim, yDim));  // resize to xDim x yDim
            imgFcty.Rotate(rotAng).BackgroundColor(System.Drawing.Color.White);
            imgFcty.Filter(MatrixFilters.Invert); // invert pixel values
            int[,] imgArr = convertImgToArr(new Bitmap(imgFcty.Image));
            // Create fluence matrix //
            float[,] flncMtx = createFluenceMatrix(imgArr, 150);
            // Create MLC field //
            createMlcField(flncMtx, rotAng);
        }
        private float[,] createFluenceMatrix(int[,] imgArr, int win)
        {
            int height = imgArr.GetLength(0);
            int width = imgArr.GetLength(1);
            float[,] flncMtx = new float[height, width];
            // Window and leveling, based on the input window level (e.g. 100),
            // shift the highest pixel value (darkest) to the max window level (e.g. 241 -> 100),
            // all the lower pixel values would go to 0 (e.g. 0-141 -> 0)
            int maxVal = imgArr.Cast<int>().Max();
            int minVal = imgArr.Cast<int>().Min();
            int ValShift = win - maxVal;
            double pxToGy = maxGy * pddFctr / win;
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    flncMtx[j, i] = (float)(Math.Max(imgArr[j, i] + ValShift, 0) * pxToGy);
                }
            }
            return flncMtx;
        }
        private void createMlcField(float[,] flncMtx, float rotAng)
        {
            int height = flncMtx.GetLength(0);
            int width = flncMtx.GetLength(1);
            double xOrg = -0.5*((double)width * flncRes - flncRes);
            double yOrg = 0.5*((double)height * flncRes - flncRes);
            // Create fluence
            Fluence mlcFlnc = new Fluence(flncMtx, xOrg, yOrg);
            // Add beam
            Beam currBm = currPln.AddMLCBeam(currMachParam, new float[2, 60], new VRect<double>(-50.0, -50.0, 50.0, 50.0), rotAng, 0.0, 0.0, new VVector(0, 0, 0));
            currBm.SetOptimalFluence(mlcFlnc);
            currPln.CalculateLeafMotions(vlmcOpt);
        }
        public void computeDose()
        {
            // Create prescription //
            currPln.SetPrescription(1, new DoseValue(800.0, DoseValue.DoseUnit.cGy), 1.0);
            // Compute dose //
            currPln.CalculateDose();
        }
        private static int[,] convertImgToArr(Bitmap img)
        {
            int Width = img.Width;
            int Height = img.Height;
            int[,] imgArr = new int[Height, Width];
            // Assign grey scale value to integer //
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    System.Drawing.Color clr = img.GetPixel(i, j);
                    int gs = (int)Convert.ChangeType(clr.R * 0.3 + clr.G * 0.59 + clr.B * 0.11, typeof(int));
                    imgArr[j, i] = gs;
                }
            }
            return imgArr;
        }
    }
}
