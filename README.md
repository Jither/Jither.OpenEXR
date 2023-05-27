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
* API finalization (or even making it just barely useful). Currently, the API is very low level, requiring the user to know the OpenEXR spec, and handling color spaces and pixel formats themselves. It also doesn't yet help with creating images from scratch - focus has been on reading, modifying, and writing back existing images.

* Support for tiled images.

* Support for deep data.

* Tests - and testing. 🤪

* Possibly integration with ImageSharp.

There are no plans to support the lossy compression schemes (PXR24, B44/B44A, DWAA/DWAB).
