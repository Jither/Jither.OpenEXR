using Jither.OpenEXR.Attributes;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Rectangle = System.Drawing.Rectangle;

namespace Jither.OpenEXR.Compression;

public class PizCompressor : Compressor
{
    private const int USHORT_RANGE = 1 << 16;
    private const int BITMAP_SIZE = USHORT_RANGE >> 3;
    public override int ScanLinesPerBlock => 32;

    // PIZ stream layout:
    // - ushort minNonZeroIndex
    // - ushort maxNonZeroIndex
    // - byte[minNonZeroIndex .. maxNonZeroIndex] bitmap
    // - compressed data

    private class ChannelInfo
    {
        public int YSampling { get; }
        public V2i Resolution { get; }
        public int UShortsPerPixel { get; }
        public int UShortsPerLine { get; }
        public int BytesPerLine { get; }
        public int UShortsTotal { get; }
        public int StartUShortOffset { get; set; }
        public int NextScanLineByteOffset { get; set; }
        public int NextScanLineUShortOffset { get; set; }

        public ChannelInfo(Channel channel, Rectangle bounds)
        {
            YSampling = channel.YSampling;
            Resolution = channel.GetSubsampledResolution(new V2i(bounds.Width, bounds.Height));
            UShortsPerPixel = channel.Type.GetBytesPerPixel() / 2;
            UShortsPerLine =  Resolution.X * UShortsPerPixel;
            BytesPerLine = UShortsPerLine * 2;
            UShortsTotal = Resolution.Area * UShortsPerPixel;
        }
    }

