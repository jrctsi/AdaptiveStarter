using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using static VMS.TPS.Common.Model.Types.DoseValue;

namespace AdaptiveStarter
{
    //MCB
    public static class RapidPlan
    {
        // static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public static class Add
        {
            /// <summary>
            /// Applies Dose Volume Histogram (DVH) estimates to an external plan setup using a specified RapidPlan model.
            /// </summary>
            /// <param name="plan">The external plan setup to which DVH estimates will be applied.</param>
            /// <param name="RapidPlanModelId">The ID of the RapidPlan model to use for DVH estimates.</param>
            /// <param name="TargetDoseLevels">A list of target structures and their assigned dose levels.</param>
            /// <param name="StructureMatches">A list of structure matches between the plan and RapidPlan model structures.</param>
            /// <returns>
            /// True if DVH estimates are successfully applied; otherwise, false if any errors occur or setup/imaging beams are missing.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown if the plan, RapidPlanModelId, TargetDoseLevels, or StructureMatches are null.</exception>
            /// <exception cref="InvalidOperationException">Thrown if the dose unit cannot be retrieved or if DVH calculation fails.</exception>
            public static bool Apply(
                ExternalPlanSetup plan,
                string RapidPlanModelId,
                List<(string StructureId, double AssignedDose)> TargetDoseLevels,
                List<(string StructureId, string RapidPlanStructureId)> StructureMatches)
            {
                // Validate input parameters
                if (plan == null)
                    throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");
                if (string.IsNullOrWhiteSpace(RapidPlanModelId))
                    throw new ArgumentNullException(nameof(RapidPlanModelId), "RapidPlanModelId cannot be null or empty.");
                if (TargetDoseLevels == null || TargetDoseLevels.Count == 0)
                    throw new ArgumentNullException(nameof(TargetDoseLevels), "TargetDoseLevels cannot be null or empty.");
                if (StructureMatches == null || StructureMatches.Count == 0)
                    throw new ArgumentNullException(nameof(StructureMatches), "StructureMatches cannot be null or empty.");

                // Retrieve the dose unit
                DoseUnit doseUnit;
                try
                {
                    //Because the plan must have a define prescription before using rapidplan,
                    //the dose unit from the prescription will be used instead of SetDoseUnit().
                    doseUnit = ESAPI_Helpers.GetDoseUnit(plan);
                }
                catch (Exception ex)
                {
                    //logger.Error($"Error retrieving dose unit: {ex.Message}");
                    throw new InvalidOperationException("Failed to retrieve dose unit for the plan.", ex);
                }

                try
                {
                    // Ensure the plan has treatment beams
                    if (RetrieveItems.GetTreatmentBeams(plan).Count == 0)
                    {
                        //logger.Error("Error: The plan must contain at least one treatment beam.");
                        return false;
                    }

                    // Create dictionaries for target levels and structure matches
                    var targetLevels = new Dictionary<string, DoseValue>();
                    var structureMatches = new Dictionary<string, string>();

                    foreach (var target in TargetDoseLevels)
                    {
                        targetLevels.Add(target.StructureId, new DoseValue(target.AssignedDose, doseUnit));
                    }

                    foreach (var match in StructureMatches)
                    {
                        structureMatches.Add(match.StructureId, match.RapidPlanStructureId);
                    }

                    // Calculate DVH estimates
                    CalculationResult dvhCalc;
                    try
                    {
                        dvhCalc = plan.CalculateDVHEstimates(RapidPlanModelId, targetLevels, structureMatches);
                    }
                    catch (Exception ex)
                    {
                        //logger.Error($"Error calculating DVH estimates: {ex.Message}");
                        throw new InvalidOperationException("DVH calculation failed.", ex);
                    }

                    // Check if calculation result is valid
                    if (dvhCalc == null || !dvhCalc.Success)
                    {
                        //logger.Error("Error: DVH calculation was unsuccessful.");
                        return false;
                    }

                    return true; // DVH estimates successfully applied
                }
                catch (Exception ex)
                {
                    //logger.Error($"Unexpected error during DVH estimate application: {ex.Message}");
                    throw new InvalidOperationException("An unexpected error occurred during DVH estimate application.", ex);
                }
            }

            /// <summary>
            /// Applies the RapidPlan DVH estimation using the provided inputs, after validating the inputs.
            /// </summary>
            /// <param name="esapi">The ESAPI application instance for validation purposes.</param>
            /// <param name="plan">The external plan setup to which the DVH estimation will be applied.</param>
            /// <param name="RapidPlanModelId">The RapidPlan model ID to use for DVH estimation.</param>
            /// <param name="TargetDoseLevels">A list of target structures and their assigned dose levels.</param>
            /// <param name="StructureMatches">A list of structure matches between the plan and RapidPlan model structures.</param>
            /// <returns>
            /// True if the DVH estimation is successfully applied; otherwise, false if validation fails or an error occurs.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown if any required parameter is null.</exception>
            /// <exception cref="InvalidOperationException">Thrown if validation fails or if an error occurs during the operation.</exception>
            public static bool Apply(
                VMS.TPS.Common.Model.API.Application esapi,
                ExternalPlanSetup plan,
                string RapidPlanModelId,
                List<(string StructureId, double AssignedDose)> TargetDoseLevels,
                List<(string StructureId, string RapidPlanStructureId)> StructureMatches)
            {
                // Validate input parameters
                if (esapi == null)
                    throw new ArgumentNullException(nameof(esapi), "ESAPI application instance cannot be null.");
                if (plan == null)
                    throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");
                if (string.IsNullOrWhiteSpace(RapidPlanModelId))
                    throw new ArgumentNullException(nameof(RapidPlanModelId), "RapidPlanModelId cannot be null or empty.");
                if (TargetDoseLevels == null)
                    throw new ArgumentNullException(nameof(TargetDoseLevels), "TargetDoseLevels cannot be null.");
                if (StructureMatches == null)
                    throw new ArgumentNullException(nameof(StructureMatches), "StructureMatches cannot be null.");

                try
                {
                    // Validate RapidPlan inputs using the ESAPI instance
                    if (!Validation.ValidateRapidPlanInputs(esapi, plan, RapidPlanModelId, TargetDoseLevels, StructureMatches))
                    {
                        //logger.Error("Error: RapidPlan inputs validation failed.");
                        return false;
                    }

                    // Apply DVH estimation logic (delegating to the other overload)
                    return Apply(plan, RapidPlanModelId, TargetDoseLevels, StructureMatches);
                }
                catch (Exception ex)
                {
                    //logger.Error($"Unexpected error during RapidPlan application: {ex.Message}");
                    throw new InvalidOperationException("An error occurred during the RapidPlan application process.", ex);
                }
            }

            /// <summary>
            /// Applies a RapidPlan model to the specified plan using a serialized <see cref="DHO_RapidPlanSetup"/> configuration.
            /// </summary>
            /// <param name="esapi">The ESAPI application instance.</param>
            /// <param name="plan">The plan to which the RapidPlan model will be applied.</param>
            /// <param name="dhoRapidPlanSetup">The serialized RapidPlan setup configuration.</param>
            /// <returns>Returns <c>true</c> if the RapidPlan model was successfully applied; otherwise, <c>false</c>.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown if <paramref name="esapi"/>, <paramref name="plan"/>, or <paramref name="dhoRapidPlanSetup"/> is null.
            /// </exception>
            public static bool Apply(
                VMS.TPS.Common.Model.API.Application esapi,
                ExternalPlanSetup plan,
                DHO_RapidPlanSetup dhoRapidPlanSetup)
            {
                // Validate input parameters
                if (esapi == null)
                    throw new ArgumentNullException(nameof(esapi), "The ESAPI application instance cannot be null.");

                if (plan == null)
                    throw new ArgumentNullException(nameof(plan), "The plan to apply RapidPlan to cannot be null.");

                if (dhoRapidPlanSetup == null)
                    throw new ArgumentNullException(nameof(dhoRapidPlanSetup), "RapidPlan setup data cannot be null.");

                var targetLevels = new List<(string StructureId, double AssignedDose)>();
                var structureMatches = new List<(string StructureId, string RapidPlanStructureId)>();

                // Translate DHO target levels to tuple format
                foreach (var tdl in dhoRapidPlanSetup.TargetLevels)
                {
                    if (string.IsNullOrWhiteSpace(tdl.TargetId))
                        continue; // Skip invalid entries

                    targetLevels.Add((tdl.TargetId, tdl.Dose));
                }

                // Translate DHO structure matches to tuple format
                foreach (var sm in dhoRapidPlanSetup.StructureMatches)
                {
                    if (string.IsNullOrWhiteSpace(sm.StructureId) || string.IsNullOrWhiteSpace(sm.RapidPlanStructureId))
                        continue; // Skip invalid matches

                    structureMatches.Add((sm.StructureId, sm.RapidPlanStructureId));
                }

                // Delegate to the existing Apply method
                return Apply(esapi, plan, dhoRapidPlanSetup.RapidPlanModelId, targetLevels, structureMatches);
            }


            /// <summary>
            /// Copies the RapidPlan DVH estimation configuration from a source plan and applies it to a target plan.
            /// <para>
            /// This method extracts the RapidPlan model ID, structure mappings, and target dose levels
            /// from the source plan (using calculation logs only), then applies the same configuration
            /// to the target plan via DVH estimation.
            /// </para>
            /// </summary>
            /// <param name="sourcePlan">
            /// The source <see cref="ExternalPlanSetup"/> from which the RapidPlan configuration
            /// (model ID, targets, and structure matches) will be extracted.
            /// </param>
            /// <param name="targetPlan">
            /// The destination <see cref="ExternalPlanSetup"/> to which the RapidPlan configuration
            /// will be applied.
            /// </param>
            /// <returns>
            /// <c>true</c> if the RapidPlan configuration was successfully extracted and applied;
            /// otherwise, <c>false</c> if extraction fails, required data is missing,
            /// or DVH estimation cannot be applied.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown if <paramref name="sourcePlan"/> or <paramref name="targetPlan"/> is null.
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// Thrown if an unexpected error occurs during extraction or application.
            /// </exception>
            public static bool CopyRapidPlanFromSourceToTargetPlan(
                ExternalPlanSetup sourcePlan,
                ExternalPlanSetup targetPlan)
            {
                // ---- Basic argument validation ----
                if (sourcePlan == null)
                    throw new ArgumentNullException(nameof(sourcePlan), "Source plan cannot be null.");

                if (targetPlan == null)
                    throw new ArgumentNullException(nameof(targetPlan), "Target plan cannot be null.");

                try
                {
                    // ---- Extract RapidPlan configuration from source plan ----
                    var sourceResults = Retrieval.Extract(sourcePlan);

                    // Validate extracted results
                    if (sourceResults == null)
                    {
                        // logger.Warn("RapidPlan extraction returned null.");
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(sourceResults.RapidPlanModelId))
                    {
                        // logger.Warn("Source plan does not contain a RapidPlan model ID.");
                        return false;
                    }

                    if (sourceResults.StructureMatches == null || sourceResults.StructureMatches.Count == 0)
                    {
                        // logger.Warn("No structure matches were extracted from the source plan.");
                        return false;
                    }

                    // Target levels may legitimately be empty (log-based extraction),
                    // so we do NOT fail if TargetLevels is empty.

                    // ---- Apply extracted RapidPlan configuration to target plan ----
                    return Apply(
                        targetPlan,
                        sourceResults.RapidPlanModelId,
                        sourceResults.TargetLevels?
                            .Select(t => (t.TargetId, t.Dose))
                            .ToList()
                            ?? new List<(string StructureId, double AssignedDose)>(),
                        sourceResults.StructureMatches
                            .Select(s => (s.StructureId, s.RapidPlanStructureId))
                            .ToList()
                    );
                }
                catch (Exception ex)
                {
                    // logger.Error(ex, "Error copying RapidPlan configuration from source to target plan.");
                    throw new InvalidOperationException(
                        "An error occurred while copying RapidPlan configuration from the source plan to the target plan.",
                        ex);
                }
            }

        }

