using System.Text;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed record UiPathFileEncoding(Encoding Encoding, bool HasBom);

public static class UiPathFileEncodingDetector
{
    public static UiPathFileEncoding Detect(string filePath)
    {
        var buffer = new byte[Math.Min(4, new FileInfo(filePath).Length)];
        using (var stream = File.OpenRead(filePath))
        {
            _ = stream.Read(buffer, 0, buffer.Length);
        }

        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return new UiPathFileEncoding(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), HasBom: true);
        }

        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            return new UiPathFileEncoding(Encoding.Unicode, HasBom: true);
        }

        if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            return new UiPathFileEncoding(Encoding.BigEndianUnicode, HasBom: true);
        }

        return new UiPathFileEncoding(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), HasBom: false);
    }
}
