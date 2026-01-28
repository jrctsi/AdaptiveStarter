using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace AdaptiveStarter
{
    //MCB
    public static class Structures
    {

        /// <summary>
        /// Creates a new structure in the specified <see cref="StructureSet"/> with a randomly generated unique ID.
        /// </summary>
        /// <param name="structureSet">The structure set in which the new structure will be created.</param>
        /// <param name="DICOMType">The DICOM type of the structure (e.g., "PTV", "OAR").</param>
        /// <returns>
        /// The newly created <see cref="Structure"/> object if successful; otherwise, <c>null</c> if an error occurs.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="structureSet"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// The method generates a unique random ID and uses it to create a new structure with the specified DICOM type.
        /// If the operation fails, an error message is logged to the console, and the method returns <c>null</c>.
        /// </remarks>

        public static Structure AddNewStructureWithRandomId(StructureSet structureSet, string DICOMType, bool highResolution, string comment)
        {
            if (structureSet == null)
            {
                throw new ArgumentNullException(nameof(structureSet), "StructureSet cannot be null.");
            }

            try
            {
                // Generate a unique random ID for the structure
                string randomId = GetRandomNonExistingStructureId(10, structureSet);

                // Create a new structure with the generated ID and specified DICOM type
                Structure newStructure = structureSet.AddStructure(DICOMType, randomId);

                //Console.WriteLine($"New structure '{newStructure.Id}' created successfully.");

                if (highResolution)
                {
                    newStructure.ConvertToHighResolution();
                }

                return newStructure;
            }
            catch (Exception ex)
            {
               return null;
            }
        }

        public static Structure AddNewStructureWithRandomId(StructureSet structureSet, string DICOMType, string comment)
        {
            return AddNewStructureWithRandomId(structureSet, DICOMType, false, comment);
        }

        /// <summary>
        /// Crops a list of structures (OARs) from the base structure by expanding them with a margin and removing the resulting volumes from the base structure.
        /// </summary>
        /// <param name="structureSet">The StructureSet containing the base structure and structures to crop.</param>
        /// <param name="BaseStructureId">The ID of the base structure to crop from.</param>
        /// <param name="StructuresToCrop">A list of IDs for the structures to crop from the base structure.</param>
        /// <param name="marginMM">The margin in millimeters by which to expand the structures to crop.</param>
        /// <returns>True if the operation is successful; otherwise, false.</returns>
        public static bool CropStructure(StructureSet structureSet, string BaseStructureId, List<string> StructuresToCrop, double marginMM)
        {
            try
            {
                // Validate inputs
                if (structureSet == null)
                    throw new ArgumentNullException(nameof(structureSet), "StructureSet cannot be null.");
                if (string.IsNullOrWhiteSpace(BaseStructureId))
                    throw new ArgumentException("BaseStructureId cannot be null or empty.", nameof(BaseStructureId));
                if (StructuresToCrop == null || !StructuresToCrop.Any())
                {
                    return true; // No operation required.
                }
                if (marginMM < 0)
                    throw new ArgumentOutOfRangeException(nameof(marginMM), "Margin must be greater than or equal to zero.");

                // Get the base structure
                var baseStructure = RetrieveItems.StructureItems.GetStructureById(structureSet, BaseStructureId);
                if (baseStructure == null)
                    throw new InvalidOperationException($"Base structure '{BaseStructureId}' does not exist in the StructureSet.");

                var temporaryStructures = new List<Structure>();

                foreach (var structureId in StructuresToCrop)
                {
                    // Get the structure to crop
                    var structureToCrop = RetrieveItems.StructureItems.GetStructureById(structureSet, structureId);
                    if (structureToCrop == null)
                    {
                        Console.WriteLine($"Warning: Structure '{structureId}' not found and will be skipped.");
                        continue;
                    }

                    // Create a temporary structure with the margin applied
                    string tempStructureId = GetRandomNonExistingStructureId(10, structureSet);
                    var tempStructure = AddNewStructureWithRandomId(structureSet, "Organ", "CropStructue, tempStructure");
                    if (structureToCrop.IsHighResolution)
                    {
                        //Convert to high resolution
                        ConvertSingle(tempStructure);
                    }
                    tempStructure.SegmentVolume = structureToCrop.Margin(marginMM);
                    temporaryStructures.Add(tempStructure);
                }

                // Ensure temporary structures are high resolution
                var (updatedTempStructures, tempStructuresToRemove, crosswalk) =
                    EnsureHighResolution(structureSet, temporaryStructures);

                // Add the high-resolution structures back to the list of temporary structures.
                temporaryStructures.AddRange(updatedTempStructures);
                // Remove duplicate structures so removal won't be attempted multiple times.
                temporaryStructures = temporaryStructures.Distinct().ToList();

                if (temporaryStructures.Any(s => s.IsHighResolution))
                {
                    //Convert to High Resolution
                    ConvertSingle(baseStructure);
                }

                // Perform the subtraction to crop the base structure
                if (!RemoveStructures(structureSet, BaseStructureId, updatedTempStructures.Select(s => s.Id).ToList()))
                {
                    Console.WriteLine($"Error: Failed to crop structures from base structure '{BaseStructureId}'.");
                    return false;
                }

                // Clean up temporary structures
                foreach (var tempStructure in temporaryStructures)
                {
                    structureSet.RemoveStructure(tempStructure);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CropOAR: {ex.Message}");
                return false;
            }
        }



        /// <summary>
        /// Creates cropped structures for Simultaneous Integrated Boost (SIB) planning based on specified targets and dose thresholds.
        /// This method ensures new structures are created or reused according to the `replace` flag, applies margin-based cropping,
        /// and assigns visually distinct colors to the new structures.
        ///
        /// </summary>
        /// <param name="structureSet">The structure set containing the original and new structures.</param>
        /// <param name="TargetsToCrop">
        /// A list of tuples containing the target structure IDs and their associated doses.
        /// Structures with higher doses are used to crop the target structure.
        /// </param>
        /// <param name="marginMM">The margin (in mm) applied during the crop operation.</param>
        /// <param name="prefix">The prefix added to the new structure IDs.</param>
        /// <param name="suffix">The suffix added to the new structure IDs.</param>
        /// <param name="replace">
        /// If true, existing structures with the same ID are removed and replaced.
        /// If false, new structures are created with unique IDs.
        /// </param>
        /// <returns>
        /// A list of tuples containing the original structure ID, the new structure ID, and the dose associated with the cropped structure.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the structure set is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the target list is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the margin is less than zero.</exception>

        public static List<(string OriginalId, string CroppedId, double dose)> CropSIBTargets(
            StructureSet structureSet,
            List<(string StructureId, double Dose)> TargetsToCrop,
            double marginMM,
            string prefix,
            string suffix,
            bool replace)
        {
            if (structureSet == null)
                throw new ArgumentNullException(nameof(structureSet), "StructureSet cannot be null.");
            if (TargetsToCrop == null || !TargetsToCrop.Any())
                throw new ArgumentException("TargetsToCrop cannot be null or empty.", nameof(TargetsToCrop));
            if (marginMM < 0)
                throw new ArgumentOutOfRangeException(nameof(marginMM), "Margin must be greater than or equal to zero.");

            var croppedStructures = new List<(string OriginalId, string NewId, double dose)>();

            foreach (var target in TargetsToCrop)
            {
                string originalId = target.StructureId;
                double dose = target.Dose;

                // Generate the structure ID with prefix and suffix
                string newId = $"{prefix}{originalId}{suffix}";

                // Trim StructureId to 16 characters if necessary
                if (newId.Length > 16)
                {
                    newId = newId.Substring(0, 16);
                }

                // Check if the structure already exists
                var existingStructure = RetrieveItems.StructureItems.GetStructureById(structureSet, newId);
                Structure createdStructure;

                if (existingStructure != null)
                {
                    if (replace)
                    {
                        // Remove and recreate the structure if replace is true
                        var (success, recreatedStructure) = CheckExistenceByIdRemoveAndCreateNew(structureSet, newId);
                        if (!success)
                        {
                            Console.WriteLine($"Failed to recreate structure: {newId}");
                            continue;
                        }
                        createdStructure = recreatedStructure;
                    }
                    else
                    {
                        // Generate a unique ID for the new structure
                        newId = GetNextAvailableStructureId(structureSet, newId);

                        // Create a new structure with the unique ID
                        createdStructure = structureSet.AddStructure("PTV", newId);
                    }
                }
                else
                {
                    // Create the structure with the generated ID if it does not exist
                    createdStructure = structureSet.AddStructure("PTV", newId);
                }

                // Retrieve the structure to be cropped by its ID from the structure set
                var targetStructure = RetrieveItems.StructureItems.GetStructureById(structureSet, target.StructureId);

                // Check if the target structure is in high-resolution mode
                if (targetStructure.IsHighResolution)
                {
                    // Convert the newly created structure to high-resolution mode if necessary
                    ConvertSingle(createdStructure);
                }

                // Copy the original target structure into the newly created structure
                createdStructure.SegmentVolume = targetStructure.SegmentVolume;

                // Set the color of the newly created structure to a lighter shade of the target structure's color
                SetColorOfStructure(createdStructure, ESAPI_Helpers.LightenColor(targetStructure.Color, 0.5));

                // Get the list of structures to crop from
                var structuresToCrop = TargetsToCrop
                    .Where(t => t.Dose > dose)
                    .Select(t => t.StructureId)
                    .ToList();

                // Perform the crop operation using the CropStructure method
                if (!CropStructure(structureSet, createdStructure.Id, structuresToCrop, marginMM))
                {
                    Console.WriteLine($"Failed to crop structure: {createdStructure.Id}");
                    continue;
                }

                // Add the cropped structure to the result list
                croppedStructures.Add((originalId, newId, dose));
            }

            return croppedStructures;
        }




        /// <summary>
        /// Sets the color of the specified <see cref="Structure"/> to the given color.
        /// </summary>
        /// <param name="structure">The structure whose color will be updated.</param>
        /// <param name="color">The new color to assign to the structure.</param>
        /// <returns>
        /// <c>true</c> if the color was successfully set; otherwise, <c>false</c>.
        /// This method will return <c>false</c> for versions prior to V156.
        /// </returns>
        /// <remarks>
        /// The method updates the color property of the structure to the specified color.
        /// Compatibility with different versions is controlled using conditional compilation.
        /// </remarks>
        public static bool SetColorOfStructure(Structure structure, System.Windows.Media.Color color)
        {
#if !V156
            structure.Color = color;
            return true;
#else
 return false;
#endif
        }


        /// <summary>
        /// Generates a unique random structure ID that starts with the prefix "zz" and does not already exist
        /// in the specified <see cref="StructureSet"/>.
        /// </summary>
        /// <param name="length">
        /// The total length of the generated ID, including the "zz" prefix. Must be greater than 2.
        /// </param>
        /// <param name="structureSet">
        /// The <see cref="StructureSet"/> in which uniqueness will be enforced.
        /// </param>
        /// <returns>
        /// A randomly generated, unique ID string of the specified length that starts with "zz".
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the specified <paramref name="length"/> is less than or equal to 2,
        /// which is insufficient to include both the "zz" prefix and a random suffix.
        /// </exception>
        /// <remarks>
        /// This method ensures that the returned ID does not conflict with any existing structure IDs in the given structure set.
        /// </remarks>
        public static string GetRandomNonExistingStructureId(int length, StructureSet structureSet)
        {
            if (length <= 2)
                throw new ArgumentException("Length must be greater than 2 to accommodate the 'zz' prefix.", nameof(length));

            const string prefix = "zz";
            int suffixLength = length - prefix.Length;

            string candidateId;

            do
            {
                string randomSuffix = ESAPI_Helpers.GenerateRandomString(suffixLength);
                candidateId = prefix + randomSuffix;
            }
            while (structureSet.Structures.Any(s => s.Id.Equals(candidateId, StringComparison.OrdinalIgnoreCase)));

            return candidateId;
        }

        /// <summary>
        /// Generates the next available unique structure ID based on the specified root ID.
        /// Uses a helper method to ensure the generated ID does not conflict with any existing IDs in the structure set.
        /// </summary>
        /// <param name="structureSet">The structure set to check for existing structure IDs.</param>
        /// <param name="rootId">The base ID used to generate the new unique structure ID.</param>
        /// <returns>
        /// A unique structure ID that does not already exist in the structure set.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the structure set or root ID is null.
        /// </exception>
        /// <remarks>
        /// This method relies on `GenerateStructureIdWithPrefixSuffix` to create a unique ID using the root ID with empty prefix and suffix.
        /// </remarks>

        public static string GetNextAvailableStructureId(StructureSet structureSet, string rootId)
        {
            return GenerateStructureIdWithPrefixSuffix(structureSet, rootId, "", "");
        }


        /// <summary>
        /// Generates a unique structure ID using the specified root ID, prefix, and suffix, while adhering to a maximum length limit.
        /// If the generated ID already exists in the structure set, a numerical suffix is appended to ensure uniqueness.
        /// </summary>
        /// <param name="structureSet">The structure set to check for existing structure IDs.</param>
        /// <param name="rootId">The base ID used to generate the new structure ID.</param>
        /// <param name="prefix">The prefix to prepend to the root ID.</param>
        /// <param name="suffix">The suffix to append to the root ID.</param>
        /// <returns>
        /// A unique structure ID that does not already exist in the structure set.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the structure set, root ID, prefix, or suffix is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the root ID is empty or the combined prefix and suffix exceed the maximum allowable length.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a unique ID cannot be generated within the maximum length constraint.
        /// </exception>
        /// <remarks>
        /// - The total length of the generated ID, including the prefix, root ID, and suffix, must not exceed 16 characters.
        /// - The method ensures uniqueness by appending a numerical suffix (e.g., "_01", "_02") to the generated ID.
        /// - Case-insensitive checks are used to verify uniqueness within the structure set.
        /// </remarks>
        public static string GenerateStructureIdWithPrefixSuffix(
    StructureSet structureSet, string rootId, string prefix, string suffix)
        {
            const int MaxLength = 16;

            if (structureSet == null)
                throw new ArgumentNullException(nameof(structureSet), "StructureSet cannot be null.");
            if (string.IsNullOrWhiteSpace(rootId))
                throw new ArgumentException("Root ID cannot be null or empty.", nameof(rootId));
            if (prefix == null)
                throw new ArgumentException("Prefix cannot be null.", nameof(prefix));
            if (suffix == null)
                throw new ArgumentException("Suffix cannot be null.", nameof(suffix));

            // Combine prefix, rootId, and suffix while adhering to the length limitation
            string baseId = prefix + rootId + suffix;

            if (baseId.Length > MaxLength)
            {
                int truncateLength = MaxLength - (prefix.Length + suffix.Length);
                if (truncateLength < 0)
                    throw new ArgumentException("Combined prefix and suffix exceed maximum length.", nameof(prefix));

                baseId = prefix + rootId.Substring(0, truncateLength) + suffix;
            }

            // Check if the base structure ID already exists using GetStructureById
            if (RetrieveItems.StructureItems.GetStructureById(structureSet, baseId) == null)
            {
                return baseId; // The ID does not exist; return as is.
            }

            // Generate unique IDs with numerical suffix
            int counter = 1;
            string candidateId;
            do
            {
                string suffixString = counter.ToString("D2"); // Format as 2 digits
                int truncateLength = MaxLength - suffixString.Length - (prefix.Length + suffix.Length);
                if (truncateLength < 0)
                    throw new InvalidOperationException("Cannot generate unique ID within the length limit.");

                string truncatedRootId = rootId.Length > truncateLength ? rootId.Substring(0, truncateLength) : rootId;
                candidateId = prefix + truncatedRootId + suffix + suffixString;

                counter++;
            } while (RetrieveItems.StructureItems.GetStructureById(structureSet, candidateId) != null);

            return candidateId; // Return the new unique ID
        }


        /// <summary>
        /// Converts a single structure to high resolution.
        /// </summary>
        /// <param name="structure">The structure to convert.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool ConvertSingle(Structure structure)
        {
            return ConvertList(new List<Structure>() { structure });
        }

        /// <summary>
        /// Converts a list of structures to high resolution.
        /// </summary>
        /// <param name="Structures">List of structures to convert.</param>
        /// <returns>True if all conversions are successful, false if any fail.</returns>
        public static bool ConvertList(List<Structure> Structures)
        {
            foreach (var structure in Structures.Where(s => !s.IsHighResolution))
            {
                try
                {
                    structure.ConvertToHighResolution();
                }
                catch (Exception ex)
                {
                    // Log the exception and return false if conversion fails.
                    //logger.Info($"Could not convert {structure.Id} to high resolution: {ex}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Ensures that all structures in the provided list are high resolution.
        /// If a structure is not already high resolution, a temporary high-resolution structure is created
        /// and used for further operations. Temporary structures are marked and can be cleaned up later.
        /// </summary>
        /// <param name="structureSet">The structure set containing the structures to process.</param>
        /// <param name="structureIds">The list of structure IDs to check and convert to high resolution if necessary.</param>
        /// <returns>
        /// A tuple containing:
        /// - UpdatedStructures: The list of original and temporary high-resolution structures for further operations.
        /// - TemporaryStructures: The list of temporary structures created during the process.
        /// - Crosswalk: A dictionary mapping original structure IDs to their corresponding temporary structure IDs.
        /// </returns>
        /// <remarks>
        /// Temporary high-resolution structures are created with the same segment volume, color, and a comment
        /// indicating they are temporary instances of the original structure.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the structure set or the list of structure IDs is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the list of structure IDs is empty or contains invalid or duplicate IDs.
        /// </exception>
        public static (List<Structure> UpdatedStructures, List<Structure> TemporaryStructures, Dictionary<string, string> Crosswalk)
            EnsureHighResolution(StructureSet structureSet, List<string> structureIds)
        {
            // Validate input parameters
            if (structureSet == null)
                throw new ArgumentNullException(nameof(structureSet), "Structure set cannot be null.");

            if (structureIds == null || !structureIds.Any())
                throw new ArgumentException("Structure IDs list cannot be null or empty.", nameof(structureIds));

            if (structureIds.Count != structureIds.Distinct().Count())
                throw new ArgumentException("Structure IDs list contains duplicates.", nameof(structureIds));

            // Retrieve matching structures by ID
            var matchingResult = RetrieveItems.StructureItems.GetMatchingStructuresById(structureSet, structureIds);
            var structures = matchingResult.MatchedStructures;

            // Check if any IDs didn't match a structure
            if (structures.Count != structureIds.Count)
            {
                var missingIds = structureIds.Except(structures.Select(s => s.Id)).ToList();
                throw new ArgumentException($"The following structure IDs were not found in the structure set: {string.Join(", ", missingIds)}");
            }

            var updatedStructures = new List<Structure>();
            var temporaryStructures = new List<Structure>();
            var crosswalk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var structure in structures)
            {
                if (structure.IsHighResolution)
                {
                    updatedStructures.Add(structure);
                    crosswalk[structure.Id] = structure.Id; // Map to itself if already high resolution
                }
                else
                {
                    // Create a temporary high-resolution structure
                    var tempStructure = AddNewStructureWithRandomId(structureSet, "Organ", "EnsureHighRes, tempStructure");
                    tempStructure.SegmentVolume = structure.SegmentVolume;
                    SetColorOfStructure(tempStructure, structure.Color);
                    AddCommentToStructure(tempStructure, $"Temporary High Resolution instance of {structure.Id}.");
                    tempStructure.ConvertToHighResolution();

                    temporaryStructures.Add(tempStructure);
                    updatedStructures.Add(tempStructure);

                    // Map original structure ID to temporary structure ID
                    crosswalk[structure.Id] = tempStructure.Id;
                }
            }

            return (updatedStructures, temporaryStructures, crosswalk);
        }

        public static (List<Structure> UpdatedStructures, List<Structure> TemporaryStructures, Dictionary<string, string> Crosswalk) EnsureHighResolution(
            StructureSet structureSet, List<Structure> structures)
        {
            return EnsureHighResolution(structureSet, structures.Select(s => s.Id).ToList());
        }

        /// <summary>
        /// Removes specified structures from a base structure within the given structure set.
        /// Ensures all structures are converted to high resolution before performing Boolean subtraction
        /// and cleans up any temporary structures created during the process.
        /// </summary>
        /// <param name="structureSet">The structure set containing the base structure and structures to remove.</param>
        /// <param name="baseStructureId">The ID of the base structure from which other structures will be subtracted.</param>
        /// <param name="structureIdsToRemove">A list of structure IDs to subtract from the base structure.</param>
        /// <returns>
        /// True if all specified structures are successfully removed from the base structure; false if an error occurs.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the structure set or base structure ID is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the base structure ID is empty or the list of structures to remove is null or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the base structure does not exist in the structure set.
        /// </exception>

        public static bool RemoveStructures(StructureSet structureSet, string baseStructureId, List<string> structureIdsToRemove)
        {
            try
            {
                // Validate inputs
                if (structureSet == null)
                    throw new ArgumentNullException(nameof(structureSet), "StructureSet cannot be null.");
                if (string.IsNullOrWhiteSpace(baseStructureId))
                    throw new ArgumentException("Base structure ID cannot be null or empty.", nameof(baseStructureId));
                if (structureIdsToRemove == null || !structureIdsToRemove.Any())
                    throw new ArgumentException("List of structure IDs to remove cannot be null or empty.", nameof(structureIdsToRemove));

                // Get the base structure
                var baseStructure = RetrieveItems.StructureItems.GetStructureById(structureSet, baseStructureId);
                if (baseStructure == null)
                    throw new InvalidOperationException($"Base structure '{baseStructureId}' does not exist in the StructureSet.");

                // Get the structures to remove
                var result = RetrieveItems.StructureItems.GetMatchingStructuresById(structureSet, structureIdsToRemove);
                var structuresToRemove = result.MatchedStructures;
                var missingStructures = result.MissingStructureIds;

                if (missingStructures.Any())
                {
                    //logger.Info("Warning: The following structures were not found and will not be removed:");
                    //foreach (var missingId in missingStructures)
                    //    logger.Info($"- {missingId}");
                }

                // Ensure all structures (base and to remove) are high resolution
                var highResConversion = EnsureHighResolution(structureSet, new List<Structure> { baseStructure }.Concat(structuresToRemove).ToList());
                var updatedStructures = highResConversion.UpdatedStructures;
                var temporaryStructures = highResConversion.TemporaryStructures;

                //This is probably redudant, but conver the base structure to high resolution if necessary.
                if (updatedStructures.Concat(temporaryStructures).Any(x => x.IsHighResolution))
                {
                    ConvertSingle(baseStructure);
                }

                //We may not be able to use the original structures because of different geometries.
                //The ensur resolution method provides a crosswalk, which must be applied.
                List<Structure> crosswalkedStructuresToRemove = new List<Structure>();
                foreach (var structure in structuresToRemove)
                {
                    var crosswalk = highResConversion.Crosswalk;
                    var crossWalkedId = crosswalk.First(x => x.Key.ToUpper() == structure.Id.ToUpper()).Value;
                    crosswalkedStructuresToRemove.Add(RetrieveItems.StructureItems.GetStructureById(structureSet, crossWalkedId));
                }

                // Perform subtraction for each structure
                foreach (var structure in crosswalkedStructuresToRemove)
                {
                    baseStructure.SegmentVolume = baseStructure.Sub(structure);
                }

                // Clean up temporary structures
                foreach (var tempStructure in temporaryStructures)
                {
                    structureSet.RemoveStructure(tempStructure);
                }

                return true; // Successfully removed all matched structures
            }
            catch (Exception ex)
            {
                //logger.Info($"Error in RemoveStructures: {ex.Message}");
                return false; // Return false if an error occurs
            }
        }


        /// <summary>
        /// Checks if a structure with the specified ID exists in the given <see cref="StructureSet"/>.
        /// If the structure exists, it is removed and replaced with a new structure of the same ID and DICOM type.
        /// </summary>
        /// <param name="structureSet">The structure set to check for the structure's existence and to add a new structure.</param>
        /// <param name="StructureId">The ID of the structure to check, remove, and recreate.</param>
        /// <returns>
        /// A tuple containing:
        /// <c>Success</c> - <c>true</c> if the operation succeeds; otherwise, <c>false</c>.
        /// <c>CreatedStructure</c> - The newly created <see cref="Structure"/> object, or <c>null</c> if the operation fails.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="structureSet"/> is <c>null</c> or <paramref name="StructureId"/> is <c>null</c> or whitespace.
        /// </exception>
        /// <remarks>
        /// If the structure already exists, it is removed before creating a new one with the same ID and resolved DICOM type.
        /// The color of the new structure is set to match the removed structure (if applicable), with a default color of LightGoldenrodYellow.
        /// </remarks>

        public static (bool Success, Structure CreatedStructure) CheckExistenceByIdRemoveAndCreateNew(StructureSet structureSet, string StructureId)
        {

            if (structureSet == null || string.IsNullOrWhiteSpace(StructureId))
            {
                //logger.Info("Error: Invalid input parameters.");
                return (false, null);
            }

            try
            {
                string DICOMtype = "Organ"; // Default to "Organ" if DICOM type is not available
                System.Windows.Media.Color structureColor = System.Windows.Media.Colors.LightGoldenrodYellow;

                // Check if the structure exists
                var existingStructure = RetrieveItems.StructureItems.GetStructureById(structureSet, StructureId);
                if (existingStructure != null)
                {
                    // Retrieve the DICOM type of the existing structure
                    DICOMtype = !string.IsNullOrWhiteSpace(existingStructure.DicomType) ? existingStructure.DicomType : "Organ";
                    structureColor = existingStructure.Color;
                    try
                    {
                        structureSet.RemoveStructure(existingStructure);
                    }
                    catch (Exception ex)
                    {
                        //logger.Info($"Error removing structure: {ex.Message}");
                        return (false, null);
                    }
                }

                // Create the new structure with the same ID and resolved DICOM type
                var newStructure = structureSet.AddStructure(DICOMtype, StructureId);
                SetColorOfStructure(newStructure, structureColor);

                return (true, newStructure);
            }
            catch (Exception ex)
            {
                //logger.Info($"Error during structure operation: {ex.Message}");
                return (false, null);
            }
        }


        /// <summary>
        /// Adds a comment to the specified structure.
        /// </summary>
        /// <param name="structure">The structure to which the comment will be added.</param>
        /// <param name="comment">The comment text to add to the structure.</param>
        /// <param name="append">If true, the existing comment will be maintained and the new will be appendeded.</param>
        /// <returns>
        /// Returns <c>true</c> if the comment was successfully added; otherwise, <c>false</c>.
        /// The operation will return <c>false</c> for versions prior to V156.
        /// </returns>

        public static bool AddCommentToStructure(Structure structure, string comment, bool append)
        {
#if !V156
            try
            {
                if (append)
                {
                    structure.Comment = $"{structure.Comment}\n{comment}";
                }
                else
                {
                    structure.Comment = comment;
                }
            }
            catch (Exception ex)
            {
               //logger.Info($"Could not add comment to {structure.Id}: {ex.Message}");
            }
            return true;
#else
 return false;
#endif
        }

        public static bool AddCommentToStructure(Structure structure, string comment)
        {
            return AddCommentToStructure(structure, comment, false);
        }

        /// <summary>
        /// Retrieves a list of target structures that have a lower optimization objective in the given treatment plan.
        /// </summary>
        /// <param name="plan">The ExternalPlanSetup object containing the treatment plan.</param>
        /// <returns>
        /// A list of structures that have an optimization objective with the "Lower" operator.
        /// Returns an empty list if no matching structures are found or if the plan is invalid.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided plan is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the plan does not have an optimization setup.</exception>
        public static List<Structure> GetTargetsByLowerObjective(ExternalPlanSetup plan)
        {
            // Validate input
            if (plan == null)
                throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");

            if (plan.OptimizationSetup == null)
                throw new InvalidOperationException("The given plan does not have an OptimizationSetup.");

            try
            {
                // Retrieve structures that have a lower optimization objective
                var targetStructures = plan.OptimizationSetup.Objectives
                    .Where(x => x.Operator == OptimizationObjectiveOperator.Lower && x.Structure != null)
                    .Select(x => x.Structure)
                    .Distinct() // Ensure unique structures in the result
                    .ToList();

                return targetStructures;
            }
            catch (Exception ex)
            {
                //logger.Info($"Error retrieving target structures with a lower objective: {ex.Message}");
                return new List<Structure>(); // Return an empty list on failure
            }
        }

        /// <summary>
        /// Retrieves a list of organ-at-risk (OAR) structures that do not have a lower optimization objective.
        /// </summary>
        /// <param name="plan">The ExternalPlanSetup object containing the treatment plan.</param>
        /// <returns>
        /// A list of OAR structures that do not have a lower optimization objective.
        /// Returns an empty list if the plan is invalid or contains no matching structures.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided plan is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the plan does not have an OptimizationSetup.</exception>
        public static List<Structure> GetOARbyNoLowerObjective(ExternalPlanSetup plan)
        {
            // Validate input
            if (plan == null)
                throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");

            if (plan.OptimizationSetup == null)
                throw new InvalidOperationException("The given plan does not have an OptimizationSetup.");

            try
            {
                // Get the list of target structure IDs with a lower objective
                var targetIds = GetTargetsByLowerObjective(plan)
                    .Where(x => x != null) // Ensure no null structures
                    .Select(x => x.Id)
                    .Distinct() // Ensure unique IDs
                    .ToList();

                // Retrieve structures that are NOT in the target list
                var oarStructures = plan.OptimizationSetup.Objectives
                    .Where(x => x.Structure != null) // Ensure structure is not null
                    .Select(x => x.Structure)
                    .Where(x => !targetIds.Contains(x.Id)) // Exclude target structures
                    .Distinct() // Ensure unique structures
                    .ToList();

                return oarStructures;
            }
            catch (Exception ex)
            {
                //logger.Info($"Error retrieving OAR structures without a lower objective: {ex.Message}");
                return new List<Structure>(); // Return an empty list on failure
            }
        }
    }
}

