using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace AdaptiveStarter
{
    internal sealed class CalculatePlanRequest
    {
        public string PatientId { get; set; }
        public string CourseId { get; set; }
        public string PlanId { get; set; }

        // If true, uses CalculateLeafMotionsAndDose for STATIC beams when needed
        public bool UseLeafMotionsForStaticImrt { get; set; } = true;
    }

    internal sealed class CalculatePlanResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public string CalculationType { get; set; }   // "CalculateDose" / "CalculateLeafMotionsAndDose" / "CalculateDoseWithPresetValues"
        public string Details { get; set; }           // anything useful for debugging (messages/log notes)
    }

    internal sealed class CalculatePlanRunner
    {
        public CalculatePlanResult Run(VMS.TPS.Common.Model.API.Application app, CalculatePlanRequest req)
        {
            var result = new CalculatePlanResult();

            try
            {
                try { app.ClosePatient(); } catch { }

                var patient = app.OpenPatientById(req.PatientId)
                              ?? throw new Exception($"Patient not found: {req.PatientId}");

                Log.Info($"Opened patient {patient.Id} for calculation");
                patient.BeginModifications();

                var course = patient.Courses.FirstOrDefault(c => c.Id.Equals(req.CourseId, StringComparison.OrdinalIgnoreCase))
                            ?? throw new Exception($"Course not found: {req.CourseId}");

                var plan = course.ExternalPlanSetups.FirstOrDefault(p => p.Id.Equals(req.PlanId, StringComparison.OrdinalIgnoreCase))
                           ?? throw new Exception($"Plan not found: {req.PlanId}");

                Log.Info($"Calculating plan {course.Id}/{plan.Id}");

                var firstTxBeam = plan.Beams.FirstOrDefault(b => !b.IsSetupField)
                                  ?? throw new Exception("Plan has no non-setup treatment beams.");

                var techniqueId = SafeTechniqueId(firstTxBeam);
                Log.Info($"Technique (first tx beam): {techniqueId}");

                CalculationResult calc;

                if (techniqueId.Equals("STATIC", StringComparison.OrdinalIgnoreCase) && req.UseLeafMotionsForStaticImrt)
                {
                    result.CalculationType = "CalculateLeafMotionsAndDose";
                    Log.Info("Using CalculateLeafMotionsAndDose()");
                    calc = plan.CalculateLeafMotionsAndDose();
                }
                else
                {
                    result.CalculationType = "CalculateDose";
                    Log.Info("Using CalculateDose()");
                    calc = plan.CalculateDose();
                }

                // Robust diagnostics (works even if ESAPI type changes)
                result.Details = DescribeCalculationResult(calc);
                Log.Info("Calc result:\n" + result.Details);

                if (!calc.Success)
                    throw new Exception("Dose calculation returned Success=false (see details above).");

                app.SaveModifications();
                Log.Info("Saved modifications after calculation.");

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static string SafeTechniqueId(Beam b)
        {
            try { return b.Technique?.Id ?? ""; } catch { return ""; }
        }

        private static string DescribeCalculationResult(CalculationResult calc)
        {
            if (calc == null) return "CalculationResult: <null>";

            var sb = new StringBuilder();
            sb.AppendLine($"Type: {calc.GetType().FullName}");
            sb.AppendLine($"Success: {calc.Success}");

            // calc.ToString() sometimes contains useful info
            try { sb.AppendLine("ToString: " + calc.ToString()); } catch { }

            // Dump all public properties safely so we don't guess member names
            var props = calc.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .OrderBy(p => p.Name);

            foreach (var p in props)
            {
                if (!p.CanRead) continue;

                object value = null;
                try { value = p.GetValue(calc, null); }
                catch (Exception ex) { value = "<error reading: " + ex.Message + ">"; }

                sb.AppendLine($"{p.Name}: {FormatValue(value)}");
            }

            return sb.ToString();
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "<null>";
            if (value is string s) return s;

            // Simple formatting for common types
            if (value is DateTime dt) return dt.ToString("o");
            if (value is bool b) return b ? "true" : "false";

            return value.ToString();
        }
    }
}

