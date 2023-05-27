# Jither.OpenEXR

Jither.OpenEXR is a C#/.NET library for reading and writing [OpenEXR](https://openexr.com/) image files.

The current code is the result of a weekend's work, and isn't ready for production use in any way.

Current state
-------------
* Half (F16), Float (F32) and Uint channel support
* RLE, ZIP, ZIPS and PIZ compression (as well as uncompressed)
* Any channel setup (i.e. including custom channels)
* Sub-sampling support
* Supports all attributes
* Scanline images only for now

TODO
----
* API finalization (or even making it just barely useful). Currently, the API is very low level, requiring the user to know the OpenEXR spec, and handling color spaces and pixel formats themselves.

* Support for tiled images.

* Probably integration with ImageSharp.

* Possibly support for deep data.

* Optimization

* Tests - and testing. 🤪

There are no plans to support the lossy compression schemes (PXR24, B44/B44A, DWAA/DWAB).
