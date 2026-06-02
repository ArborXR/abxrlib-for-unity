using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using ZXing;

namespace AbxrLib.Runtime.Core.QRScanner
{
    internal static class QrCodeScanCommon
    {
        public static BarcodeReader CreateBarcodeReader()
        {
            return new BarcodeReader
            {
                AutoRotate = true,
                Options =
                {
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.CODE_93 },
                    TryHarder = true
                }
            };
        }

        public static string TryDecodeMatchingAbxrQr(BarcodeReader barcodeReader, Color32[] pixels, int width, int height)
        {
            if (barcodeReader == null || pixels == null || pixels.Length == 0 || width <= 0 || height <= 0) return null;

            // Fast path: single decode
            Result single = null;
            try
            {
                single = barcodeReader.Decode(pixels, width, height);
                Debug.LogError("AbxrLib: FOUND BARCODE: " + single.Text);
            }
            catch (Exception ex)
            {
                Logcat.Warning("QR decode error: " + ex.Message);
            }
            
            if (single == null) return null; // found nothing

            string text = single.Text?.Trim();
            if (!string.IsNullOrEmpty(text) && text.StartsWith("ABXR:", StringComparison.OrdinalIgnoreCase))
                return text;

            // Found a code, but not an ABXR one. There may still be an ABXR code in frame
            // that single-decode didn't return. Run multi-pass to look for it.
            // Only bother running multi-pass for QR codes
            if (single.BarcodeFormat != BarcodeFormat.QR_CODE) return null;
            try
            {
                Result[] results = barcodeReader.DecodeMultiple(pixels, width, height);
                if (results != null)
                {
                    foreach (Result result in results)
                    {
                        text = result?.Text?.Trim();
                        if (!string.IsNullOrEmpty(text) && text.StartsWith("ABXR:", StringComparison.OrdinalIgnoreCase))
                            return text;
                    }
                }
            }
            catch (Exception ex)
            {
                Logcat.Warning("QR decode-multiple error: " + ex.Message);
            }

            return null;
        }

        public static bool TryExtractPinFromQrPayload(string scanResult, out string pin)
        {
            pin = null;
            if (string.IsNullOrEmpty(scanResult)) return false;

            string s = scanResult.Trim();
            Match match = Regex.Match(s, @"(?i)(?<=ABXR:)\d+");
            if (match.Success)
            {
                pin = match.Value;
                return true;
            }

            match = Regex.Match(s, @"^\d{6}$");
            if (match.Success)
            {
                pin = match.Value;
                return true;
            }

            return false;
        }
    }
}
