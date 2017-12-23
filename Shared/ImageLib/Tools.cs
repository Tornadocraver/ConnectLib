using System;
using System.IO;

namespace ImageLib.Tools
{
    public static class Utilities
    {
        #region Image Serialization
        public static string ImageTostring(this MemoryStream image)
        {
            if (image == null)
                return null;
            return Convert.ToBase64String(image.ToArray());
        }
        public static MemoryStream ToImageStream(this string input)
        {
            if (input == null)
                return null;
            return new MemoryStream(Convert.FromBase64String(input));
        }
        #endregion
    }
}