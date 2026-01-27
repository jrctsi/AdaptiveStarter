using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AdaptiveStarter
{
    internal sealed class CouchHuInfo
    {
        // Display name from XML, e.g. "Exact IGRT Couch, medium"
        public string ModelName { get; set; }

        // Internal ID for ESAPI, e.g. "Exact_IGRT_Couch_medium"
        public string ModelId { get; set; }
        public double? SurfaceHU { get; set; }
        public double? InteriorHU { get; set; }
        public double? RailHU { get; set; } // many models won’t have rail mappings
    }

    internal static class CouchModelXml
    {
        public static CouchHuInfo FindByModelName(string couchModelsDir, string modelName)
        {
            if (!Directory.Exists(couchModelsDir))
                throw new DirectoryNotFoundException($"Couch model directory not found: {couchModelsDir}");


            var files = Directory.GetFiles(couchModelsDir, "*.xml", SearchOption.AllDirectories);

            foreach (var f in files)
            {
                XDocument doc;
                try { doc = XDocument.Load(f); }
                catch { continue; }

                var root = doc.Root;
                if (root == null) continue;

                var name = (string)root.Element("ModelName");
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!name.Equals(modelName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Parse mappings
                double? surface = GetHu(root, "CouchSurface");
                double? interior = GetHu(root, "CouchInterior");
                double? rail = GetHu(root, "CouchRail"); // may not exist; leave null

                return new CouchHuInfo
                {
                    ModelName = name,
                    ModelId = CouchModelIdHelper.ToInternalId(name),
                    SurfaceHU = surface,
                    InteriorHU = interior,
                    RailHU = rail
                };
            }

            throw new Exception($"Couch model '{modelName}' not found under {couchModelsDir}");
        }

        private static double? GetHu(XElement root, string structureId)
        {
            var mappings = root.Element("StructureHuMappings");
            if (mappings == null) return null;

            var map = mappings.Elements("StructureHuMapping")
                .FirstOrDefault(e => string.Equals((string)e.Attribute("StructureId"), structureId, StringComparison.OrdinalIgnoreCase));

            if (map == null) return null;

            var s = (string)map.Attribute("AssignedHuValue");
            if (double.TryParse(s, out var v)) return v;
            return null;
        }

        public static CouchHuInfo FindByModelId(string couchModelsDir, string modelId)
        {
            if (!Directory.Exists(couchModelsDir))
                throw new DirectoryNotFoundException($"Couch model directory not found: {couchModelsDir}");

            var files = Directory.GetFiles(couchModelsDir, "*.xml", SearchOption.AllDirectories);

            foreach (var f in files)
            {
                XDocument doc;
                try { doc = XDocument.Load(f); }
                catch { continue; }

                var root = doc.Root;
                if (root == null) continue;

                var name = (string)root.Element("ModelName");
                if (string.IsNullOrWhiteSpace(name)) continue;

                var derivedId = CouchModelIdHelper.ToInternalId(name);

                if (!derivedId.Equals(modelId, StringComparison.OrdinalIgnoreCase))
                    continue;

                double? surface = GetHu(root, "CouchSurface");
                double? interior = GetHu(root, "CouchInterior");
                double? rail = GetHu(root, "CouchRail");

                return new CouchHuInfo
                {
                    ModelName = name,
                    ModelId = derivedId,
                    SurfaceHU = surface,
                    InteriorHU = interior,
                    RailHU = rail
                };
            }

            throw new Exception($"Couch model id '{modelId}' not found under {couchModelsDir}");
        }
    }
    internal static class CouchModelIdHelper
    {
        public static string ToInternalId(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Couch display name is empty.");

            var s = displayName.Trim();

            // collapse all whitespace runs to single spaces
            s = string.Join(" ", s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

            // remove punctuation that Eclipse strips
            s = s.Replace(",", "");

            // spaces -> underscores
            return s.Replace(" ", "_");
        }
    }

}