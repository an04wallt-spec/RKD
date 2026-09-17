using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace RkdEstimator
{
    internal static class LosslessPdfImage
    {
        public static byte[] EncodeRgb(Bitmap bitmap)
        {
            Rectangle bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                using (MemoryStream output = new MemoryStream())
                {
                    // RFC 1950 header for a standard DEFLATE stream used by PDF /FlateDecode.
                    output.WriteByte(0x78);
                    output.WriteByte(0x9C);
                    uint a = 1;
                    uint b = 0;
                    byte[] sourceRow = new byte[Math.Abs(data.Stride)];
                    byte[] rgbRow = new byte[bitmap.Width * 3];
                    using (DeflateStream deflate = new DeflateStream(output, CompressionMode.Compress, true))
                    {
                        for (int y = 0; y < bitmap.Height; y++)
                        {
                            IntPtr rowPointer = IntPtr.Add(data.Scan0, y * data.Stride);
                            Marshal.Copy(rowPointer, sourceRow, 0, sourceRow.Length);
                            for (int x = 0; x < bitmap.Width; x++)
                            {
                                int source = x * 3;
                                int target = source;
                                rgbRow[target] = sourceRow[source + 2];
                                rgbRow[target + 1] = sourceRow[source + 1];
                                rgbRow[target + 2] = sourceRow[source];
                            }
                            deflate.Write(rgbRow, 0, rgbRow.Length);
                            for (int i = 0; i < rgbRow.Length; i++)
                            {
                                a += rgbRow[i];
                                b += a;
                                if ((i & 4095) == 4095) { a %= 65521; b %= 65521; }
                            }
                            a %= 65521;
                            b %= 65521;
                        }
                    }
                    uint adler = (b << 16) | a;
                    output.WriteByte((byte)(adler >> 24));
                    output.WriteByte((byte)(adler >> 16));
                    output.WriteByte((byte)(adler >> 8));
                    output.WriteByte((byte)adler);
                    return output.ToArray();
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
