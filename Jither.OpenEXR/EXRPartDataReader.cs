﻿using Jither.OpenEXR.Compression;
using Jither.OpenEXR.Converters;
using System.Buffers;

namespace Jither.OpenEXR;

public class EXRPartDataReader : EXRPartDataHandler
{
    private OffsetTable? _offsets;

    private readonly EXRReader reader;
    private readonly long offsetTableOffset;

    private OffsetTable OffsetTable
    {
        get
        {
            if (_offsets != null)
            {
                return _offsets;
            }

            reader.Seek(offsetTableOffset);
            _offsets = OffsetTable.ReadFrom(reader, ChunkCount);
            return _offsets;
        }
    }

    public EXRPartDataReader(EXRPart part, EXRVersion version, EXRReader reader, long offsetTableOffset) : base(part, version)
    {
        this.reader = reader;
        this.offsetTableOffset = offsetTableOffset;
    }

    /// <summary>
    /// Reads the image data from the part into a scanline-interleaved array (the standard OpenEXR image data layout).
    /// </summary>
    /// <remarks>
    /// Scanline-interleaved means that channels are stored separately for each scanline and sorted in alphabetical order.
    /// In other words, a 5x2 pixel RGBA image will be stored as: AAAAA BBBBB GGGGG RRRRR AAAAA BBBBB GGGGG RRRRR.
    /// </remarks>
    public void Read(byte[] dest)
    {
        part.ValidateAttributes(fileIsMultiPart, fileHasDeepData);

        if (dest == null)
        {
            throw new ArgumentNullException(nameof(dest));
        }

        var totalBytes = GetTotalByteCount();
        if (dest.Length < totalBytes)
        {
            throw new ArgumentException($"Destination array too small ({dest.Length}) to fit pixel data ({totalBytes})");
        }

        int destOffset = 0;
        for (int i = 0; i < ChunkCount; i++)
        {
            var chunkInfo = ReadChunkHeader(i);
            InternalReadChunk(chunkInfo, dest, destOffset);
            destOffset += chunkInfo.UncompressedByteCount;
        }
    }

    public void ReadChunk(int chunkIndex, byte[] dest, int destOffset = 0)
    {
        part.ValidateAttributes(fileIsMultiPart, fileHasDeepData);

        var chunkInfo = ReadChunkHeader(chunkIndex);
        InternalReadChunk(chunkInfo, dest, destOffset);
    }

    public void ReadInterleaved(byte[] dest, string[] channelOrder)
    {
        part.ValidateAttributes(fileIsMultiPart, fileHasDeepData);

        CheckInterleavedPrerequisites();

        if (dest == null)
        {
            throw new ArgumentNullException(nameof(dest));
        }

        var converter = new PixelInterleaveConverter(part.Channels, channelOrder);
        int destOffset = 0;
        for (int chunkIndex = 0; chunkIndex < ChunkCount; chunkIndex++)
        {
            var chunkInfo = ReadChunkHeader(chunkIndex);
            var pixelData = ArrayPool<byte>.Shared.Rent(chunkInfo.UncompressedByteCount);
            try
            {
                InternalReadChunk(chunkInfo, pixelData, 0);
                converter.FromEXR(chunkInfo.GetBounds(), pixelData, dest, destOffset);
                destOffset += chunkInfo.UncompressedByteCount;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pixelData);
            }
        }
    }

    public ChunkInfo ReadChunkInterleaved(int chunkIndex, byte[] dest, string[] channelOrder, int destOffset = 0)
    {
        part.ValidateAttributes(fileIsMultiPart, fileHasDeepData);

        CheckInterleavedPrerequisites();

        var converter = new PixelInterleaveConverter(part.Channels, channelOrder);

        var chunkInfo = ReadChunkHeader(chunkIndex);
        var pixelData = ArrayPool<byte>.Shared.Rent(chunkInfo.UncompressedByteCount);
        try
        {
            InternalReadChunk(chunkInfo, pixelData, 0);
            converter.FromEXR(chunkInfo.GetBounds(), pixelData, dest, destOffset);
            return chunkInfo;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixelData);
        }
    }

    private ChunkInfo ReadChunkHeader(int chunkIndex)
    {
        var offset = OffsetTable[chunkIndex];

        reader.Seek(offset);
        int partNumber = fileIsMultiPart ? reader.ReadInt() : 0;

        if (partNumber != part.PartNumber)
        {
            throw new EXRFormatException($"Read unexpected part number for chunk {chunkIndex}. Reading for part {part.PartNumber} but chunk is for part {partNumber}.");
        }

        ChunkInfo chunkInfo;
        if (IsTiled)
        {
            if (reader.Remaining < 16)
            {
                throw new EXRFormatException($"Truncated chunk header - expected at least 16 bytes, was: {reader.Remaining}");
            }
            int x = reader.ReadInt();
            int y = reader.ReadInt();
            int levelX = reader.ReadInt();
            int levelY = reader.ReadInt();
            chunkInfo = new TileChunkInfo(part, chunkIndex, x, y, levelX, levelY);
        }
        else
        {
            if (reader.Remaining < 4)
            {
                throw new EXRFormatException($"Truncated chunk header - expected at least 4 bytes, was: {reader.Remaining}");
            }
            int y = reader.ReadInt();
            chunkInfo = new ScanlineChunkInfo(part, chunkIndex, y);
        }

        chunkInfo.CompressedByteCount = reader.ReadInt();

        chunkInfo.PixelDataFileOffset = reader.Position;
        chunkInfo.FileOffset = offset;

        return chunkInfo;
    }

    private void InternalReadChunk(ChunkInfo chunkInfo, byte[] dest, int destIndex)
    {
        reader.Seek(chunkInfo.PixelDataFileOffset);
        var chunkStream = reader.GetChunkStream(chunkInfo.CompressedByteCount);

        if (destIndex + chunkInfo.UncompressedByteCount > dest.Length)
        {
            throw new EXRFormatException($"Uncompressed byte count for {chunkInfo} ({chunkInfo.UncompressedByteCount}) exceeds expected size (max {dest.Length - destIndex}");
        }

        using (var destStream = new MemoryStream(dest, destIndex, chunkInfo.UncompressedByteCount))
        {
            // Yes, compressors could use the length or capacity of the stream rather than
            // an explicit expectedBytes parameter, but not sure if we'll change this
            // implementation in the future.
            var info = new PixelDataInfo(
                part.Channels,
                chunkInfo.GetBounds(),
                chunkInfo.UncompressedByteCount
            );

            compressor.Decompress(chunkStream, destStream, info);
        }
    }
}