    public override CompressionResult InternalCompress(Stream source, Stream dest, PixelDataInfo info)
    {
        byte[] uncompressedScanlineBytes = new byte[info.UncompressedByteSize];
        source.ReadExactly(uncompressedScanlineBytes);

        var channelInfos = CreateChannelInfos(info);

        // PIZ uncompressed data should store all scanlines for each channel consecutively -
        // e.g. A for all scanlines, followed by B for all scanlines, followed by G, followed by R.
        // Here we do the rearrangement:
        var uncompressedArray = ArrayPool<ushort>.Shared.Rent(info.UncompressedByteSize / 2);
        try
        {
            // The array may (mostly will) not be the size of the data. We use a span over the exact for convenience
            var uncompressed = uncompressedArray.AsSpan(0, uncompressedScanlineBytes.Length / 2);

            int nextByteOffset = 0;
            for (int y = info.Bounds.Top; y < info.Bounds.Bottom; y++)
            {
                foreach (var channel in channelInfos)
                {
                    if (y % channel.YSampling != 0)
                    {
                        continue;
                    }
                    var byteSize = channel.BytesPerLine;
                    var ushortSize = channel.UShortsPerLine;
                    var channelScanline = MemoryMarshal.Cast<byte, ushort>(uncompressedScanlineBytes.AsSpan(nextByteOffset, byteSize));
                    var resultSpan = uncompressed.Slice(channel.NextScanLineUShortOffset, ushortSize);
                    channel.NextScanLineUShortOffset += ushortSize;
                    channelScanline.CopyTo(resultSpan);
                    nextByteOffset += byteSize;
                }
            }

            (var bitmap, var minNonZero, var maxNonZero) = BitmapFromData(uncompressed);
            (var lut, var maxValue) = ForwardLUTFromBitmap(bitmap);
            ApplyLUT(lut, uncompressed);

            foreach (var channelInfo in channelInfos)
            {
                var data = uncompressed.Slice(channelInfo.StartUShortOffset, channelInfo.UShortsTotal);
                // For 32 bit channels, each half is transformed separately:
                for (int offset = 0; offset < channelInfo.UShortsPerPixel; offset++)
                {
                    Wavelet.Encode2D(
                        data[offset..],
                        channelInfo.Resolution.X,
                        channelInfo.UShortsPerPixel,
                        channelInfo.Resolution.Y,
                        channelInfo.UShortsPerLine,
                        maxValue
                    );
                }
            }

            var compressed = HuffmanCoding.Compress(uncompressed);

            int bitmapSize = 4 + maxNonZero - minNonZero + 1;
            if (compressed.Length + bitmapSize + 4 >= info.UncompressedByteSize)
            {
                return CompressionResult.NoGain;
            }

            using (var writer = new BinaryWriter(dest, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((ushort)minNonZero);
                writer.Write((ushort)maxNonZero);
                // Bitmap is stored only from the first to the last non-zero value
                // If first is larger than last non-zero value, the bitmap is all 0, and we don't store anything
                if (minNonZero <= maxNonZero)
                {
                    writer.Write(bitmap.AsSpan(minNonZero, maxNonZero - minNonZero + 1));
                }
                writer.Write(compressed.Length);
                writer.Write(compressed);
                
                return CompressionResult.Success;
            }
        }
        finally
        {
            ArrayPool<ushort>.Shared.Return(uncompressedArray);
        }
    }

    public override void InternalDecompress(Stream source, Stream dest, PixelDataInfo info)
    {
        int expectedUShortSize = info.UncompressedByteSize / 2;

        // Read data:

        ushort minNonZeroIndex, maxNonZeroIndex;
        byte[] bitmap = new byte[BITMAP_SIZE];
        byte[] compressed;
        using (var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true))
        {
            minNonZeroIndex = reader.ReadUInt16();
            maxNonZeroIndex = reader.ReadUInt16();

            if (minNonZeroIndex >= BITMAP_SIZE || maxNonZeroIndex >= BITMAP_SIZE)
            {
                throw new CompressionException($"Error in PIZ data: min/max non-zero indices exceed bitmap size");
            }

            // Bitmap is stored only from the first to the last non-zero value
            // If first is larger than last non-zero value, the bitmap is all 0.
            if (minNonZeroIndex <= maxNonZeroIndex)
            {
                source.ReadExactly(bitmap, minNonZeroIndex, maxNonZeroIndex - minNonZeroIndex + 1);
            }

            int dataSize = reader.ReadInt32();

            compressed = reader.ReadBytes(dataSize);
        }

        // Decompression:

        (var lut, var maxValue) = ReverseLUTFromBitmap(bitmap);

        var decompressed = HuffmanCoding.Decompress(compressed, expectedUShortSize);

        var channelInfos = CreateChannelInfos(info);

        foreach (var channelInfo in channelInfos)
        {
            var data = decompressed.AsSpan(channelInfo.StartUShortOffset, channelInfo.UShortsTotal);
            // For 32 bit channels, each half is transformed seperately:
            for (int offset = 0; offset < channelInfo.UShortsPerPixel; offset++)
            {
                Wavelet.Decode2D(
                    data[offset..],
                    channelInfo.Resolution.X,
                    channelInfo.UShortsPerPixel,
                    channelInfo.Resolution.Y,
                    channelInfo.UShortsPerLine,
                    maxValue
                );
            }
        }

        ApplyLUT(lut, decompressed);

        // TODO: Handle Big Endian
        var decompressedAsBytes = MemoryMarshal.AsBytes<ushort>(decompressed);

        // The uncompressed PIZ data stores all scanlines for each channel consecutively -
        // e.g. A for all scanlines, followed by B for all scanlines, followed by G, followed by R.
        // Split into scanlines, each containing its own (e.g.) ABGR channels.
        var result = ArrayPool<byte>.Shared.Rent(decompressedAsBytes.Length);
        try
        {
            int resultIndex = 0;
            for (int y = info.Bounds.Top; y < info.Bounds.Bottom; y++)
            {
                foreach (var channel in channelInfos)
                {
                    if (y % channel.YSampling != 0)
                    {
                        continue;
                    }
                    var size = channel.BytesPerLine;
                    var channelScanline = decompressedAsBytes.Slice(channel.NextScanLineByteOffset, size);
                    channel.NextScanLineByteOffset += size;
                    var resultSpan = result.AsSpan(resultIndex, size);
                    channelScanline.CopyTo(resultSpan);
                    resultIndex += size;
                }
            }
            dest.Write(result, 0, decompressedAsBytes.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(result);
        }

    }

    private static List<ChannelInfo> CreateChannelInfos(PixelDataInfo info)
    {
        var channelInfos = info.Channels.Select(c => new ChannelInfo(c, info.Bounds)).ToList();
        int channelStart = 0;
        foreach (var channelInfo in channelInfos)
        {
            channelInfo.StartUShortOffset = channelStart;
            channelInfo.NextScanLineByteOffset = channelStart * 2;
            channelInfo.NextScanLineUShortOffset = channelStart;
            channelStart += channelInfo.UShortsTotal;
        }

        return channelInfos;
    }

    private static (byte[] bitmap, int minIndexNonZero, int maxIndexNonZero) BitmapFromData(Span<ushort> data)
    {
        int minIndexNonZero = BITMAP_SIZE - 1;
        int maxIndexNonZero = 0;
        var bitmap = new byte[BITMAP_SIZE];

        for (int i = 0; i < data.Length; i++)
        {
            ushort value = data[i];
            bitmap[value >> 3] |= (byte)(1 << (value & 0b111));
        }

        bitmap[0] &= 0b11111110; // Clear bit for 0 - "zero is not explicitly stored in the bitmap; we assume that the data always contain zeroes"

        for (ushort i = 0; i < BITMAP_SIZE; i++)
        {
            if (bitmap[i] != 0)
            {
                if (minIndexNonZero > i)
                {
                    minIndexNonZero = i;
                }
                if (maxIndexNonZero < i)
                {
                    maxIndexNonZero = i;
                }
            }
        }

        return (bitmap, minIndexNonZero, maxIndexNonZero);
    }

    private static (ushort[] lut, ushort maxValue) ForwardLUTFromBitmap(byte[] bitmap)
    {
        ushort k = 0;
        ushort[] lut = new ushort[USHORT_RANGE];

        for (uint i = 0; i < USHORT_RANGE; i++)
        {
            if (i == 0 || (bitmap[i >> 3] & (1 << (int)(i & 0b111))) != 0)
            {
                lut[i] = k++;
            }
            else
            {
                lut[i] = 0;
            }
        }

        return (lut, (ushort)(k - 1));
    }

    private static (ushort[] lut, ushort maxValue) ReverseLUTFromBitmap(byte[] bitmap)
    {
        uint n;
        uint k = 0;
        ushort[] lut = new ushort[USHORT_RANGE];

        for (uint i = 0; i < USHORT_RANGE; i++)
        {
            if (i == 0 || (bitmap[i >> 3] & (1 << (int)(i & 0b111))) != 0)
            {
                lut[k++] = (ushort)i;
            }
        }

        n = k - 1;

        while (k < USHORT_RANGE)
        {
            lut[k++] = 0;
        }

        return (lut, (ushort)n);
    }

    private static void ApplyLUT(ushort[] lut, Span<ushort> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = lut[data[i]];
        }
    }
}
