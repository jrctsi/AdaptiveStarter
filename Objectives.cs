using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using static VMS.TPS.Common.Model.Types.DoseValue;

namespace AdaptiveStarter
{
    //MCB
    public static class Objectives
    {
        /// <summary>
        /// Copies all optimization objectives from a source plan to a target plan.
        /// This method assumes that the patient associated with both plans is the same,
        /// and converts each objective into a <see cref="DynamicObjective"/> for transfer.
        /// </summary>
        /// <param name="sourcePlan">The plan from which objectives will be copied.</param>
        /// <param name="targetPlan">The plan to which objectives will be added.</param>
        /// <returns>
        /// <c>true</c> if all objectives were copied successfully; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="sourcePlan"/> or <paramref name="targetPlan"/> is <c>null</c>.
        /// </exception>
        public static bool CopyObjectivesFromPlanToPlan(ExternalPlanSetup sourcePlan, ExternalPlanSetup targetPlan)
        {
            // Validate inputs
            if (sourcePlan == null)
                throw new ArgumentNullException(nameof(sourcePlan), "Source plan cannot be null.");
            if (targetPlan == null)
                throw new ArgumentNullException(nameof(targetPlan), "Target plan cannot be null.");

            // Ensure both plans belong to the same patient
            if (sourcePlan.Course.Patient.Id != targetPlan.Course.Patient.Id)
            {
                Console.WriteLine("The source and target patient must be the same.");
                return false;
            }


            //***Disabled this check for OART plans that use different structure sets***
            // Ensure both plans share the same structure set
            //if (sourcePlan.StructureSet.Id != targetPlan.StructureSet.Id)
            //{
            //    Console.WriteLine("The source and target structure set must be the same.");
            //    return false;
            //}

            // Iterate through each optimization objective in the source plan
            foreach (var objective in sourcePlan.OptimizationSetup.Objectives)
            {

                // 🔹 Explicitly skip line objectives
                if (objective is OptimizationLineObjective)
                {
                    Console.WriteLine(
                        $"Skipping line objective for structure '{objective.StructureId}'.");
                    continue;
                }

                try
                {
                    // Wrap the objective as a DynamicObjective and add it to the target plan
                    var dynObj = new DynamicObjective(objective);
                    dynObj.AddObjective(targetPlan);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to copy objective for structure '{objective.StructureId}': {ex.Message}");
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Removes all optimization objectives from the specified external plan setup.
        /// </summary>
        /// <param name="plan">The external plan setup from which optimization objectives will be removed.</param>
        /// <returns>
        /// True if all optimization objectives are successfully removed; otherwise, false if an error occurs.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the plan is null or the OptimizationSetup is not initialized.</exception>
        /// <exception cref="InvalidOperationException">Thrown if an error occurs while removing optimization objectives.</exception>
        public static bool RemoveAllOptimizationObjectives(ExternalPlanSetup plan)
        {
            // Validate input parameter
            if (plan == null)
            {
                Console.WriteLine("Error: Plan is null.");
                throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");
            }

            if (plan.OptimizationSetup == null)
            {
                Console.WriteLine("Error: OptimizationSetup is not initialized in the plan.");
                throw new ArgumentNullException(nameof(plan.OptimizationSetup), "OptimizationSetup is not initialized.");
            }

            try
            {
                // Create a local list of objectives to avoid accessing disposed objects during iteration
                var objectivesToRemove = plan.OptimizationSetup.Objectives.ToList();

                // Log the count of objectives to be removed
                Console.WriteLine($"Found {objectivesToRemove.Count} optimization objectives to remove.");

                // Remove each objective
                foreach (var objective in objectivesToRemove)
                {
                    try
                    {
                        plan.OptimizationSetup.RemoveObjective(objective);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error while removing an optimization objective. Skipping to the next. Error: {ex.Message}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error while removing all optimization objectives: {ex.Message}");
                throw new InvalidOperationException("An unexpected error occurred while removing optimization objectives.", ex);
            }
        }

        /// <summary>
        /// Removes all optimization objectives associated with a specified structure in the optimization setup.
        /// </summary>
        /// <param name="plan">The ExternalPlanSetup containing the optimization objectives.</param>
        /// <param name="structureId">The ID of the structure whose objectives will be removed.</param>
        /// <returns>
        /// Returns true if at least one objective was removed; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the plan or structureId is null.</exception>
        public static bool RemoveOptimizationObjectivesByStructureId(ExternalPlanSetup plan, string structureId)
        {
            // Validate input parameters
            if (plan == null)
                throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");

            if (string.IsNullOrWhiteSpace(structureId))
                throw new ArgumentNullException(nameof(structureId), "Structure ID cannot be null or empty.");

            try
            {
                // Retrieve objectives linked to the specified structure
                var structureObjectives = plan.OptimizationSetup.Objectives
                    .Where(x => x.StructureId.Equals(structureId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!structureObjectives.Any())
                {
                    Console.WriteLine($"No optimization objectives found for structure '{structureId}'.");
                    return false;
                }

                // Remove each objective associated with the structure
                foreach (var objective in structureObjectives)
                {
                    plan.OptimizationSetup.RemoveObjective(objective);
                }

                Console.WriteLine($"Successfully removed {structureObjectives.Count} optimization objectives for structure '{structureId}'.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing optimization objectives for structure '{structureId}': {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Retrieves a list of target structures with a lower optimization objective.
        /// This method calls the underlying retrieval method from StructureRepo.
        /// </summary>
        /// <param name="plan">The ExternalPlanSetup object containing the treatment plan.</param>
        /// <returns>
        /// A list of structures that have a lower optimization objective.
        /// Returns an empty list if the plan is null, the retrieval method fails, or no matching structures are found.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided plan is null.</exception>
        public static List<Structure> GetTargetsByLowerObjective(ExternalPlanSetup plan)
        {
            // Validate input
            if (plan == null)
                throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");

            try
            {
                // Call the underlying retrieval method
                var targets = GetTargetsByLowerObjective(plan);

                // Verify and return the result, ensuring no null values
                return targets ?? new List<Structure>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving target structures with a lower objective: {ex.Message}");
                return new List<Structure>(); // Fail gracefully by returning an empty list
            }
        }

        /// <summary>
        /// Retrieves a list of organ-at-risk (OAR) structures that do not have a lower optimization objective.
        /// This method calls the underlying retrieval method from StructureRepo.
        /// </summary>
        /// <param name="plan">The ExternalPlanSetup object containing the treatment plan.</param>
        /// <returns>
        /// A list of OAR structures that do not have a lower optimization objective.
        /// Returns an empty list if the plan is null, the retrieval method fails, or no matching structures are found.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided plan is null.</exception>
        public static List<Structure> GetOARbyNoLowerObjective(ExternalPlanSetup plan)
        {
            // Validate input
            if (plan == null)
                throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");

            try
            {
                // Call the underlying retrieval method
                var oars = Structures.GetOARbyNoLowerObjective(plan);

                // Verify and return the result, ensuring no null values
                return oars ?? new List<Structure>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving OAR structures without a lower objective: {ex.Message}");
                return new List<Structure>(); // Fail gracefully by returning an empty list
            }
        }



        /// <summary>
        /// Represents a dynamic optimization objective that extracts key properties from a given optimization objective.
        /// </summary>
        public class DynamicObjective
        {
            public DynamicObjective()
            {

            }
            /// <summary>
            /// Initializes a new instance of the <see cref="DynamicObjective"/> class with explicit parameters.
            /// </summary>
            /// <param name="structureId">The structure identifier associated with the objective.</param>
            /// <param name="dose">The dose value for the objective.</param>
            /// <param name="volume">
            /// The volume value for point objectives. For non-point objectives, this value may be ignored.
            /// </param>
            /// <param name="priority">The priority value for the objective.</param>
            /// <param name="alpha">
            /// The alpha parameter for EUD objectives. For objectives that do not use alpha, this value may be ignored.
            /// </param>
            /// <param name="objOperator">The operator for the optimization objective.</param>
            /// <param name="objectiveType">The type of the optimization objective.</param>
            /// <exception cref="ArgumentException">
            /// Thrown if <paramref name="structureId"/> is null, empty, or whitespace.
            /// </exception>
            /// <exception cref="ArgumentNullException">
            /// Thrown if <paramref name="dose"/> is null.
            /// </exception>
            public DynamicObjective(
                string structureId,
                DoseValue dose,
                double volume,
                double priority,
                double alpha,
                OptimizationObjectiveOperator objOperator,
                OptimizationObjectiveType objectiveType)
            {
                // Validate input parameters.
                if (string.IsNullOrWhiteSpace(structureId))
                {
                    throw new ArgumentException("StructureId cannot be null or whitespace.", nameof(structureId));
                }
                if (dose == null)
                {
                    throw new ArgumentNullException(nameof(dose), "Dose cannot be null.");
                }

                // Initialize properties.
                StructureId = structureId;
                Dose = dose;
                Volume = volume;
                Priority = priority;
                this.alpha = alpha;
                ObjOperator = objOperator;
                ObjectiveType = objectiveType;
            }




            public string ToString()
            {
                return $"Structure: {StructureId}, Type: {ObjectiveType}, Dose: {Math.Round(Dose.Dose, 2)} {Dose.UnitAsString},  Volume: {Math.Round(Volume, 4)}%, Operator: {ObjOperator}";
            }


            /// <summary>
            /// Initializes a new instance of the <see cref="DynamicObjective"/> class from a serialized <see cref="DHO_OptObjective"/>.
            /// This is typically used to convert deserialized configuration data into an executable optimization objective.
            /// </summary>
            /// <param name="dho">The serialized objective to convert into a <see cref="DynamicObjective"/>.</param>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="dho"/> is null.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown when the structure ID is null or whitespace, or the dose is invalid.
            /// </exception>
            public DynamicObjective(DHO_OptObjective dho)
            {
                if (dho == null)
                    throw new ArgumentNullException(nameof(dho), "Serialized objective cannot be null.");


                if (string.IsNullOrWhiteSpace(dho.StructureId))
                    throw new ArgumentException("Structure ID cannot be null or empty.", nameof(dho.StructureId));

                if (dho.Type != DHO_OptObjType.Line && dho.Type != DHO_OptObjType.Unknown)
                {
                    if (dho.Dose == null)
                        throw new ArgumentException("Dose must be specified.", nameof(dho.Dose));
                }

                // Assign base properties
                StructureId = dho.StructureId;
                Priority = dho.Priority;

                // Map the operator enum to Eclipse's OptimizationObjectiveOperator
                switch (dho.Operator)
                {
                    case DHO_OptOperator.Upper:
                        ObjOperator = OptimizationObjectiveOperator.Upper;
                        break;
                    case DHO_OptOperator.Lower:
                        ObjOperator = OptimizationObjectiveOperator.Lower;
                        break;
                    case DHO_OptOperator.Exact:
                        ObjOperator = OptimizationObjectiveOperator.Exact;
                        break;
                    default:
                        ObjOperator = OptimizationObjectiveOperator.None; // Fallback
                        break;
                }

                // Map the type enum to Eclipse's OptimizationObjectiveType
                switch (dho.Type)
                {
                    case DHO_OptObjType.Point:
                        ObjectiveType = OptimizationObjectiveType.Point;
                        Dose = dho.Dose.EclipseDose();
                        Volume = dho.Volume;
                        break;
                    case DHO_OptObjType.EUD:
                        ObjectiveType = OptimizationObjectiveType.EUD;
                        Dose = dho.Dose.EclipseDose();
                        alpha = dho.ParameterA;
                        break;
                    case DHO_OptObjType.Line:
                        ObjectiveType = OptimizationObjectiveType.Line;
                        break;
                    case DHO_OptObjType.Mean:
                        ObjectiveType = OptimizationObjectiveType.Mean;
                        Dose = dho.Dose.EclipseDose();
                        break;
                    default:
                        ObjectiveType = OptimizationObjectiveType.Unknown; // Fallback
                        break;
                }
            }



            /// <summary>
            /// Initializes a new instance of the <see cref="DynamicObjective"/> class based on the provided optimization objective.
            /// </summary>
            /// <param name="objective">The <see cref="OptimizationObjective"/> to be converted into a dynamic objective.</param>
            /// <exception cref="ArgumentNullException">Thrown when the provided <paramref name="objective"/> is null.</exception>
            /// <exception cref="NotSupportedException">Thrown when the provided objective type is not supported (e.g., <see cref="OptimizationLineObjective"/>).</exception>
            public DynamicObjective(OptimizationObjective objective)
            {
                // Validate input.
                if (objective == null)
                {
                    throw new ArgumentNullException(nameof(objective), "Optimization objective cannot be null.");
                }

                // Do not support line objectives.
                if (objective is OptimizationLineObjective)
                {
                    throw new NotSupportedException("Line Objective is not supported.");
                }

                // Initialize common properties.
                StructureId = objective.StructureId;
                Dose = GetObjectiveDose(objective);
                Priority = GetObjectivePriority(objective);
                ObjOperator = objective.Operator;

                // Determine objective type and set type-specific properties.
                if (objective is OptimizationMeanDoseObjective)
                {
                    ObjectiveType = OptimizationObjectiveType.Mean;
                }
                else if (objective is OptimizationPointObjective)
                {
                    Volume = GetObjectiveVolume(objective);
                    ObjectiveType = OptimizationObjectiveType.Point;
                }
                else if (objective is OptimizationEUDObjective edo)
                {
                    alpha = edo.ParameterA;
                    ObjectiveType = OptimizationObjectiveType.EUD;
                }
                else
                {
                    // Mark as unknown if type is not recognized.
                    ObjectiveType = OptimizationObjectiveType.Unknown;
                }
            }

            /// <summary>
            /// Gets or sets the structure identifier associated with the optimization objective.
            /// </summary>
            public string StructureId { get; set; }

            /// <summary>
            /// Gets or sets the dose value associated with the optimization objective.
            /// </summary>
            public DoseValue Dose { get; set; }

            /// <summary>
            /// Gets or sets the volume value for point objectives.
            /// </summary>
            public double Volume { get; set; }

            /// <summary>
            /// Gets or sets the priority value associated with the optimization objective.
            /// </summary>
            public double Priority { get; set; }

            /// <summary>
            /// Gets or sets the alpha parameter for EUD objectives.
            /// </summary>
            public double alpha { get; set; }

            /// <summary>
            /// Gets or sets the operator for the optimization objective.
            /// </summary>
            public OptimizationObjectiveOperator ObjOperator { get; set; }

            /// <summary>
            /// Gets or sets the type of the optimization objective.
            /// </summary>
            public OptimizationObjectiveType ObjectiveType { get; set; }

            /// <summary>
            /// Adds this dynamic objective to the given treatment plan's optimization setup.
            /// </summary>
            /// <param name="plan">The <see cref="ExternalPlanSetup"/> instance to which the objective will be added.</param>
            /// <returns>
            /// <c>true</c> if the objective is added successfully; otherwise, <c>false</c>.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="plan"/> is null.</exception>
            public bool AddObjective(ExternalPlanSetup plan)
            {
                // Validate input.
                if (plan == null)
                {
                    throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");
                }

                // Retrieve the structure from the plan using the structure ID.
                Structure structure = RetrieveItems.StructureItems.GetStructureById(plan, StructureId);
                if (structure == null)
                {
                    Console.WriteLine($"Failed to add objective. Structure '{StructureId}' not found.");
                    return false;
                }

                try
                {
                    // Add the appropriate objective based on its type.
                    switch (ObjectiveType)
                    {
                        case OptimizationObjectiveType.Mean:
                            plan.OptimizationSetup.AddMeanDoseObjective(structure, Dose, Priority);
                            break;
                        case OptimizationObjectiveType.Point:
                            // Validate that Volume is within the allowed range (0 to 100).
                            // If not, set it 0/100.
                            double correctedVolume = Volume;
                            if (correctedVolume < 0)
                            {
                                Console.WriteLine("The requested objective volume was less than 0, so it was set to 0.");
                                correctedVolume = 0;
                            }

                            if (correctedVolume > 100)
                            {
                                Console.WriteLine("The requested objective volume was greater than 100, so it was set to 100.");
                                correctedVolume = 100;
                            }
                            plan.OptimizationSetup.AddPointObjective(structure, ObjOperator, Dose, correctedVolume, Priority);
                            break;
                        case OptimizationObjectiveType.EUD:
                            plan.OptimizationSetup.AddEUDObjective(structure, ObjOperator, Dose, alpha, Priority);
                            break;
                        default:
                            Console.WriteLine($"Objective not added. Type '{ObjectiveType}' not supported.");
                            return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while adding objective: {ex.Message}");
                    return false;
                }

                return true;
            }

            /// <summary>
            /// Retrieves the dose value from the specified optimization objective.
            /// </summary>
            /// <param name="objective">The optimization objective from which to retrieve the dose value.</param>
            /// <returns>
            /// The <see cref="DoseValue"/> associated with the optimization objective.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="objective"/> is null.</exception>
            /// <exception cref="NotSupportedException">Thrown when the objective type does not support dose retrieval.</exception>
            public static DoseValue GetObjectiveDose(OptimizationObjective objective)
            {
                // Validate input.
                if (objective == null)
                {
                    throw new ArgumentNullException(nameof(objective), "Objective cannot be null.");
                }

                // Determine dose based on objective type.
                if (objective is OptimizationMeanDoseObjective meanDoseObj)
                {
                    return meanDoseObj.Dose;
                }
                else if (objective is OptimizationPointObjective pointObj)
                {
                    return pointObj.Dose;
                }
                else if (objective is OptimizationEUDObjective eudObj)
                {
                    return eudObj.Dose;
                }
                else if (objective is OptimizationLineObjective)
                {
                    throw new NotSupportedException("Dose retrieval for OptimizationLineObjective is not supported.");
                }
                else
                {
                    throw new NotSupportedException("Objective type not supported for dose retrieval.");
                }
            }

            /// <summary>
            /// Retrieves the priority value from the specified optimization objective.
            /// </summary>
            /// <param name="objective">The optimization objective from which to retrieve the priority value.</param>
            /// <returns>
            /// The priority value (as a <see cref="double"/>) associated with the optimization objective.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="objective"/> is null.</exception>
            /// <exception cref="NotSupportedException">Thrown when the objective type does not support priority retrieval.</exception>
            public static double GetObjectivePriority(OptimizationObjective objective)
            {
                // Validate input.
                if (objective == null)
                {
                    throw new ArgumentNullException(nameof(objective), "Objective cannot be null.");
                }

                // Determine priority based on objective type.
                if (objective is OptimizationMeanDoseObjective meanDoseObj)
                {
                    return meanDoseObj.Priority;
                }
                else if (objective is OptimizationPointObjective pointObj)
                {
                    return pointObj.Priority;
                }
                else if (objective is OptimizationEUDObjective eudObj)
                {
                    return eudObj.Priority;
                }
                else if (objective is OptimizationLineObjective lineObj)
                {
                    // Optionally, you can throw an exception here if line objectives are not supported.
                    return lineObj.Priority;
                }
                else
                {
                    throw new NotSupportedException("Objective type not supported for priority retrieval.");
                }
            }

            /// <summary>
            /// Retrieves the volume value from the specified optimization objective.
            /// </summary>
            /// <param name="objective">The optimization objective from which to retrieve the volume value.</param>
            /// <returns>
            /// The volume value (as a <see cref="double"/>) associated with the optimization objective.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="objective"/> is null.</exception>
            /// <exception cref="NotSupportedException">Thrown when the objective type does not support volume retrieval.</exception>
            public static double GetObjectiveVolume(OptimizationObjective objective)
            {
                // Validate input.
                if (objective == null)
                {
                    throw new ArgumentNullException(nameof(objective), "Objective cannot be null.");
                }

                // Only point objectives support volume retrieval.
                if (objective is OptimizationPointObjective pointObj)
                {
                    return pointObj.Volume;
                }

                throw new NotSupportedException("Volume retrieval is not supported for the given objective type.");
            }



            /// <summary>
            /// Retrieves the dose value from a clinical goal based on its measure type.
            /// </summary>
            /// <param name="goal">The clinical goal from which to retrieve the dose.</param>
            /// <returns>
            /// A <see cref="double"/> representing the dose value from the clinical goal, in Gy or % depending on unit.
            /// </returns>
            /// <exception cref="ArgumentException">Thrown when the dose cannot be determined from the clinical goal.</exception>
            public static DoseValue GetDoseFromClinicalGoal(ExternalPlanSetup plan, ClinicalGoal goal)
            {

                var doseUnit = plan.TotalDose.Unit;
                var totalDoseValue = plan.TotalDose.Dose;
                switch (goal.MeasureType)
                {
                    case MeasureType.MeasureTypeDQP_DXXX:
                    case MeasureType.MeasureTypeDQP_DXXXcc:
                        if (goal.Objective.LimitUnit == ObjectiveUnit.Absolute)
                        {
                            if (doseUnit == DoseUnit.Gy)
                            {
                                return new DoseValue(goal.Objective.Limit, doseUnit);
                            }
                            else
                            {
                                return new DoseValue(goal.Objective.Limit * 100, doseUnit);
                            }
                        }
                        else
                        {
                            return new DoseValue(goal.Objective.Limit * totalDoseValue / 100.0, doseUnit);
                        }
                    case MeasureType.MeasureTypeDQP_VXXX:
                        return new DoseValue(goal.Objective.Value * totalDoseValue / 100.0, doseUnit);

                    case MeasureType.MeasureTypeDQP_VXXXGy:
                        if (doseUnit == DoseUnit.Gy)
                        {
                            //The value is Gy by default, so not correction needed
                            return new DoseValue(goal.Objective.Value, doseUnit);
                        }
                        else
                        {
                            //If the unit is cGy, then a conversion is necessary.
                            return new DoseValue(goal.Objective.Value * 100, doseUnit);
                        }

                    case MeasureType.MeasureTypeDoseMin:
                    case MeasureType.MeasureTypeDoseMax:
                    case MeasureType.MeasureTypeDoseMean:
                        if (goal.Objective.LimitUnit == ObjectiveUnit.Absolute)
                        {
                            if (doseUnit == DoseUnit.Gy)
                            {
                                return new DoseValue(goal.Objective.Limit, ESAPI_Helpers.SetDoseUnit());
                            }
                            else
                            {
                                return new DoseValue(goal.Objective.Limit * 100, ESAPI_Helpers.SetDoseUnit());
                            }
                        }
                        else
                        {
                            return new DoseValue(goal.Objective.Limit * totalDoseValue / 100.0, doseUnit);
                        }

                    default:
                        throw new ArgumentException($"Unsupported MeasureType: {goal.MeasureType}");
                }
            }


            /// <summary>
            /// Retrieves the volume value from a clinical goal based on its measure type.
            /// For Dmax and Dmin, returns 0 and 100 respectively, as per standard definitions.
            /// </summary>
            /// <param name="goal">The clinical goal from which to retrieve the volume.</param>
            /// <returns>
            /// A <see cref="double"/> representing the volume from the clinical goal, in cc or % depending on unit or convention.
            /// </returns>
            /// <exception cref="ArgumentException">Thrown when the volume cannot be determined from the clinical goal.</exception>
            public static double GetVolumeFromClinicalGoal(ExternalPlanSetup plan, ClinicalGoal goal)
            {
                var structure = RetrieveItems.StructureItems.GetStructureById(plan, goal.StructureId);
                var structureVolumeMMM = structure.Volume * 1000;
                switch (goal.MeasureType)
                {
                    case MeasureType.MeasureTypeDQP_DXXX:
                    case MeasureType.MeasureTypeDQP_DXXXcc:
                        if (goal.Objective.ValueUnit == ObjectiveUnit.Absolute)
                        {
                            return goal.Objective.Value * 100 / structureVolumeMMM;
                        }
                        else
                        {
                            return goal.Objective.Value;
                        }


                    case MeasureType.MeasureTypeDQP_VXXX:
                    case MeasureType.MeasureTypeDQP_VXXXGy:
                        if (goal.Objective.LimitUnit == ObjectiveUnit.Absolute)
                        {
                            return goal.Objective.Limit * 100 / structureVolumeMMM;
                        }
                        else
                        {
                            return goal.Objective.Limit;
                        }


                    case MeasureType.MeasureTypeDoseMax:
                        return 0.0; // Dmax applies to a point (0% volume)

                    case MeasureType.MeasureTypeDoseMin:
                        return 100.0; // Dmin applies to the full volume (100%)

                    default:
                        throw new ArgumentException($"Unsupported MeasureType: {goal.MeasureType} does not contain volume information.");
                }
            }

            /// <summary>
            /// Determines the corresponding <see cref="OptimizationObjectiveType"/> for a given clinical goal.
            /// </summary>
            /// <param name="goal">The clinical goal to evaluate.</param>
            /// <returns>The mapped <see cref="OptimizationObjectiveType"/>.</returns>
            public static OptimizationObjectiveType GetObjectiveTypeFromClinicalGoal(ClinicalGoal goal)
            {
                switch (goal.MeasureType)
                {
                    case MeasureType.MeasureTypeDoseMean:
                        return OptimizationObjectiveType.Mean;

                    case MeasureType.MeasureTypeDQP_DXXX:
                    case MeasureType.MeasureTypeDQP_DXXXcc:
                    case MeasureType.MeasureTypeDQP_VXXX:
                    case MeasureType.MeasureTypeDQP_VXXXGy:
                    case MeasureType.MeasureTypeDoseMax:
                    case MeasureType.MeasureTypeDoseMin:
                        return OptimizationObjectiveType.Point;

                    default:
                        return OptimizationObjectiveType.Unknown;
                }
            }


            /// <summary>
            /// Converts a clinical goal's <see cref="ObjectiveOperator"/> to an <see cref="OptimizationObjectiveOperator"/>.
            /// </summary>
            /// <param name="goal">The clinical goal from which to determine the operator type.</param>
            /// <returns>
            /// The corresponding <see cref="OptimizationObjectiveOperator"/>.
            /// </returns>
            public static OptimizationObjectiveOperator GetOperatorFromClinicalGoal(ClinicalGoal goal)
            {

                switch (goal.Objective.Operator)
                {
                    case ObjectiveOperator.LessThan:
                    case ObjectiveOperator.LessThanOrEqual:
                        return OptimizationObjectiveOperator.Upper;

                    case ObjectiveOperator.GreaterThan:
                    case ObjectiveOperator.GreaterThanOrEqual:
                        return OptimizationObjectiveOperator.Lower;

                    case ObjectiveOperator.Equals:
                        return OptimizationObjectiveOperator.Exact;

                    default:
                        return OptimizationObjectiveOperator.None;
                }
            }


            public override bool Equals(object obj)
            {
                var other = obj as DynamicObjective;
                if (other == null)
                    return false;

                if (!string.Equals(StructureId, other.StructureId, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (ObjectiveType != other.ObjectiveType)
                    return false;

                if (Math.Abs(Dose.Dose - other.Dose.Dose) > 0.01)
                    return false;

                if (Math.Abs(Priority - other.Priority) >= 0.0001)
                    return false;

                if (ObjectiveType == OptimizationObjectiveType.Point)
                {
                    if (ObjOperator != other.ObjOperator)
                        return false;

                    if (Math.Abs(Volume - other.Volume) >= 0.01)
                        return false;
                }

                return true;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;

                    hash = hash * 23 + (StructureId != null ? StructureId.ToUpperInvariant().GetHashCode() : 0);
                    hash = hash * 23 + ObjectiveType.GetHashCode();
                    hash = hash * 23 + ObjOperator.GetHashCode();
                    hash = hash * 23 + Math.Round(Dose.Dose, 2).GetHashCode();
                    hash = hash * 23 + Math.Round(Priority, 2).GetHashCode();

                    if (ObjectiveType == OptimizationObjectiveType.Point)
                    {
                        hash = hash * 23 + Math.Round(Volume, 2).GetHashCode();
                    }

                    return hash;
                }
            }

            public static bool operator ==(DynamicObjective left, DynamicObjective right)
            {
                if (ReferenceEquals(left, right))
                    return true;

                if (((object)left == null) || ((object)right == null))
                    return false;

                return left.Equals(right);
            }

            public static bool operator !=(DynamicObjective left, DynamicObjective right)
            {
                return !(left == right);
            }



        }

        public enum DHO_OptObjType
        {
            EUD = 0,
            Mean = 1,
            Line = 2,
            Point = 3,
            Unknown = 999
        }


        public enum DHO_OptOperator
        {
            Upper = 0,
            Lower = 1,
            Exact = 2,
            None = 99
        }


        public class DHO_OptObjective
        {
            public DHO_OptObjective() { }
            public string StructureId { get; set; }
            public DHO_OptOperator Operator { get; set; }
            public DHO_DoseObject Dose { get; set; }
            public double Priority { get; set; }
            public DHO_OptObjType Type { get; set; }

            //optional
            public DHO_DVHPoint[] CurveData { get; set; }
            public double ParameterA { get; set; }
            public double Volume { get; set; }
        }


        public class DHO_DVHPoint
        {
            public DHO_DVHPoint() { }
            public double Volume { get; set; }
            public string VolumeUnit { get; set; }
            public DHO_DoseObject Dose { get; set; }

        }
        public enum DHO_DoseUnit
        {
            cGy = 0,
            Gy = 1,
            Percent = 2
        }
        public class DHO_DoseObject
        {
            public DHO_DoseObject()
            {
                Unit = DHO_DoseUnit.cGy;
                IsAbsoluteDoseValue = false;
            }

            public DHO_DoseObject(double dose, bool isAbsolute, DHO_DoseUnit unit)
            {
                Dose = dose;
                IsAbsoluteDoseValue = isAbsolute;
                Unit = unit;
            }
            public double Dose { get; set; }
            public bool IsAbsoluteDoseValue { get; set; }
            public DHO_DoseUnit Unit { get; set; }
            public string DoseDisplay()
            {
                if (IsAbsoluteDoseValue)
                {
                    return $"{Dose} {Unit}";
                }
                else
                {
                    return $"{Dose} %";
                }
            }

            public DoseValue EclipseDose()
            {
                if (Unit == DHO_DoseUnit.cGy)
                {
                    return new DoseValue(Dose, DoseUnit.cGy);
                }

                if (Unit == DHO_DoseUnit.Gy)
                {
                    return new DoseValue(Dose, DoseUnit.Gy);
                }

                return new DoseValue(0, DoseUnit.Unknown); ;
            }
        }


        /// <summary>
        /// Enumerates the supported types of optimization objectives.
        /// </summary>
        public enum OptimizationObjectiveType
        {
            /// <summary>
            /// Represents a point objective.
            /// </summary>
            Point = 0,

            /// <summary>
            /// Represents a mean dose objective.
            /// </summary>
            Mean = 1,

            /// <summary>
            /// Represents an EUD (Equivalent Uniform Dose) objective.
            /// </summary>
            EUD = 2,

            /// <summary>
            /// Represents a line objective.
            /// </summary>
            Line = 3,

            /// <summary>
            /// Represents an unknown or unsupported objective type.
            /// </summary>
            Unknown = 4
        }

        public static class Line
        {
            /// <summary>
            /// Converts line objectives in the optimization setup of the target plan to point objectives based on a specified percentage to keep.
            /// </summary>
            /// <param name="targetPlan">The plan containing the line objectives to convert.</param>
            /// <param name="percentageToKeep">The percentage of DVH points to retain as point objectives.</param>
            public static void ConvertAllLineObjectivesToPoints(ExternalPlanSetup targetPlan, double percentageToKeep)
            {
                if (targetPlan == null)
                    throw new ArgumentNullException(nameof(targetPlan), "Target plan cannot be null.");
                if (percentageToKeep <= 0 || percentageToKeep > 100)
                    throw new ArgumentOutOfRangeException(nameof(percentageToKeep), "Percentage to keep must be between 0 and 100.");

                var initialObjectives = targetPlan.OptimizationSetup.Objectives.ToList();

                foreach (var objective in initialObjectives)
                {
                    if (objective is OptimizationLineObjective lineObjective)
                    {
                        if (!ConvertSingleLineObjectiveToPoints(targetPlan, lineObjective, percentageToKeep))
                        {
                            throw new InvalidOperationException($"Failed to convert line objective for structure {lineObjective.Structure.Id}.");
                        }
                    }
                }
            }

            /// <summary>
            /// Converts all line objectives for a list of specific structures in the optimization setup of the target plan to point objectives.
            /// This method acts as an overload to process multiple structures by calling the single-structure version iteratively.
            /// </summary>
            /// <param name="targetPlan">The external plan setup containing the line objectives.</param>
            /// <param name="StructureIds">A list of structure IDs for which to convert line objectives.</param>
            /// <param name="percentageToKeep">The percentage of DVH points to retain as point objectives (between 0 and 100).</param>
            /// <exception cref="ArgumentNullException">Thrown if targetPlan or StructureIds is null, or if any structure ID in the list is null or empty.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if percentageToKeep is not in the range (0, 100].</exception>
            public static void ConvertStructureLineObjectivesToPoints(ExternalPlanSetup targetPlan, List<string> StructureIds, double percentageToKeep)
            {
                // Validate inputs
                if (targetPlan == null)
                    throw new ArgumentNullException(nameof(targetPlan), "Target plan cannot be null.");
                if (StructureIds == null || !StructureIds.Any())
                    throw new ArgumentNullException(nameof(StructureIds), "Structure IDs list cannot be null or empty.");
                if (StructureIds.Any(string.IsNullOrWhiteSpace))
                    throw new ArgumentException("Structure IDs list contains null or empty values.", nameof(StructureIds));
                if (percentageToKeep <= 0 || percentageToKeep > 100)
                    throw new ArgumentOutOfRangeException(nameof(percentageToKeep), "Percentage to keep must be between 0 and 100.");

                try
                {
                    foreach (string structureId in StructureIds)
                    {
                        try
                        {
                            // Call the single-structure conversion method for each structure ID
                            ConvertStructureLineObjectivesToPoints(targetPlan, structureId, percentageToKeep);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error converting line objectives for structure {structureId}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error in ConvertStructureLineObjectivesToPoints: {ex.Message}");
                    throw; // Re-throw the exception for higher-level handling if needed
                }
            }

            /// <summary>
            /// Converts all line objectives for a specific structure in the optimization setup of the target plan to point objectives.
            /// </summary>
            /// <param name="targetPlan">The external plan setup containing the line objectives.</param>
            /// <param name="StructureId">The ID of the structure for which to convert line objectives.</param>
            /// <param name="percentageToKeep">The percentage of DVH points to retain as point objectives (between 0 and 100).</param>
            public static void ConvertStructureLineObjectivesToPoints(ExternalPlanSetup targetPlan, string StructureId, double percentageToKeep)
            {
                if (targetPlan == null)
                    throw new ArgumentNullException(nameof(targetPlan), "Target plan cannot be null.");
                if (string.IsNullOrWhiteSpace(StructureId))
                    throw new ArgumentNullException(nameof(StructureId), "Structure ID cannot be null or empty.");
                if (percentageToKeep <= 0 || percentageToKeep > 100)
                    throw new ArgumentOutOfRangeException(nameof(percentageToKeep), "Percentage to keep must be between 0 and 100.");
                if (targetPlan.StructureSet == null)
                    throw new InvalidOperationException("The plan's StructureSet cannot be null.");
                if (!targetPlan.StructureSet.Structures.Any(x => x.Id.ToUpper() == StructureId.ToUpper()))
                    throw new InvalidOperationException($"The structure {StructureId} does not exist in the StructureSet.");

                var lineObjectives = targetPlan.OptimizationSetup.Objectives
                    .OfType<OptimizationLineObjective>()
                    .Where(x => x.StructureId.ToUpper() == StructureId.ToUpper())
                    .ToList();

                if (!lineObjectives.Any())
                    throw new InvalidOperationException($"No line objectives found for structure {StructureId}.");

                foreach (var lineObjective in lineObjectives)
                {
                    if (!ConvertSingleLineObjectiveToPoints(targetPlan, lineObjective, percentageToKeep))
                    {
                        throw new InvalidOperationException($"Failed to convert line objective for structure {lineObjective.Structure.Id}.");
                    }
                }
            }

            /// <summary>
            /// Converts a single line objective to point objectives based on a specified percentage to keep.
            /// </summary>
            /// <param name="targetPlan">The external plan setup containing the line objective.</param>
            /// <param name="lineObjective">The line objective to process.</param>
            /// <param name="percentageToKeep">The percentage of DVH points to retain as point objectives.</param>
            /// <returns>True if the line objective was successfully processed; otherwise, false.</returns>
            public static bool ConvertSingleLineObjectiveToPoints(
                ExternalPlanSetup targetPlan,
                OptimizationLineObjective lineObjective,
                double percentageToKeep)
            {
                try
                {
                    var dvhPoints = lineObjective.CurveData;
                    int totalPoints = dvhPoints.Length;
                    int numPointsToKeep = (int)(totalPoints * percentageToKeep / 100);

                    if (numPointsToKeep <= 0) return true;

                    // Sort DVH points by dose in descending order
                    var sortedPoints = dvhPoints.OrderByDescending(p => p.DoseValue.Dose).ToArray();

                    // Add max and min dose points
                    AddPointObjectiveFromDvhPoint(targetPlan, lineObjective, sortedPoints.First());
                    AddPointObjectiveFromDvhPoint(targetPlan, lineObjective, sortedPoints.Last());

                    // Remove max and min dose points
                    var remainingPoints = sortedPoints.Skip(1).Take(sortedPoints.Length - 2).ToArray();

                    // Add evenly distributed points
                    AddEvenlyDistributedPoints(targetPlan, lineObjective, remainingPoints, numPointsToKeep);

                    // Remove the original line objective
                    targetPlan.OptimizationSetup.RemoveObjective(lineObjective);

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing line objective for {lineObjective.Structure.Id}: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// Adds a single point objective to the optimization setup based on a DVH point.
            /// </summary>
            /// <param name="targetPlan">The plan to add the point objective to.</param>
            /// <param name="lineObjective">The original line objective providing the structure and priority.</param>
            /// <param name="dvhPoint">The DVH point to use for the point objective.</param>
            private static void AddPointObjectiveFromDvhPoint(
                ExternalPlanSetup targetPlan,
                OptimizationLineObjective lineObjective,
                DVHPoint dvhPoint)
            {
                targetPlan.OptimizationSetup.AddPointObjective(
                    lineObjective.Structure,
                    OptimizationObjectiveOperator.Upper,
                    dvhPoint.DoseValue,
                    dvhPoint.Volume,
                    lineObjective.Priority
                );
            }

            /// <summary>
            /// Adds evenly distributed point objectives from the remaining DVH points.
            /// </summary>
            /// <param name="targetPlan">The plan to add the point objectives to.</param>
            /// <param name="lineObjective">The original line objective providing the structure and priority.</param>
            /// <param name="remainingPoints">The remaining DVH points after removing max and min.</param>
            /// <param name="numPointsToKeep">The number of evenly distributed points to keep.</param>
            private static void AddEvenlyDistributedPoints(
                ExternalPlanSetup targetPlan,
                OptimizationLineObjective lineObjective,
                DVHPoint[] remainingPoints,
                int numPointsToKeep)
            {
                int interval = (int)Math.Ceiling(remainingPoints.Length / (double)numPointsToKeep);

                for (int i = 0; i < remainingPoints.Length; i += interval)
                {
                    if (i >= remainingPoints.Length) break;

                    var point = remainingPoints[i];
                    targetPlan.OptimizationSetup.AddPointObjective(
                        lineObjective.Structure,
                        OptimizationObjectiveOperator.Upper,
                        point.DoseValue,
                        point.Volume,
                        lineObjective.Priority
                    );
                }
            }

            /// <summary>
            /// Generates a list of point objectives that would result from converting a line objective, without adding them to the plan.
            /// </summary>
            /// <param name="lineObjective">The line objective to convert.</param>
            /// <param name="percentageToKeep">The percentage of DVH points to retain as point objectives (between 0 and 100).</param>
            /// <returns>
            /// A list of tuples representing point objectives with dose, volume, and priority. Returns an empty list if no objectives are generated.
            /// </returns>
            /// <exception cref="ArgumentNullException">Thrown if the lineObjective is null.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if percentageToKeep is not in the range (0, 100].</exception>
            public static List<(DoseValue Dose, double Volume, double Priority)> GetPointRepresentationOfLineObjective(
                OptimizationLineObjective lineObjective,
                double percentageToKeep)
            {
                if (lineObjective == null)
                    throw new ArgumentNullException(nameof(lineObjective), "Line objective cannot be null.");

                if (percentageToKeep <= 0 || percentageToKeep > 100)
                    throw new ArgumentOutOfRangeException(nameof(percentageToKeep), "Percentage to keep must be between 0 and 100.");

                var result = new List<(DoseValue Dose, double Volume, double Priority)>();

                try
                {
                    var dvhPoints = lineObjective.CurveData;
                    int totalPoints = dvhPoints.Length;
                    int numPointsToKeep = (int)(totalPoints * percentageToKeep / 100);

                    if (numPointsToKeep <= 0)
                        return result; // Return empty list if no points to keep

                    // Sort DVH points by dose in descending order
                    var sortedPoints = dvhPoints.OrderByDescending(p => p.DoseValue.Dose).ToArray();

                    // Add max and min dose points to the result
                    result.Add((sortedPoints.First().DoseValue, sortedPoints.First().Volume, lineObjective.Priority));
                    result.Add((sortedPoints.Last().DoseValue, sortedPoints.Last().Volume, lineObjective.Priority));

                    // Remove max and min dose points
                    var remainingPoints = sortedPoints.Skip(1).Take(sortedPoints.Length - 2).ToArray();

                    // Add evenly distributed points
                    int interval = (int)Math.Ceiling(remainingPoints.Length / (double)(numPointsToKeep - 2));
                    for (int i = 0; i < remainingPoints.Length; i += interval)
                    {
                        if (i >= remainingPoints.Length) break;
                        var point = remainingPoints[i];
                        result.Add((point.DoseValue, point.Volume, lineObjective.Priority)); // Ensure Priority is an int
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating point objectives: {ex.Message}");
                }

                return result;
            }
        }
    }


}
