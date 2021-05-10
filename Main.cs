//////////////////////////////////////////////////////////////////////
///This program creates the sliding window MLC fields that deliver a dose pattern of the input picture//
///It contains a user control window that browse the picture file, as well as setting the output image dimension at the iso center plane.
///It creates four beams with different collimator angles, attaches the fluence, computes the MLC pattern and dose.
///
///--version 1.0.0.2
///Set beams' iso center to iso center of the first beam, instead of (0,0,0)
///Becket Hui 2020/05
///
///--version 1.0.0.1
///Becket Hui 2021/05
//////////////////////////////////////////////////////////////////////
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.2")]
[assembly: AssemblyFileVersion("1.0.0.2")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context, Window MainWin)
        {
            // Open current patient
            Patient currPt = context.Patient;
            // If there's no selected patient, throw an exception
            if (currPt == null)
                throw new ApplicationException("Please open a patient before using this script.");
            currPt.BeginModifications();

            // Open current course
            Course currCrs = context.Course;
            // If there's no selected course, throw an exception
            if (currCrs == null)
                throw new ApplicationException("Please select at least one course before using this script.");

            // Open current plan
            ExternalPlanSetup currPln = context.ExternalPlanSetup;
            // If there's no selected plan, throw an exception
            if (currPln == null)
                throw new ApplicationException("Please creat a plan with one beam with the preferred machine and energy.");

            // Check if plan is approved
            if (currPln.ApprovalStatus != PlanSetupApprovalStatus.UnApproved)
                throw new ApplicationException("Please unapprove plan before using this script.");

            // Open beam
            Beam currBm = currPln.Beams.FirstOrDefault();
            if (currBm == null)
                throw new ApplicationException("Please insert one beam with the preferred machine and energy.");

            // Call WPF Win
            var MainWinCtr = new createMLCPicture.MainWindow(context);
            MainWin.Content = MainWinCtr;
            MainWin.Title = "Create MLC Picture";
            MainWin.Width = 440;
            MainWin.Height = 240;
            MainWin.ResizeMode = ResizeMode.NoResize;
        }
    }
}
