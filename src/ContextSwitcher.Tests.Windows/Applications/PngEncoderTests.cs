using System.IO.Compression;
using ContextSwitcher.Infrastructure.Windows.Applications;

namespace ContextSwitcher.Tests.Windows.Applications;

/// <summary>The PNG an icon is kept as is a PNG any reader takes: read back here chunk by chunk, with the checksums.</summary>
public sealed class PngEncoderTests
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void ThePictureComesBackAsItWentIn()
    {
        // Two pixels by two: blue, green / red, translucent white - as blue, green, red, alpha.
        byte[] bgra =
        [
            255, 0, 0, 255, 0, 255, 0, 255,
            0, 0, 255, 255, 255, 255, 255, 128
        ];

        PngFile png = Read(PngEncoder.FromBgra(2, 2, bgra));

        Assert.Equal((2u, 2u), (png.Width, png.Height));
        Assert.Equal(8, png.BitDepth);
        Assert.Equal(6, png.ColourType);
        Assert.Equal<byte>(
            [
                0, 0, 0, 255, 255, 0, 255, 0, 255,
                0, 255, 0, 0, 255, 255, 255, 255, 128
            ],
            png.Rows);
    }

    [Fact]
    public void EveryChunkHasItsChecksumAndTheFileEndsWhereItShould()
    {
        byte[] file = PngEncoder.FromBgra(3, 1, new byte[12]);

        Assert.Equal(Signature, file[..8]);
        List<string> types = [];
        int position = 8;
        while (position < file.Length)
        {
            int length = (int)ReadUInt32(file, position);
            string type = System.Text.Encoding.ASCII.GetString(file, position + 4, 4);
            uint stored = ReadUInt32(file, position + 8 + length);
            Assert.Equal(Crc32(file.AsSpan(position + 4, length + 4)), stored);
            types.Add(type);
            position += 12 + length;
        }

        Assert.Equal(file.Length, position);
        Assert.Equal(["IHDR", "IDAT", "IEND"], types);
    }

    [Fact]
    public void ABiggerPictureKeepsEveryRowInOrder()
    {
        const int width = 5;
        const int height = 7;
        byte[] bgra = new byte[width * height * 4];
        for (int i = 0; i < bgra.Length; i++)
        {
            bgra[i] = (byte)((i * 7) % 251);
        }

        PngFile png = Read(PngEncoder.FromBgra(width, height, bgra));

        for (int y = 0; y < height; y++)
        {
            Assert.Equal(0, png.Rows[y * ((width * 4) + 1)]);
            for (int x = 0; x < width; x++)
            {
                int source = ((y * width) + x) * 4;
                int target = (y * ((width * 4) + 1)) + 1 + (x * 4);
                Assert.Equal<byte>([bgra[source + 2], bgra[source + 1], bgra[source], bgra[source + 3]], png.Rows[target..(target + 4)]);
            }
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-3, 2)]
    public void APictureWithNoPixelsIsRefused(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.FromBgra(width, height, new byte[16]));
    }

    [Fact]
    public void PixelsThatDoNotFillThePictureAreRefused()
    {
        Assert.Throws<ArgumentException>(() => PngEncoder.FromBgra(2, 2, new byte[15]));
        Assert.Throws<ArgumentException>(() => PngEncoder.FromBgra(2, 2, new byte[17]));
    }

    /// <summary>Windows draws an icon with its colours already multiplied by its alpha; a PNG holds them plain.</summary>
    [Fact]
    public void AlphaMultipliedIntoTheColoursIsTakenOut()
    {
        // Half-transparent white as Windows draws it is 128 128 128 128; plain, it is 255 255 255 128.
        byte[] pixels = [128, 128, 128, 128, 10, 20, 30, 255, 99, 99, 99, 0];

        PngEncoder.Unpremultiply(pixels);

        Assert.Equal<byte>([255, 255, 255, 128, 10, 20, 30, 255, 99, 99, 99, 0], pixels);
    }

    [Fact]
    public void ColoursNeverOverflowWhateverTheAlpha()
    {
        byte[] pixels = [200, 200, 200, 10];

        PngEncoder.Unpremultiply(pixels);

        Assert.Equal<byte>([255, 255, 255, 10], pixels);
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]);

    /// <summary>The checksum PNG uses (CRC-32), worked out bit by bit here so as not to share the encoder's table.</summary>
    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static PngFile Read(byte[] file)
    {
        Assert.Equal(Signature, file[..8]);
        uint width = ReadUInt32(file, 16);
        uint height = ReadUInt32(file, 20);
        byte depth = file[24];
        byte colour = file[25];

        using MemoryStream compressed = new();
        int position = 8;
        while (position < file.Length)
        {
            int length = (int)ReadUInt32(file, position);
            if (System.Text.Encoding.ASCII.GetString(file, position + 4, 4) == "IDAT")
            {
                compressed.Write(file, position + 8, length);
            }

            position += 12 + length;
        }

        compressed.Position = 0;
        using ZLibStream inflater = new(compressed, CompressionMode.Decompress);
        using MemoryStream rows = new();
        inflater.CopyTo(rows);
        return new PngFile(width, height, depth, colour, rows.ToArray());
    }

    private sealed record PngFile(uint Width, uint Height, int BitDepth, int ColourType, byte[] Rows);
}