        public static class Validation
        {
            /// <summary>
            /// Validates the inputs for a RapidPlan DVH estimation operation.
            /// </summary>
            /// <param name="esapi">The ESAPI application instance.</param>
            /// <param name="plan">The external plan setup being validated.</param>
            /// <param name="RapidPlanModelId">The RapidPlan model ID to validate.</param>
            /// <param name="TargetDoseLevels">A list of target structures and their assigned dose levels.</param>
            /// <param name="StructureMatches">A list of structure matches between the plan and RapidPlan model structures.</param>
            /// <returns>
            /// True if all inputs are valid; otherwise, false. Detailed error messages are logged for any failures.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown if any required parameter is null.</exception>
            /// <exception cref="InvalidOperationException">Thrown if no matching RapidPlan model is found or an error occurs during validation.</exception>
            public static bool ValidateRapidPlanInputs(
                VMS.TPS.Common.Model.API.Application esapi,
                ExternalPlanSetup plan,
                string RapidPlanModelId,
                List<(string StructureId, double AssignedDose)> TargetDoseLevels,
                List<(string StructureId, string RapidPlanStructureId)> StructureMatches)
            {
                // Validate input parameters
                if (esapi == null)
                    throw new ArgumentNullException(nameof(esapi), "ESAPI application instance cannot be null.");
                if (plan == null)
                    throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");
                if (string.IsNullOrWhiteSpace(RapidPlanModelId))
                    throw new ArgumentNullException(nameof(RapidPlanModelId), "RapidPlanModelId cannot be null or empty.");
                if (TargetDoseLevels == null)
                    throw new ArgumentNullException(nameof(TargetDoseLevels), "TargetDoseLevels cannot be null.");
                if (StructureMatches == null)
                    throw new ArgumentNullException(nameof(StructureMatches), "StructureMatches cannot be null.");

                try
                {
                    // Validate that the plan has a structure set
                    if (plan.StructureSet == null)
                    {
                        //logger.Error("Error: Plan does not have an associated structure set.");
                        return false;
                    }

                    // Validate that all structure matches exist in the plan's structure set
                    foreach (var structureId in StructureMatches.Select(x => x.StructureId).Concat(TargetDoseLevels.Select(x => x.StructureId)))
                    {
                        if (!plan.StructureSet.Structures.Select(x => x.Id).Contains(structureId))
                        {
                            //logger.Error($"Error: Structure '{structureId}' is not found in the plan's structure set.");
                            return false;
                        }
                    }

                    // Retrieve available RapidPlan models and validate the specified model
                    var availableModels = esapi.Calculation.GetDvhEstimationModelSummaries();
                    var RapidPlanModel = availableModels.SingleOrDefault(x => x.Name.Equals(RapidPlanModelId, StringComparison.OrdinalIgnoreCase));

                    if (RapidPlanModel == null)
                    {
                        //logger.Error($"Error: RapidPlan model with ID '{RapidPlanModelId}' is not available.");
                        return false;
                    }

                    // Validate that all structure matches exist in the RapidPlan model's structures
                    var modelStructures = esapi.Calculation.GetDvhEstimationModelStructures(RapidPlanModel.ModelUID).Select(x => x.Id);
                    foreach (var match in StructureMatches)
                    {
                        if (!modelStructures.Contains(match.RapidPlanStructureId))
                        {
                            //logger.Error($"Error: Structure '{match.RapidPlanStructureId}' is not found in the RapidPlan model '{RapidPlanModelId}'.");
                            return false;
                        }
                    }

                    return true; // All validations passed
                }
                catch (Exception ex)
                {
                    //logger.Error($"Unexpected error during RapidPlan input validation: {ex.Message}");
                    throw new InvalidOperationException("An error occurred during RapidPlan input validation.", ex);
                }
            }
        }

