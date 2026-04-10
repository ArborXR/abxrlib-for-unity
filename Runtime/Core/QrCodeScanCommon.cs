using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using ZXing;

namespace AbxrLib.Runtime.Core
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
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                    TryHarder = true
                }
            };
        }

        public static string TryDecodeMatchingAbxrQr(BarcodeReader barcodeReader, Color32[] pixels, int width, int height)
        {
            if (barcodeReader == null || pixels == null || pixels.Length == 0 || width <= 0 || height <= 0) return null;

            try
            {
                Result[] results = barcodeReader.DecodeMultiple(pixels, width, height);
                if (results != null && results.Length > 0)
                {
                    foreach (Result result in results)
                    {
                        string text = result?.Text?.Trim();
                        if (!string.IsNullOrEmpty(text) && text.StartsWith("ABXR:", StringComparison.OrdinalIgnoreCase)) return text;
                    }
                }
            }
            catch (Exception ex)
            {
                Logcat.Warning("QR decode-multiple error: " + ex.Message);
            }

            try
            {
                Result result = barcodeReader.Decode(pixels, width, height);
                string text = result?.Text?.Trim();
                if (!string.IsNullOrEmpty(text) && text.StartsWith("ABXR:", StringComparison.OrdinalIgnoreCase)) return text;
            }
            catch (Exception ex)
            {
                Logcat.Warning("QR decode error: " + ex.Message);
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
