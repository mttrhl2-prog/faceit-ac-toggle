using System.Text;

namespace FaceitSwitch.Core.Services;

/// <summary>Decodes redirected Windows command output without depending on the user's .NET culture.</summary>
public static class ProcessOutputDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    static ProcessOutputDecoder() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static string Decode(ReadOnlySpan<byte> bytes, int oemCodePage)
    {
        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) return Encoding.UTF8.GetString(bytes[3..]);
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE })) return Encoding.Unicode.GetString(bytes[2..]);
        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF })) return Encoding.BigEndianUnicode.GetString(bytes[2..]);

        // Some Windows tools redirect UTF-16 without a BOM. ASCII BCD keys make byte parity detectable.
        if (bytes.Length >= 4 && bytes.Length % 2 == 0)
        {
            var evenZeros = 0;
            var oddZeros = 0;
            for (var i = 0; i < bytes.Length; i += 2)
            {
                if (bytes[i] == 0) evenZeros++;
                if (bytes[i + 1] == 0) oddZeros++;
            }
            var pairs = bytes.Length / 2;
            if (oddZeros > pairs / 3 && evenZeros <= pairs / 10) return Encoding.Unicode.GetString(bytes);
            if (evenZeros > pairs / 3 && oddZeros <= pairs / 10) return Encoding.BigEndianUnicode.GetString(bytes);
        }

        try { return StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException) { return Encoding.GetEncoding(oemCodePage).GetString(bytes); }
    }
}
