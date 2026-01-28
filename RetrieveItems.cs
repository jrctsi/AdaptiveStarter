using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace AdaptiveStarter
{
    //MCB
    public static class RetrieveItems
    {
        public static class StructureItems
        {
            /// <summary>
            /// Retrieves structures from a structure set that match the specified list of IDs.
            /// The method performs a case-insensitive comparison to find matches and identifies any IDs that do not exist in the structure set.
            /// </summary>
            /// <param name="structureSet">The structure set containing the structures to search.</param>
            /// <param name="listOfStructureIds">A list of structure IDs to match within the structure set.</param>
            /// <returns>
            /// A tuple containing:
            /// - MatchedStructures: A list of structures that match the specified IDs.
            /// - MissingStructureIds: A list of IDs that could not be matched in the structure set.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the structure set or list of structure IDs is null.
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown if the list of structure IDs is empty.
            /// </exception>
            /// <remarks>
            /// This method uses a case-insensitive comparison to match structure IDs and ensure accuracy across different casing conventions.
            /// </remarks>

            public static (List<Structure> MatchedStructures, List<string> MissingStructureIds) GetMatchingStructuresById(
            StructureSet structureSet, List<string> listOfStructureIds)
            {
                if (structureSet == null)
                    throw new ArgumentNullException(nameof(structureSet), "StructureSet cannot be null.");
                if (listOfStructureIds == null)
                    throw new ArgumentException("ListOfStructureIds cannot be null.", nameof(listOfStructureIds));

                // Convert input IDs to uppercase for case-insensitive comparison
                var inputIdsUpper = listOfStructureIds.Select(id => id.ToUpperInvariant()).ToHashSet();

                // Find matching structures
                var matchedStructures = structureSet.Structures
                    .Where(s => inputIdsUpper.Contains(s.Id.ToUpperInvariant()))
                    .ToList();

                // Find missing IDs
                var matchedIdsUpper = matchedStructures.Select(s => s.Id.ToUpperInvariant()).ToHashSet();
                var missingIds = listOfStructureIds
                    .Where(id => !matchedIdsUpper.Contains(id.ToUpperInvariant()))
                    .ToList();

                return (matchedStructures, missingIds);
            }

            /// <summary>
            /// Retrieves a structure from the specified structure set by its ID, using a case-insensitive comparison.
            /// </summary>
            /// <param name="structureSet">The structure set containing the structures to search.</param>
            /// <param name="structureId">The ID of the structure to retrieve.</param>
            /// <returns>
            /// The structure with the specified ID if found; otherwise, <c>null</c>.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="structureSet"/> is null or <paramref name="structureId"/> is null or empty.
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// Thrown when multiple structures match the given ID, which should be unique.
            /// </exception>
            /// <remarks>
            /// This method performs a case-insensitive comparison to match the structure ID.
            /// </remarks>
            public static Structure GetStructureById(StructureSet structureSet, string structureId)
            {
                if (structureSet == null)
                    throw new ArgumentNullException(nameof(structureSet), "Structure set cannot be null.");

                if (string.IsNullOrWhiteSpace(structureId))
                    throw new ArgumentNullException(nameof(structureId), "Structure ID cannot be null or empty.");

                var matches = structureSet.Structures
                    .Where(s => string.Equals(s.Id, structureId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count > 1)
                    throw new InvalidOperationException($"Multiple structures found with ID '{structureId}' (case-insensitive).");

                return matches.SingleOrDefault();
            }

            /// <summary>
            /// Retrieves a structure from a planning item's structure set by structure ID, using a case-insensitive comparison.
            /// </summary>
            /// <param name="plan">The planning item containing the structure set.</param>
            /// <param name="structureId">The ID of the structure to retrieve.</param>
            /// <returns>
            /// The structure with the specified ID if found; otherwise, <c>null</c>.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="plan"/> is null or does not have a structure set.
            /// </exception>
            public static Structure GetStructureById(PlanningItem plan, string structureId)
            {
                if (plan == null)
                    throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");

                if (plan.StructureSet == null)
                    throw new ArgumentNullException(nameof(plan.StructureSet), "Plan does not contain a structure set.");

                return GetStructureById(plan.StructureSet, structureId);
            }

            /// <summary>
            /// Retrieves a structure set from the specified patient by its ID using case-insensitive comparison.
            /// </summary>
            /// <param name="patient">The patient containing the structure sets.</param>
            /// <param name="structureSetId">The ID of the structure set to retrieve (case-insensitive).</param>
            /// <returns>
            /// The <see cref="StructureSet"/> with the specified ID if found; otherwise, <c>null</c>.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="patient"/> or <paramref name="structureSetId"/> is <c>null</c> or empty.
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// Thrown when multiple structure sets match the given ID (case-insensitive), which should not occur.
            /// </exception>
            public static StructureSet GetStructureSetById(Patient patient, string structureSetId)
            {
                // Validate input
                if (patient == null)
                    throw new ArgumentNullException(nameof(patient), "Patient cannot be null.");

                if (string.IsNullOrWhiteSpace(structureSetId))
                    throw new ArgumentNullException(nameof(structureSetId), "StructureSet ID cannot be null or empty.");

                // Find matches using case-insensitive comparison
                var matches = patient.StructureSets
                    .Where(s => string.Equals(s.Id, structureSetId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count > 1)
                    throw new InvalidOperationException($"Multiple structure sets found with ID '{structureSetId}' (case-insensitive).");

                // Return the single match, or null if not found
                return matches.SingleOrDefault();
            }

            /// <summary>
            /// Retrieves the structure set associated with a given planning item.
            /// </summary>
            /// <param name="plan">The planning item whose structure set is to be retrieved.</param>
            /// <returns>
            /// The <see cref="StructureSet"/> from the planning item’s associated patient and structure set ID.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when the plan, its course, patient, or structure set is null.
            /// </exception>
            public static StructureSet GetStructureSetById(PlanningItem plan)
            {
                if (plan == null)
                    throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");

                if (plan.Course == null || plan.Course.Patient == null)
                    throw new ArgumentNullException(nameof(plan.Course), "Plan's course or patient is null.");

                if (plan.StructureSet == null)
                    throw new ArgumentNullException(nameof(plan.StructureSet), "Plan does not contain a structure set.");

                return GetStructureSetById(plan.Course.Patient, plan.StructureSet.Id);
            }

            public static StructureSet GetStructureSetById(Course course, string structureSetId)
            {
                if (course == null)
                    throw new ArgumentNullException(nameof(course), "Plan cannot be null.");
                if (string.IsNullOrWhiteSpace(structureSetId))
                    throw new ArgumentNullException(nameof(structureSetId), "StructureSet ID cannot be null or empty.");

                return GetStructureSetById(course.Patient, structureSetId);
            }

        }


        /// <summary>
        /// Retrieves a list of treatment beams from the given ExternalPlanSetup.
        /// Excludes setup fields and, for versions prior to 15.6, also excludes imaging fields.
        /// </summary>
        /// <param name="plan">The ExternalPlanSetup object representing the treatment plan.</param>
        /// <returns>
        /// A list of treatment beams that are not setup fields, and not imaging fields in pre-15.6 versions.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the plan is null.</exception>
        public static List<Beam> GetTreatmentBeams(ExternalPlanSetup plan)
        {
            if (plan == null)
            {
                Console.WriteLine("Error: Plan cannot be null - returning empty list.");
                return new List<Beam>();
            }

            try
            {
#if !V156
                // For versions before 15.6, exclude both setup and imaging treatment fields
                return plan.Beams
                           .Where(b => !b.IsSetupField && !b.IsImagingTreatmentField)
                           .ToList();
#else
        // For versions 15.6 and above, exclude only setup fields
        return plan.Beams
                   .Where(b => !b.IsSetupField)
                   .ToList();
#endif
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while retrieving treatment beams: {ex.Message}");
                throw;
            }
        }
    }
}
