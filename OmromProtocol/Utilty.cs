using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmromProtocol
{
    public static class Utilty
    {
        /// <summary>
        /// Converts a byte array to a float value by rearranging bytes and using BitConverter.
        /// </summary>
        /// <param name="input">The input byte array containing 4 bytes</param>
        /// <returns>A float value converted from the byte array</returns>
        public static float ToFloat(byte[] input)
        {
            byte[] newArray = new byte[] { input[2], input[3], input[0], input[1] };
            return BitConverter.ToSingle(newArray, 0);
        }

        public static float ToFloat(byte[] input, int high, int low)
        {
            high *= 2;
            low *= 2;
            byte[] newArray = new byte[] { input[low + 1], input[low], input[high + 1], input[high] };
            return BitConverter.ToSingle(newArray, 0);
        }

        /// <summary>
        /// Converts a byte array to a 16-bit integer (word).
        /// </summary>
        /// <param name="input">The input byte array containing 2 bytes</param>
        /// <returns>A 16-bit integer converted from the byte array</returns>
        public static int ToWord(byte[] input, bool reverse = false)
        {
            if (reverse) return BitConverter.ToInt16(new byte[] { input[1], input[0] }, 0);
            else return BitConverter.ToInt16(new byte[] { input[0], input[1] }, 0);
        }

        /// <summary>
        /// Converts an integer value to a hexadecimal string representation.
        /// </summary>
        /// <param name="value">The integer value to convert</param>
        /// <returns>A string containing the hexadecimal representation</returns>
        public static string ToHexToString(int value)
        {
            byte[] bytes = BitConverter.GetBytes((ushort)value);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return string.Join(string.Empty, bytes.Select(b => ((char)b).ToString()));
        }

        public static string ConvertHexToAscii(int hexValue)
        {
            byte[] bytes = BitConverter.GetBytes(hexValue);
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        }

        /// <summary>
        /// Converts a byte array to a 32-bit integer.
        /// </summary>
        /// <param name="input">The input byte array containing 4 bytes</param>
        /// <returns>A 32-bit integer converted from the byte array</returns>
        public static int ToWord32(byte[] input)
        {
            byte[] newArray = new byte[] { input[0], input[1], input[2], input[3] };
            return BitConverter.ToInt32(newArray, 0);
        }

        public static int ReadWord(byte[] data, int index, bool reverse = false)
        {
            byte[] result = new byte[] { data[index * 2], data[(index * 2) + 1] };
            return ToWord(result, reverse);
        }

        public static string ReadText(byte[] data, int startIndex, int endIndex, bool reverse = false)
        {
            string result = string.Empty;

            startIndex *= 2;
            endIndex *= 2;

            for (int i = 0; i < endIndex; i++)
            {
                int index = startIndex + (i * 2);
                string chunk = ConvertHexToAscii(ToWord(new byte[] { data[index + 1], data[index] }, reverse));
                if (chunk != "\0" || chunk != "\0\0") result += chunk;
            }

            return result.ToString().Trim('\0');
        }


        /// <summary>
        /// Gets the current date and time formatted according to specified patterns and culture.
        /// </summary>
        /// <param name="datePattarn">The pattern to format the date</param>
        /// <param name="timePattarn">The pattern to format the time</param>
        /// <param name="culture">The culture code to use for formatting (defaults to "en-US")</param>
        /// <returns>A tuple containing the formatted date and time strings</returns>
        /// <exception cref="Exception">Thrown when date/time formatting fails</exception>
        public static (string date, string time) GetDateTime(string datePattarn, string timePattarn, string culture = "en-US")
        {
            try
            {
                return (
                        DateTime.Now.ToString(datePattarn, new System.Globalization.CultureInfo(culture)),
                        DateTime.Now.ToString(timePattarn, new System.Globalization.CultureInfo(culture))
                    );
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get date time: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the size of a directory in megabytes.
        /// </summary>
        /// <param name="folderPath">The path to the directory</param>
        /// <returns>The size of the directory in megabytes, or -1 if the directory does not exist</returns>
        public static double GetDirectorySizeInMB(string folderPath)
        {
            var directorySize = GetDirectorySize(folderPath);
            if (directorySize > 0) return directorySize / (1024.0 * 1024.0);
            return -1;
        }

        /// <summary>
        /// Gets the size of a directory in bytes.
        /// </summary>
        /// <param name="folderPath">The path to the directory</param>
        /// <returns>The size of the directory in bytes</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist</exception>
        /// <exception cref="ArgumentNullException">Thrown when the directory path is null</exception>
        private static long GetDirectorySize(string folderPath)
        {
            DirectoryInfo dirInfo;
            try
            {
                dirInfo = new DirectoryInfo(folderPath);
                if (!dirInfo.Exists)
                {
                    throw new DirectoryNotFoundException($"Directory not found: {folderPath}");
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new DirectoryNotFoundException($"Directory not found: {ex.Message}");
            }
            catch (ArgumentNullException ex)
            {
                throw new ArgumentNullException($"Directory not found: {ex.Message}");
            }

            long totalSize = 0;

            FileInfo[] files = dirInfo.GetFiles();
            foreach (FileInfo file in files)
            {
                totalSize += file.Length;
            }

            DirectoryInfo[] directories = dirInfo.GetDirectories();
            foreach (DirectoryInfo directory in directories)
            {
                totalSize += GetDirectorySize(directory.FullName);
            }

            return totalSize;
        }

        public static bool IsRunningAsAdministrator()
        {
            try
            {
                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to check if running as administrator: {ex.Message}");
            }
        }
    }
}
