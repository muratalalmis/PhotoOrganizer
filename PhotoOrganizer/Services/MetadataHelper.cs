using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhotoOrganizer.Services
{
    internal static class MetadataHelper
    {
        private static readonly Regex _regexPattern = new Regex(":");

        internal static DateTime? GetPhotoTaken(this string fileName)
        {
            try
            {
                // https://stackoverflow.com/questions/180030/how-can-i-find-out-when-a-picture-was-actually-taken-in-c-sharp-running-on-vista
                using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                using (var image = Image.FromStream(fileStream, false, false))
                {
                    var propItem = image.GetPropertyItem(36867);
                    var dateString = Encoding.UTF8.GetString(propItem.Value);

                    return Parse(dateString);
                }
            }
            catch
            {
                try
                {
                    var dateTag = ImageMetadataReader.ReadMetadata(fileName)
                        .SelectMany(x => x.Tags)
                        .FirstOrDefault(o => o.Name == "Date/Time");

                    return Parse(dateTag.Description);
                }
                catch (Exception)
                {
                    return default;
                }
            }
        }

        private static DateTime Parse(string date)
        {
            return DateTime.Parse(_regexPattern.Replace(date, "-", 2));
        }
    }
}