        public static class Retrieval
        {
            /// <summary>
            /// Extracts RapidPlan model details, target dose levels, and structure matches from the specified plan,
            /// reading only from the calculation logs (without relying on DVH estimates).
            /// </summary>
            /// <param name="plan">The external plan setup to analyze.</param>
            /// <returns>
            /// A tuple containing the RapidPlan model ID, target dose levels (if available), and structure matches.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown if the plan is null.</exception>
            /// <exception cref="InvalidOperationException">Thrown if an error occurs during extraction.</exception>
            public static DHO_RapidPlanSetup

                //(string RapidPlanModelId,
                //List<(string TargetId, double Dose)> TargetLevels,
                //List<(string StructureId, string RapidPlanStructureId)> StructureMatches)
                Extract(ExternalPlanSetup plan)
            {
                if (plan == null)
                    throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");

                try
                {
                    // Validate plan and extract data if valid
                    if (!IsPlanValid(plan))
                    {
                        //logger.Warn("Plan is not valid for DVH Estimation extraction.");
                        return new DHO_RapidPlanSetup();
                    }

                    return new DHO_RapidPlanSetup(ExtractDataFromPlan(plan));
                }
                catch (Exception ex)
                {
                    //logger.Error($"Unexpected error during data extraction: {ex.Message}");
                    throw new InvalidOperationException("An error occurred during data extraction.", ex);
                }
            }

