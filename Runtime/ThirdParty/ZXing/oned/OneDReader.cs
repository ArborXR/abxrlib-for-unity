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
    /// Stub base class for OneDReader (not used for QR code reading)
    /// </summary>
    public abstract class OneDReader : Reader
    {
        /// <summary>
        /// Attempts to decode a one-dimensional barcode format given a single row of an image.
        /// </summary>
        public abstract Result decodeRow(int rowNumber, BitArray row, IDictionary<DecodeHintType, object> hints);

        public virtual Result decode(BinaryBitmap image)
        {
            return null;
        }

        public virtual Result decode(BinaryBitmap image, IDictionary<DecodeHintType, object> hints)
        {
            return null;
        }

        public virtual void reset()
        {
        }
    }
}

