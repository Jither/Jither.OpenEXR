using Jither.OpenEXR.Attributes;
using Jither.OpenEXR.Compression;
using System.Buffers;
using System.Linq;

namespace Jither.OpenEXR;

public class EXRPart
{
    // Used for reading of files
    private readonly bool isSinglePartTiled;
    private readonly bool isMultiPart;
    private readonly OffsetTable offsets;

    // Used for writing of files
    private long offsetTableOffset;

    private readonly EXRHeader header;

    public ChannelList Channels => header.Channels;
    public EXRCompression Compression => header.Compression;
    public Box2i DataWindow => header.DataWindow;
    public Box2i DisplayWindow => header.DisplayWindow;
    public LineOrder LineOrder => header.LineOrder;
    public float PixelAspectRatio => header.PixelAspectRatio;
    public V2f ScreenWindowCenter => header.ScreenWindowCenter;
    public float ScreenWindowWidth => header.ScreenWindowWidth;

    public string? Name => header.Name;
    public PartType Type => header.Type ?? (isSinglePartTiled ? PartType.TiledImage : PartType.ScanLineImage);

    public bool IsRGB => HasChannel("R") && HasChannel("G") && HasChannel("B");
    public bool HasAlpha => HasChannel("A");

    public int BytesPerPixel => Channels.Sum(c => c.Type.GetBytesPerPixel());
    public int BitsPerPixel => BytesPerPixel * 8;
    public int PixelsPerBlock => Compressor.ScanLinesPerBlock * DataWindow.Width;
    public int BytesPerBlock => BytesPerPixel * PixelsPerBlock;

    public bool HasLongNames => Name?.Length > 31 || header.Attributes.Any(attr => attr.Name.Length > 31 || attr.Type.Length > 31);

    private readonly Compressor Compressor;

    public EXRPart(EXRReader reader, EXRVersion version, EXRHeader header)
    {
        isSinglePartTiled = version.IsSinglePartTiled;
        isMultiPart = version.IsMultiPart;

        this.header = header;

        Compressor = Compression switch
        {
            EXRCompression.None => new NullCompressor(),
            EXRCompression.RLE => new RLECompressor(),
            EXRCompression.ZipS => new ZipSCompressor(),
            EXRCompression.Zip => new ZipCompressor(),
            EXRCompression.Piz => new PizCompressor(),
            _ => throw new NotSupportedException($"{Compression} compression not supported")
        };

        int offsetTableSize;

        if (version.IsMultiPart)
        {
            offsetTableSize = header.ChunkCount;
        }
        else if (version.IsSinglePartTiled)
        {
            // TODO: offsetTable for single part tiled
            offsetTableSize = 0;
        }
        else
        {
            offsetTableSize = (int)Math.Ceiling((double)DataWindow.Height / Compressor.ScanLinesPerBlock);
        }

        offsets = OffsetTable.ReadFrom(reader, offsetTableSize);
    }

    public T? GetAttribute<T>(string name)
    {
        if (header.TryGetAttribute<T>(name, out var result))
        {
            return result;
        }
        return default;
    }

    public void SetAttribute<T>(string name, T value)
    {
        header.SetAttribute(new EXRAttribute<T>(name, value));
    }

    public void WriteHeaderTo(EXRWriter writer)
    {
        header.WriteTo(writer);
    }

    public void WriteOffsetPlaceholdersTo(EXRWriter writer)
    {
        offsetTableOffset = writer.Position;
        int offsetTableSize = (int)Math.Ceiling((double)DataWindow.Height / Compressor.ScanLinesPerBlock);
        for (int i = 0; i < offsetTableSize; i++)
        {
            writer.WriteULong(0xffffffffffffffffUL);
        }
    }

    public void Decode(EXRReader reader, Stream destination)
    {
        Decode(reader, (buffer, length) => destination.Write(buffer, 0, length));
    }

    public void Decode(EXRReader reader, Action<byte[], int> callback, IList<string>? outputChannelOrder = null)
    {
        outputChannelOrder ??= Channels.Select(c => c.Name).ToArray();

        byte[] buffer = ArrayPool<byte>.Shared.Rent(BytesPerBlock);
        try
        {
            byte[] interleaved = ArrayPool<byte>.Shared.Rent(BytesPerBlock);
            try
            {
                Decode(reader, callback, buffer, interleaved, outputChannelOrder);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(interleaved);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void Decode(EXRReader reader, Action<byte[], int> callback, byte[] buffer, byte[] interleaved, IList<string> outputChannelOrder)
    {
        // Offsets are always ordered with scanlines from top to bottom (INCREASING_Y). However, the order of the scanlines within the file
        // may be bottom to top or random (see LineOrder). Each block stores its first scanline's y coordinate, meaning it's possible to
        // read blocks in file sequential order and reconstruct the scanline order - avoiding file seeks. For now, we just follow the
        // offset order.

        // Collect byte offsets for the channel components in each pixel. I.e., at what byte offset within the channel-interleaved pixel should each channel be stored?
        var destOffsets = new List<int>(Channels.Count);

        for (int i = 0; i < Channels.Count; i++)
        {
            destOffsets.Add(-1);
        }

        int startOffset = 0;
        foreach (var outputChannel in outputChannelOrder)
        {
            var inputChannelIndex = Channels.IndexOf(outputChannel);
            if (inputChannelIndex < 0)
            {
                throw new ArgumentException($"Unknown channel name in output channel order: {outputChannel}. Should be one of: {String.Join(", ", Channels.Select(c => c.Name))}");
            }
            var inputChannel = Channels[inputChannelIndex];
            destOffsets[inputChannelIndex] = startOffset;
            startOffset += inputChannel.Type.GetBytesPerPixel();
        }
        // The startOffset now also equals the number of bytes we're going to store per pixel
        int outputByteCount = startOffset * PixelsPerBlock;

        for (int index = 0; index < offsets.Count; index++)
        {
            int y = DecodeScanLine(reader, buffer, index);
            // The decompressed pixel data is stored with channels separated and ordered alphabetically
            int sourceIndex = 0;
            int destIndex;

            int channelIndex = 0;
            foreach (var channel in Channels)
            {
                int channelByteSize = channel.Type.GetBytesPerPixel();
                destIndex = destOffsets[channelIndex++];

                if (destIndex >= 0)
                {
                    for (int i = 0; i < PixelsPerBlock; i++)
                    {
                        for (int j = 0; j < channelByteSize; j++)
                        {
                            interleaved[destIndex + j] = buffer[sourceIndex++];
                        }
                        destIndex += BytesPerPixel;
                    }
                }
                else
                {
                    // Skip this channel
                    sourceIndex += channelByteSize * PixelsPerBlock;
                }
            }
            callback(interleaved, outputByteCount);
        }
    }

    private int DecodeScanLine(EXRReader reader, byte[] buffer, int index)
    {
        var offset = offsets[index];
        reader.Seek(offset);
        int partNumber = isMultiPart ? reader.ReadInt() : 0;
        int y = reader.ReadInt();
        int pixelDataSize = reader.ReadInt();

        ReadPixelData(reader, buffer, pixelDataSize);

        return y;
    }

    private void ReadPixelData(EXRReader reader, byte[] destination, int pixelDataSize)
    {
        var chunkStream = reader.GetChunkStream(pixelDataSize);
        var destStream = new MemoryStream(destination);
        chunkStream.CopyTo(destStream);
    }

    public bool HasChannel(string name)
    {
        return header.Channels.Any(c => c.Name == name);
    }
}
