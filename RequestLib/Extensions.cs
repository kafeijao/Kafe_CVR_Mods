using System.Text.RegularExpressions;
using System.Web;

namespace Kafe.RequestLib;

internal static class Extensions
{
    /// <summary>
    /// Sanitizes the string input to be ready to display on an HTML page
    /// </summary>
    public static string SanitizeForHtml(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // First, HTML encode the string to convert special characters to their HTML entity equivalents
        string encoded = HttpUtility.HtmlEncode(input);

        // Then, use regex to remove any potentially dangerous HTML tags
        string removedTags = Regex.Replace(encoded, @"<.*?>", string.Empty);

        return removedTags;
    }
}