            /// <summary>
            /// Validates whether the plan is eligible for extracting calculation log information.
            /// </summary>
            /// <param name="plan">The external plan setup to validate.</param>
            /// <returns>True if the plan is valid; otherwise, false.</returns>
            public static bool IsPlanValid(ExternalPlanSetup plan)
            {
                var txBeams = RetrieveItems.GetTreatmentBeams(plan);
                // Check if there are any treatment beams.
                if (txBeams.Count == 0)
                {
                    return false;
                }

                // Check if the first treatment beam has any calculation logs containing "DVH Estimation"
                var firstTxBeam = txBeams.First();
                var calcLogs = firstTxBeam.CalculationLogs;
                if (!calcLogs.Any(x => x.Category.ToUpper().Contains("DVH ESTIMATION")))
                {
                    return false;
                }
                return true;
            }

            /// <summary>
            /// Extracts RapidPlan model details, target dose levels, and structure matches from a valid plan,
            /// by parsing only the calculation logs.
            /// </summary>
            /// <param name="plan">The external plan setup to analyze.</param>
            /// <returns>
            /// A tuple containing the RapidPlan model ID, target dose levels (if any were parsed from logs),
            /// and structure matches.
            /// </returns>
            private static (string RapidPlanModelId, List<(string TargetId, double Dose)> TargetLevels, List<(string StructureId, string RapidPlanStructureId)> StructureMatches)
                ExtractDataFromPlan(ExternalPlanSetup plan)
            {
                string rapidPlanModelId = null;
                var targetLevels = new List<(string TargetId, double Dose)>();
                var structureMatches = new List<(string StructureId, string RapidPlanStructureId)>();

                try
                {
                    var beam = RetrieveItems.GetTreatmentBeams(plan).First();
                    if (beam == null)
                    {
                        //logger.Warn("No treatment beam found.");
                        return (null, targetLevels, structureMatches);
                    }

                    // Extract RapidPlan model ID from the logs.
                    rapidPlanModelId = GetRapidPlanModelIdFromLogs(beam);

                    // Extract structure mapping text matches from the logs.
                    var textMatches = ExtractTextMatchesFromLogs(beam);

                    // Loop through each match from the logs.
                    foreach (var m in textMatches)
                    {
                        string[] pair = m.Split(':');
                        if (pair.Length < 2)
                        {
                            //logger.Warn($"Skipping invalid match: {m}");
                            continue;
                        }
                        string structureId = pair[0];
                        string modelStructureId = pair[1];

                        // Since DVH estimates might be null, we do not attempt to extract target dose levels.
                        // You could add extra parsing here if target dose levels are available in the logs.
                        structureMatches.Add((structureId, modelStructureId));
                    }

                    return (rapidPlanModelId, targetLevels.Distinct().ToList(), structureMatches.Distinct().ToList());
                }
                catch (Exception ex)
                {
                    //logger.Error($"Error while extracting data from the plan: {ex.Message}");
                    throw;
                }
            }

