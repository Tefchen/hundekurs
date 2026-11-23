using System.Text;
using Hundekurs.Planung;

namespace Hundekurs.Web.Services;

/// <summary>
/// Erzeugt einen ungesendeten Mailentwurf (.eml). Outlook öffnet ihn dank „X-Unsent: 1“ als bearbeitbaren Entwurf.
/// Ob die BCC-Zeile übernommen wird, hängt vom Mailprogramm ab – im Zweifel die Empfänger kopieren.
/// </summary>
public static class MailEntwurf
{
    public static byte[] Erzeugen(Mailinhalt mail, IEnumerable<Kontakt> empfaenger)
    {
        var sb = new StringBuilder();
        sb.Append("X-Unsent: 1\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append($"Subject: {Kodieren(mail.Betreff)}\r\n");
        var adressen = empfaenger.Select(k => k.EMail).ToList();
        if (adressen.Count > 0) sb.Append($"Bcc: {string.Join(",\r\n ", adressen)}\r\n");
        sb.Append("Content-Type: text/plain; charset=utf-8\r\n");
        sb.Append("Content-Transfer-Encoding: 8bit\r\n");
        sb.Append("\r\n");
        sb.Append(mail.Text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(sb.ToString());
    }

    /// <summary>Betreff mit Umlauten nach RFC 2047.</summary>
    private static string Kodieren(string text) =>
        text.All(c => c < 128) ? text : $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}?=";
}
