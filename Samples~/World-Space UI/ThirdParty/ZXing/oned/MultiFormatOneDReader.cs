/*
 * Copyright 2008 ZXing authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using ZXing.Common;

namespace ZXing.OneD
{
    /// <summary>
    /// Stub implementation for MultiFormatOneDReader (not used for QR code reading)
    /// This is a minimal implementation that satisfies the interface requirements.
    /// </summary>
    public sealed class MultiFormatOneDReader : OneDReader
    {
        private readonly IList<OneDReader> readers;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiFormatOneDReader"/> class.
        /// </summary>
        /// <param name="hints">The hints.</param>
        public MultiFormatOneDReader(IDictionary<DecodeHintType, object> hints)
        {
            // Create empty list - not used for QR code reading
            readers = new List<OneDReader>();
        }

        /// <summary>
        /// Attempts to decode a one-dimensional barcode format given a single row of an image.
        /// </summary>
        public override Result decodeRow(int rowNumber, BitArray row, IDictionary<DecodeHintType, object> hints)
        {
            // Not used for QR code reading - return null
            return null;
        }

        /// <summary>
        /// Decode method from Reader interface
        /// </summary>
        public override Result decode(BinaryBitmap image)
        {
            return null;
        }

        /// <summary>
        /// Decode method from Reader interface with hints
        /// </summary>
        public override Result decode(BinaryBitmap image, IDictionary<DecodeHintType, object> hints)
        {
            return null;
        }

        /// <summary>
        /// Resets any internal state the implementation has after a decode, to prepare it for reuse.
        /// </summary>
        public override void reset()
        {
            // No state to reset
        }
    }
}