            /// <summary>
            /// Extracts the RapidPlan model ID from the calculation logs of a beam.
            /// </summary>
            /// <param name="beam">The beam from which to extract the RapidPlan model ID.</param>
            /// <returns>
            /// The RapidPlan model ID if found; otherwise, null.
            /// </returns>
            public static string GetRapidPlanModelIdFromLogs(Beam beam)
            {
                if (beam == null)
                    throw new ArgumentNullException(nameof(beam), "Beam cannot be null.");

                foreach (var log in beam.CalculationLogs.Where(x => x.Category.Equals("DVH Estimation", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var ml in log.MessageLines)
                    {
                        if (ml.Contains("DVH Estimates generated using model"))
                        {
                            string pattern = "'(.*?)'";
                            Match match = Regex.Match(ml, pattern);
                            if (match.Success)
                            {
                                var modelId = match.Groups[1].Value;
                                //logger.Info($"Extracted RapidPlan Model ID: {modelId}");
                                return modelId;
                            }
                        }
                    }
                }

                //logger.Warn("No RapidPlan Model ID found in the beam logs.");
                return null;
            }

            /// <summary>
            /// Extracts text matches from the calculation logs of a beam that indicate structure mappings.
            /// </summary>
            /// <param name="beam">The treatment beam to analyze.</param>
            /// <returns>A list of text matches describing structure mappings.</returns>
            private static List<string> ExtractTextMatchesFromLogs(Beam beam)
            {
                var textMatches = new List<string>();

                foreach (var log in beam.CalculationLogs.Where(x => x.Category.Equals("DVH Estimation", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var ml in log.MessageLines)
                    {
                        if (ml.Contains("matched to model structure"))
                        {
                            textMatches.Add(ExtractMatch(ml));
                        }
                    }
                }

                return textMatches;
            }

            /// <summary>
            /// Extracts the structure and model structure mapping from a log message.
            /// </summary>
            /// <param name="logMessage">The log message containing structure mapping information.</param>
            /// <returns>A formatted string representing the structure mapping.</returns>
            private static string ExtractMatch(string logMessage)
            {
                string pattern = @"([^\:]+): matched to model structure (.+?)\.?$";
                Match match = Regex.Match(logMessage, pattern);

                if (match.Success && match.Groups.Count == 3)
                {
                    string structure = match.Groups[1].Value.Trim();
                    string modelStructure = match.Groups[2].Value.Trim().TrimEnd('.');
                    return $"{structure}:{modelStructure}";
                }

                return "No match found";
            }

        }


        /// <summary>
        /// Represents the full configuration required to apply a RapidPlan model,
        /// including the model ID, target dose levels, and structure matching.
        /// </summary>
        public class DHO_RapidPlanSetup
        {
            public string RapidPlanModelId { get; set; }
            public List<TargetLevel> TargetLevels { get; set; } = new List<TargetLevel>();
            public List<StructureMatch> StructureMatches { get; set; } = new List<StructureMatch>();


            public DHO_RapidPlanSetup()
            {

            }

            /// <summary>
            /// Initializes a new instance of the <see cref="RapidPlanSetup"/> class
            /// from a tuple-based representation.
            /// </summary>
            /// <param name="tuple">A tuple containing the model ID, target levels, and structure matches.</param>
            public DHO_RapidPlanSetup(
                (string RapidPlanModelId,
                 List<(string TargetId, double Dose)> TargetLevels,
                 List<(string StructureId, string RapidPlanStructureId)> StructureMatches) tuple)
            {
                if (tuple.RapidPlanModelId == null)
                    throw new ArgumentNullException(nameof(tuple.RapidPlanModelId), "Model ID cannot be null.");

                RapidPlanModelId = tuple.RapidPlanModelId;

                if (tuple.TargetLevels != null)
                {
                    TargetLevels = tuple.TargetLevels
                        .Select(t => new TargetLevel { TargetId = t.TargetId, Dose = t.Dose })
                        .ToList();
                }

                if (tuple.StructureMatches != null)
                {
                    StructureMatches = tuple.StructureMatches
                        .Select(s => new StructureMatch
                        {
                            StructureId = s.StructureId,
                            RapidPlanStructureId = s.RapidPlanStructureId
                        })
                        .ToList();
                }
            }
        }

        /// <summary>
        /// Represents a target structure with an associated prescribed dose.
        /// </summary>
        public class TargetLevel
        {
            public string TargetId { get; set; }
            public double Dose { get; set; }
        }

        /// <summary>
        /// Represents a structure mapping between the local structure and the RapidPlan model structure.
        /// </summary>
        public class StructureMatch
        {
            public string StructureId { get; set; }
            public string RapidPlanStructureId { get; set; }
        }

    }
}