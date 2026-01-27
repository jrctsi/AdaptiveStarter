using System;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;

namespace AdaptiveStarter
{
    internal static class CopyPlan
    {
        public static PlanSetup CopyToCbct(
            VMS.TPS.Common.Model.API.Application app,
            string patientId,
            string sourceCourseId,
            string sourcePlanId,
            string targetCourseId,
            string targetPlanId,
            string targetStructureSetId,
            out string diagnosticsText)
        {
            diagnosticsText = "";

            try { app.ClosePatient(); } catch { }

            var patient = app.OpenPatientById(patientId)
                          ?? throw new Exception($"Patient not found: {patientId}");

            patient.BeginModifications();

            var sourceCourse = patient.Courses.FirstOrDefault(c => c.Id.Equals(sourceCourseId, StringComparison.OrdinalIgnoreCase))
                             ?? throw new Exception($"Source course not found: {sourceCourseId}");

            var sourcePlan = sourceCourse.PlanSetups.FirstOrDefault(p => p.Id.Equals(sourcePlanId, StringComparison.OrdinalIgnoreCase))
                           ?? throw new Exception($"Source plan not found: {sourcePlanId}");

            var ss = patient.StructureSets.FirstOrDefault(s => s.Id.Equals(targetStructureSetId, StringComparison.OrdinalIgnoreCase))
                     ?? throw new Exception($"Target StructureSet not found: {targetStructureSetId}");

            var targetCourse = patient.Courses.FirstOrDefault(c => c.Id.Equals(targetCourseId, StringComparison.OrdinalIgnoreCase));
            if (targetCourse == null)
            {
                targetCourse = patient.AddCourse();
                targetCourse.Id = targetCourseId;
            }

            var diag = new StringBuilder();

            // This is the key ESAPI method you referenced
            var copied = targetCourse.CopyPlanSetup(sourcePlan, ss, diag);

            diagnosticsText = diag.ToString();

            // Enforce/adjust plan ID after copy
            copied.Id = MakeUniquePlanId(targetCourse, targetPlanId);

            // Keep it simple and short
            copied.Comment = Truncate($"Copied from {sourceCourse.Id}/{sourcePlan.Id} onto SS {ss.Id} at {DateTime.Now:g}", 254);

            app.SaveModifications();

            return copied;
        }

        private static string MakeUniquePlanId(Course course, string desired)
        {
            var ids = course.PlanSetups.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!ids.Contains(desired)) return desired;

            int i = 1;
            while (ids.Contains(desired + i)) i++;
            return desired + i;
        }

        private static string Truncate(string s, int max) => (s.Length <= max) ? s : s.Substring(0, max);
    }

    internal sealed class PlanCopyRequest
    {
        public string PatientId { get; set; }
        public string SourceCourseId { get; set; }
        public string SourcePlanId { get; set; }

        public string TargetCourseId { get; set; }
        public string TargetPlanId { get; set; }
        public string TargetStructureSetId { get; set; }
    }

    internal sealed class PlanCopyResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public string CreatedCourseId { get; set; }
        public string CreatedPlanId { get; set; }

        // Whatever ESAPI says about the copy process
        public string Diagnostics { get; set; }
    }

    internal sealed class PlanCopyRunner
    {
        public PlanCopyResult Run(VMS.TPS.Common.Model.API.Application app, PlanCopyRequest req)
        {
            var result = new PlanCopyResult();

            try
            {
                var copied = CopyPlan.CopyToCbct(
                    app,
                    req.PatientId,
                    req.SourceCourseId,
                    req.SourcePlanId,
                    req.TargetCourseId,
                    req.TargetPlanId,
                    req.TargetStructureSetId,
                    out var diagnostics);

                result.Success = true;
                result.CreatedCourseId = copied.Course.Id;
                result.CreatedPlanId = copied.Id;
                result.Diagnostics = diagnostics;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }
}