using System.Text.RegularExpressions;

namespace Lotv.Core.Models;

/// <summary>CSV cell formatting for exports that open in Excel or import into a CRM.</summary>
public static class CsvExport
{
    private static readonly Regex PhoneLike = new(@"^[+\-]?[\d\s().\-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Quotes the value when needed and neutralises spreadsheet formulas: public form
    /// input ends up in these files, and a cell starting with = + - or @ would
    /// otherwise run as a formula when someone opens it in Excel. Phone numbers
    /// (which legitimately start with + or -) are left alone.
    /// </summary>
    public static string Cell(string? value)
    {
        var v = (value ?? "").Trim();
        if (v.Length > 0)
        {
            var first = v[0];
            var formula = first is '=' or '@' or '\t' or '\r'
                          || (first is '+' or '-' && !PhoneLike.IsMatch(v));
            if (formula) v = "'" + v;
        }
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
}
