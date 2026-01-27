using VMS.TPS.Common.Model.Types;
using System;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;

namespace AdaptiveStarter
{
    internal sealed class InsertCouchRequest
    {
        public string PatientId { get; set; }
        public string StructureSetId { get; set; }  // CBCT SS id (planning SS)

        public string CouchModelId { get; set; }    // must match Eclipse couch profile id
        public PatientOrientation Orientation { get; set; } = PatientOrientation.NoOrientation;

        public RailPosition RailA { get; set; } = RailPosition.Out;
        public RailPosition RailB { get; set; } = RailPosition.Out;

        // null => use profile defaults
        public double? SurfaceHU { get; set; } = null;
        public double? InteriorHU { get; set; } = null;
        public double? RailHU { get; set; } = null;
    }

    internal sealed class InsertCouchResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public bool ImageResized { get; set; }
        public string AddedStructureIdsCsv { get; set; }
    }

    internal sealed class InsertCouchRunner
    {
        public InsertCouchResult Run(VMS.TPS.Common.Model.API.Application app, InsertCouchRequest req)
        {
            var result = new InsertCouchResult();

            try
            {
                try { app.ClosePatient(); } catch { }

                var patient = app.OpenPatientById(req.PatientId)
                              ?? throw new Exception($"Patient not found: {req.PatientId}");

                Log.Info($"Opened patient {patient.Id} for InsertCouch");
                patient.BeginModifications();

                var ss = patient.StructureSets.FirstOrDefault(s => s.Id.Equals(req.StructureSetId, StringComparison.OrdinalIgnoreCase))
                         ?? throw new Exception($"StructureSet not found: {req.StructureSetId}");

                // Quick guard: don’t double-add if couch already exists
                // (StructureCode enums vary; simplest is detect common ids/names or VolumeType Support)
                var alreadyHasSupport = ss.Structures.Any(s =>
                {
                    try { return s.DicomType == "SUPPORT"; } catch { return false; }
                });

                if (alreadyHasSupport)
                {
                    Log.Warn("StructureSet already appears to contain SUPPORT structures; skipping AddCouchStructures.");
                    result.Success = true;
                    result.ImageResized = false;
                    result.AddedStructureIdsCsv = "";
                    app.SaveModifications();
                    return result;
                }

                // Check capability (this will catch “calculated SS” etc.)
                if (!ss.CanAddCouchStructures(out var canError))
                    throw new Exception($"Cannot add couch structures: {canError}");

                Log.Info($"Adding couch model '{req.CouchModelId}' to SS '{ss.Id}' (Orientation={req.Orientation}, RailA={req.RailA}, RailB={req.RailB})");

                var ok = ss.AddCouchStructures(
                    req.CouchModelId,
                    req.Orientation,
                    req.RailA,
                    req.RailB,
                    req.SurfaceHU,
                    req.InteriorHU,
                    req.RailHU,
                    out var added,
                    out var imageResized,
                    out var addError);

                if (!ok)
                    throw new Exception($"AddCouchStructures failed: {addError}");

                result.Success = true;
                result.ImageResized = imageResized;
                result.AddedStructureIdsCsv = (added == null || added.Count == 0)
                    ? ""
                    : string.Join(",", added.Select(s => s.Id));

                app.SaveModifications();

                Log.Info($"InsertCouch SUCCESS. ImageResized={imageResized}. Added={result.AddedStructureIdsCsv}");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Log.Error("InsertCouch FAILED: " + ex.Message);
                return result;
            }
        }
    }
}
