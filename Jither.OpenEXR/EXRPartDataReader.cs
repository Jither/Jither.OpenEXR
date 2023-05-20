using System.Buffers;
using System.Reflection.PortableExecutable;

namespace Jither.OpenEXR;

public class EXRPartDataReader : EXRPartDataHandler
{
    private OffsetTable? _offsets;

    private readonly EXRReader reader;
    private readonly long offsetTableOffset;
    private readonly int offsetTableSize;
    private readonly bool isMultiPart;

    private int blockIndex = 0;

    // Type = header.Type ?? (isSinglePartTiled ? PartType.TiledImage : PartType.ScanLineImage)

    private OffsetTable OffsetTable
    {
        get
        {
            if (_offsets != null)
            {
                return _offsets;
            }

            reader.Seek(offsetTableOffset);
            _offsets = OffsetTable.ReadFrom(reader, offsetTableSize);
            return _offsets;
        }
    }

    public EXRPartDataReader(EXRPart part, EXRVersion version, EXRReader reader) : base(part)
    {
        this.reader = reader;
        this.offsetTableOffset = reader.Position;

        if (version.IsMultiPart)
        {
            offsetTableSize = part.GetAttributeOrThrow<int>("chunkCount");
        }
        else if (version.IsSinglePartTiled)
        {
            // TODO: offsetTable for single part tiled
            throw new NotImplementedException($"Reading of single part tiled images is not implemented.");
        }
        else
        {
            offsetTableSize = (int)Math.Ceiling((double)part.DataWindow.Height / compressor.ScanLinesPerBlock);
        }
    }

    public void ReadBlock(byte[] dest)
    {
        InternalReadBlock(dest);
    }

    public void ReadBlockInterleaved(byte[] dest, IEnumerable<string> channelOrder)
    {
        // Offsets are always ordered with scanlines from top to bottom (INCREASING_Y). However, the order of the scanlines themselves within the file
        // may be bottom to top or random (see LineOrder). Each block stores its first scanline's y coordinate, meaning it's possible to
        // read blocks in file sequential order and reconstruct the scanline order - avoiding file seeks. For now, we just follow the
        // offset order.

        // Collect byte offsets for the channel components in each pixel. I.e., at what byte offset within the channel-interleaved pixel should each channel be stored?
        var destOffsets = GetInterleaveOffsets(channelOrder, out var outputBytesPerPixel);
        int outputByteCount = outputBytesPerPixel * PixelsPerBlock;

        var data = ArrayPool<byte>.Shared.Rent(outputByteCount);
        try
        {
            InternalReadBlock(data);

            // The decompressed pixel data is stored with channels separated and ordered alphabetically
            int sourceIndex = 0;
            int destIndex;

            int channelIndex = 0;
            foreach (var channel in part.Channels)
            {
                int channelBytesPerPixel = channel.Type.GetBytesPerPixel();
                destIndex = destOffsets[channelIndex++];

                if (destIndex >= 0)
                {
                    for (int i = 0; i < PixelsPerBlock; i++)
                    {
                        for (int j = 0; j < channelBytesPerPixel; j++)
                        {
                            dest[destIndex + j] = data[sourceIndex++];
                        }
                        destIndex += outputBytesPerPixel;
                    }
                }
                else
                {
                    // Skip this channel
                    sourceIndex += channelBytesPerPixel * PixelsPerBlock;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    private void InternalReadBlock(byte[] dest)
    {
        var offset = OffsetTable[blockIndex];
        reader.Seek((long)offset);
        int partNumber = isMultiPart ? reader.ReadInt() : 0;
        int y = reader.ReadInt();
        int pixelDataSize = reader.ReadInt();

        var chunkStream = reader.GetChunkStream(pixelDataSize);
        using (var destStream = new MemoryStream(dest))
        {
            compressor.Decompress(chunkStream, destStream);
        }

        blockIndex++;
    }
}
