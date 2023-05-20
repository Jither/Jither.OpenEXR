namespace Jither.OpenEXR.ImageSharp;

public class OpenExrMetadata : IDeepCloneable
{
    public OpenExrMetadata()
    {

    }

    private OpenExrMetadata(OpenExrMetadata other)
    {

    }

    public IDeepCloneable DeepClone() => new OpenExrMetadata(this);
}
