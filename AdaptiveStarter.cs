using System;
using System.Diagnostics;
using System.Reflection;
using VMS.TPS.Common.Model.API;

[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]
[assembly: ESAPIScript(IsWriteable = true)]

namespace AdaptiveStarter
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                //var couchModelsDir = @"\\server\va_data$\ProgramData\IRS\CouchModels";
                //var couchModelId = "Exact_IGRT_Couch_Top_medium";

                //var hu = CouchModelXml.FindByModelId(couchModelsDir, couchModelId);

                // Args: PatientId TargetCourseId TargetPlanId TargetStructureSetId SourceCourseId SourcePlanId
                if (args.Length < 6)
                {
                    Console.WriteLine("Usage: AdaptiveStarter.exe <PatientId> <TargetCourseId> <TargetPlanId> <TargetStructureSetId> <SourceCourseId> <SourcePlanId>");
                    return 1;
                }

                var req = new PlanCopyRequest
                {
                    PatientId = args[0],
                    TargetCourseId = args[1],
                    TargetPlanId = args[2],
                    TargetStructureSetId = args[3],
                    SourceCourseId = args[4],
                    SourcePlanId = args[5]
                };

                Log.Info($"Starting PlanCopy: Pt={req.PatientId} Source={req.SourceCourseId}/{req.SourcePlanId} -> Target={req.TargetCourseId}/{req.TargetPlanId} on SS={req.TargetStructureSetId}");

                using (var app = Application.CreateApplication())
                {
                    // -------------------------
                    // Module 1: Plan copy
                    // -------------------------
                    var runner = new PlanCopyRunner();
                    var copyResult = runner.Run(app, req);

                    if (!copyResult.Success)
                    {
                        Log.Error("FAILED: " + copyResult.ErrorMessage);
                        if (!string.IsNullOrWhiteSpace(copyResult.Diagnostics))
                            Log.Error("Copy diagnostics:\n" + copyResult.Diagnostics);

                        return 2;
                    }

                    Log.Info($"SUCCESS: Created {copyResult.CreatedCourseId}/{copyResult.CreatedPlanId}");
                    if (!string.IsNullOrWhiteSpace(copyResult.Diagnostics))
                        Log.Info("Copy diagnostics:\n" + copyResult.Diagnostics);


                    // -------------------------
                    // Module 2: Insert couch
                    // -------------------------
                    var couchReq = new InsertCouchRequest
                    {
                        PatientId = req.PatientId,
                        StructureSetId = req.TargetStructureSetId,

                        CouchModelId = "Exact_IGRT_Couch_Top_medium",
                        Orientation = VMS.TPS.Common.Model.Types.PatientOrientation.NoOrientation,
                        RailA = VMS.TPS.Common.Model.Types.RailPosition.Out,
                        RailB = VMS.TPS.Common.Model.Types.RailPosition.Out,

                        // IMPORTANT: leave HU as null to use Eclipse profile defaults
                        SurfaceHU = null,
                        InteriorHU = null,
                        RailHU = null
                    };

                    var couchRunner = new InsertCouchRunner();
                    var couchResult = couchRunner.Run(app, couchReq);

                    if (!couchResult.Success)
                    {
                        Log.Error("COUCH FAILED: " + couchResult.ErrorMessage);
                        return 3;
                    }

                    Log.Info($"COUCH SUCCESS. ImageResized={couchResult.ImageResized}. Added={couchResult.AddedStructureIdsCsv}");

                    // -------------------------
                    // Module 3: Calculate dose
                    // -------------------------
                    var calcReq = new CalculatePlanRequest
                    {
                        PatientId = req.PatientId,
                        CourseId = copyResult.CreatedCourseId,
                        PlanId = copyResult.CreatedPlanId,
                        UseLeafMotionsForStaticImrt = true
                    };

                    var calcRunner = new CalculatePlanRunner();
                    var calcResult = calcRunner.Run(app, calcReq);

                    if (!calcResult.Success)
                    {
                        Log.Error("CALC FAILED: " + calcResult.ErrorMessage);
                        Log.Error(calcResult.Details ?? "");
                        return 4;
                    }

                    Log.Info("CALC SUCCESS");
                    Log.Info(calcResult.Details ?? "");


                    return 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Fatal: " + ex);
                return 99;
            }
        }
    }

    internal static class Log
    {
        public static void Info(string msg) { Console.WriteLine(msg); Debug.WriteLine(msg); }
        public static void Warn(string msg) { Console.WriteLine("WARN: " + msg); Debug.WriteLine("WARN: " + msg); }
        public static void Error(string msg) { Console.WriteLine("ERROR: " + msg); Debug.WriteLine("ERROR: " + msg); }
    }
}