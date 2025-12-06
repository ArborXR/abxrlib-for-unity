# ZXing.Net for Unity

This directory contains a minimal subset of the ZXing.Net library required for QR code reading in Unity.

## What's Included

This is a stripped-down version of ZXing.Net that includes only the components needed for QR code decoding using Unity's `Color32[]` format:

### Core Library Files
- `BarcodeReader.Color32.cs` - Unity-specific barcode reader for Color32 arrays
- `BarcodeReaderGeneric.cs` - Base barcode reader implementation
- `BarcodeFormat.cs` - Barcode format enumeration (QR_CODE, etc.)
- `Result.cs` - Decoding result class
- Core interfaces and base classes for barcode reading

### QR Code Decoder
- `qrcode/` - Complete QR code decoder implementation including:
  - Decoder (`decoder/`)
  - Detector (`detector/`)
  - Encoder (`encoder/`) - included for completeness but not used for reading

### Common Utilities
- `common/` - Common utilities required for QR code decoding:
  - Bit manipulation (`BitArray.cs`, `BitMatrix.cs`, `BitSource.cs`)
  - Reed-Solomon error correction (`reedsolomon/`)
  - Image processing utilities
  - BigInteger support for error correction

### Unity-Specific
- `Color32LuminanceSource.cs` - Unity Color32 to luminance conversion

## License

This code is licensed under the Apache License 2.0. See `LICENSE` file for details.

## Source

Original source: https://github.com/micjahn/ZXing.Net

## Usage

This library is used by `MetaQRCodeReader.cs` for QR code scanning on Meta Quest headsets.

