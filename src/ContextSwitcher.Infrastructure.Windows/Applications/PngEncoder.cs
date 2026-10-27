using System.IO.Compression;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// Writes a picture as a PNG: what the app picker shows is a PNG file, and Windows hands an icon over
/// as pixels. Eight bits of red, green, blue and alpha, every row unfiltered, deflated by .NET - the
/// plainest PNG there is, which every reader takes.
/// </summary>
public static class PngEncoder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>The PNG of a picture whose pixels are blue, green, red and alpha, a row after another from the top.</summary>
    /// <param name="width">Its width in pixels; at least 1.</param>
    /// <param name="height">Its height in pixels; at least 1.</param>
    /// <param name="bgra">Four bytes for each pixel, <paramref name="width"/> × <paramref name="height"/> of them, alpha not premultiplied.</param>
    public static byte[] FromBgra(int width, int height, ReadOnlySpan<byte> bgra)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (bgra.Length != checked(width * height * 4))
        {
            throw new ArgumentException("The pixels don't fill a picture of that size.", nameof(bgra));
        }

        // Each row starts with its filter: 0, none. Then its pixels as red, green, blue, alpha.
        byte[] rows = new byte[checked(height * (1 + (width * 4)))];
        int next = 0;
        for (int y = 0; y < height; y++)
        {
            rows[next++] = 0;
            for (int x = 0; x < width; x++)
            {
                int pixel = ((y * width) + x) * 4;
                rows[next++] = bgra[pixel + 2];
                rows[next++] = bgra[pixel + 1];
                rows[next++] = bgra[pixel];
                rows[next++] = bgra[pixel + 3];
            }
        }

        using MemoryStream png = new();
        png.Write(Signature);

        byte[] header = new byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);

        using MemoryStream deflated = new();
        using (ZLibStream compressor = new(deflated, CompressionLevel.Optimal, leaveOpen: true))
        {
            compressor.Write(rows);
        }

        WriteChunk(png, "IDAT", deflated.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>
    /// Turns alpha that is multiplied into the colours - how Windows draws an icon - into plain alpha,
    /// in place, as a PNG holds it. A pixel with no alpha has no colour to give back.
    /// </summary>
    public static void Unpremultiply(Span<byte> bgra)
    {
        for (int pixel = 0; pixel + 3 < bgra.Length; pixel += 4)
        {
            int alpha = bgra[pixel + 3];
            if (alpha is 0 or 255)
            {
                continue;
            }

            for (int channel = 0; channel < 3; channel++)
            {
                bgra[pixel + channel] = (byte)Math.Min(255, ((bgra[pixel + channel] * 255) + (alpha / 2)) / alpha);
            }
        }
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = 0xFFFFFFFF;
        crc = Update(crc, typeBytes);
        crc = Update(crc, data);
        byte[] checksum = new byte[4];
        WriteBigEndian(checksum, 0, crc ^ 0xFFFFFFFF);
        stream.Write(checksum);
    }

    private static uint Update(uint crc, byte[] bytes)
    {
        foreach (byte value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int bit = 0; bit < 8; bit++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
