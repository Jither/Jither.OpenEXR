# Jither.OpenEXR

Jither.OpenEXR is a C#/.NET library for reading and writing [OpenEXR](https://openexr.com/) image files.

Current state
-------------
* Half (F16), Float (F32) and Uint channel support
* RLE, ZIP, ZIPS and PIZ compression (as well as uncompressed)
* Any channel setup (i.e. including custom channels)
* Sub-sampling support
* Supports all attributes
* Single or multi-part
* Reads Scanline and Tiled (including multi-resolution) images.
* Writes Scanline images only for now. (Supports converting Tiled to Scanline).

TODO
----
* API finalization (or even making it just barely useful). Currently, the API is very low level, requiring the user to know the OpenEXR spec, and handling color spaces and pixel formats themselves.

* Probably integration with ImageSharp.

* Possibly support for writing tiled images.

* Possibly support for deep data.

* Optimization

* Tests - and testing. 🤪

There are no plans to support the lossy compression schemes (PXR24, B44/B44A, DWAA/DWAB).
