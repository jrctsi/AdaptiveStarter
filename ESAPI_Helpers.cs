using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using static VMS.TPS.Common.Model.Types.DoseValue;

namespace AdaptiveStarter
{
    //MCB
    public static class ESAPI_Helpers
    {
        private static readonly Random rnd = new Random();

        /// <summary>
        /// Lightens a given color by a specified factor (normal factor is ~0.5).
        /// </summary>
        /// <param name="color">The original color to lighten.</param>
        /// <param name="factor">
        /// A value between 0 and 1 that determines the degree of lightening.
        /// 0 returns the original color, while 1 returns white.
        /// The normal value is approximately 0.5.
        /// </param>
        /// <returns>
        /// A new color that is a lighter shade of the original color.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the factor is less than 0 or greater than 1.
        /// </exception>
        /// <remarks>
        /// - The method calculates the lighter color by increasing the RGB components of the original color 
        ///   proportionally toward the maximum value (255) based on the specified factor.
        /// - This method does not affect the alpha (transparency) component of the color.
        /// </remarks>

        public static System.Windows.Media.Color LightenColor(System.Windows.Media.Color color, double factor)
        {
            if (factor < 0 || factor > 1)
                throw new ArgumentOutOfRangeException(nameof(factor), "Factor must be between 0 and 1.");

            // Decompose the color into its RGB components
            byte r = color.R;
            byte g = color.G;
            byte b = color.B;

            // Lighten each component proportionally
            byte newR = (byte)(r + (255 - r) * factor);
            byte newG = (byte)(g + (255 - g) * factor);
            byte newB = (byte)(b + (255 - b) * factor);

            // Return the new lighter color
            return System.Windows.Media.Color.FromRgb(newR, newG, newB);
        }


        /// <summary>
        /// Generates a random alphanumeric string of the specified length.
        /// </summary>
        /// <param name="length">The desired length of the generated string.</param>
        /// <returns>
        /// A random string consisting of uppercase letters, lowercase letters, and digits.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the specified length is less than or equal to zero.
        /// </exception>
        /// <remarks>
        /// - The method uses a predefined set of valid characters: uppercase letters, lowercase letters, and digits.
        /// - A random character is selected for each position in the string, ensuring uniform distribution of characters.
        /// - The resulting string has no special characters or spaces.
        /// </remarks>


        public static string GenerateRandomString(int length)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";

            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(validChars[rnd.Next(validChars.Length)]);
            }

            return sb.ToString();
        }



        public static bool IsGy { get; set; } = false;

        /// <summary>
        /// Determines and sets the dose unit based on the IsGy flag.
        /// </summary>
        /// <returns>
        /// Returns <see cref="VMS.TPS.Common.Model.Types.DoseValue.DoseUnit.Gy"/> if IsGy is true; 
        /// otherwise, returns <see cref="VMS.TPS.Common.Model.Types.DoseValue.DoseUnit.cGy"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the IsGy flag is null or undefined.
        /// </exception>
        public static VMS.TPS.Common.Model.Types.DoseValue.DoseUnit SetDoseUnit()
        {
            try
            {
                // Validate the IsGy flag to ensure it is defined
                if (!IsGy.GetType().Equals(typeof(bool)))
                {
                    throw new InvalidOperationException("IsGy must be a boolean value.");
                }

                // Determine and return the appropriate dose unit based on IsGy
                if (IsGy)
                {
                    //Console.WriteLine("SetDoseUnit requested.  Returned Gy.");
                    return VMS.TPS.Common.Model.Types.DoseValue.DoseUnit.Gy;
                }
                else
                {
                    //Console.WriteLine("SetDoseUnit requested.  Returned cGy.");
                    return VMS.TPS.Common.Model.Types.DoseValue.DoseUnit.cGy;
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as necessary
                Console.WriteLine($"Error in SetDoseUnit: {ex.Message}");
                throw; // Re-throw the exception to propagate it to the caller
            }
        }

        /// <summary>
        /// Retrieves the dose unit from the specified external plan setup.
        /// </summary>
        /// <param name="plan">The external plan setup from which the dose unit will be retrieved.</param>
        /// <returns>The dose unit of the plan.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the plan is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the DosePerFraction property is not set (null).</exception>
        public static DoseUnit GetDoseUnit(ExternalPlanSetup plan)
        {
            if (plan == null)
            {
                Console.WriteLine("Error: Plan is null.");
                throw new ArgumentNullException(nameof(plan), "Plan cannot be null.");
            }

            try
            {
                // Ensure DosePerFraction is set
                if (plan.DosePerFraction == null)
                {
                    Console.WriteLine("Error: DosePerFraction property is not set for the plan.");
                    throw new InvalidOperationException("DosePerFraction property is null. Unable to retrieve the dose unit.");
                }

                // Retrieve and return the dose unit
                return plan.DosePerFraction.Unit;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error while retrieving dose unit: {ex.Message}");
                throw new InvalidOperationException("An error occurred while retrieving the dose unit.", ex);
            }
        }

    }
}
