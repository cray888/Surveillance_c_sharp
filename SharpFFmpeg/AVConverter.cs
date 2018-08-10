using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace SharpFFmpeg
{
    public class AVConverter
    {
        static double[,] YUV2RGB_CONVERT_MATRIX = new double[3, 3] { { 1, 0, 1.4022 }, { 1, -0.3456, -0.7145 }, { 1, 1.771, 0 } };

        public static void ConvertYUV2RGB(byte[] yuvFrame, byte[] rgbFrame, int width, int height)
        {
            int uIndex = width * height;
            int vIndex = uIndex + ((width * height) >> 2);
            int gIndex = width * height;
            int bIndex = gIndex * 2;

            int temp = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    temp = (int)(yuvFrame[y * width + x] + (yuvFrame[vIndex + (y / 2) * (width / 2) + x / 2] - 128) * YUV2RGB_CONVERT_MATRIX[0, 2]);
                    rgbFrame[y * width + x] = (byte)(temp < 0 ? 0 : (temp > 255 ? 255 : temp));

                    temp = (int)(yuvFrame[y * width + x] + (yuvFrame[uIndex + (y / 2) * (width / 2) + x / 2] - 128) * YUV2RGB_CONVERT_MATRIX[1, 1] + (yuvFrame[vIndex + (y / 2) * (width / 2) + x / 2] - 128) * YUV2RGB_CONVERT_MATRIX[1, 2]);
                    rgbFrame[gIndex + y * width + x] = (byte)(temp < 0 ? 0 : (temp > 255 ? 255 : temp));

                    temp = (int)(yuvFrame[y * width + x] + (yuvFrame[uIndex + (y / 2) * (width / 2) + x / 2] - 128) * YUV2RGB_CONVERT_MATRIX[2, 1]);
                    rgbFrame[bIndex + y * width + x] = (byte)(temp < 0 ? 0 : (temp > 255 ? 255 : temp));
                }
            }
        }

        public static Bitmap ConvertRGB2Bitmap(byte[] rgbframe, int width, int height)
        {
            int yu = width * 3 % 4;
            int bytePerLine = 0;
            yu = yu != 0 ? 4 - yu : yu;
            bytePerLine = width * 3 + yu;

            MemoryStream ms = new MemoryStream();
            byte[] identifier = new byte[2] { (byte)'B', (byte)'M' };
            ms.Write(identifier, 0, 2);
            byte[] bytes0 = new byte[4];
            ms.Write(IntToBytes(bytePerLine * height + 54), 0, 4);
            ms.Write(bytes0, 0, 4);
            ms.Write(IntToBytes(54), 0, 4);
            ms.Write(IntToBytes(40), 0, 4);
            ms.Write(IntToBytes(width), 0, 4);
            ms.Write(IntToBytes(height), 0, 4);
            byte[] bytes1 = new byte[4] { 0x10, 0x00, 0x18, 0x00 };
            ms.Write(bytes1, 0, 4);
            ms.Write(bytes0, 0, 4);
            ms.Write(IntToBytes(bytePerLine * height), 0, 4);
            ms.Write(bytes0, 0, 4);
            ms.Write(bytes0, 0, 4);
            ms.Write(bytes0, 0, 4);
            ms.Write(bytes0, 0, 4);
            byte[] bgrdata = new byte[bytePerLine * height];
            int gIndex = width * height;
            int bIndex = gIndex * 2;

            for (int y = height - 1, j = 0; y >= 0; y--, j++)
            {
                for (int x = 0, o = 0; x < width; x++)
                {
                    bgrdata[y * bytePerLine + o++] = rgbframe[bIndex + j * width + x];    // B
                    bgrdata[y * bytePerLine + o++] = rgbframe[gIndex + j * width + x];    // G
                    bgrdata[y * bytePerLine + o++] = rgbframe[j * width + x];  // R
                }
            }

            ms.Write(bgrdata, 0, bgrdata.Length);
            Bitmap bm = (Bitmap)Image.FromStream(ms);

            return bm;
        }

        public static Bitmap ConvertYUV2Bitmap(byte[] yuvFrame, byte[] rgbFrame, int width, int height)
        {
            ConvertYUV2RGB(yuvFrame, rgbFrame, width, height);
            yuvFrame = null;
            Bitmap bm = ConvertRGB2Bitmap(rgbFrame, width, height);
            return bm;
        }

        static byte[] IntToBytes(int value)
        {
            byte[] src = new byte[4];
            src[3] = (byte)((value >> 24) & 0xFF);
            src[2] = (byte)((value >> 16) & 0xFF);
            src[1] = (byte)((value >> 8) & 0xFF);
            src[0] = (byte)(value & 0xFF);
            return src;
        }
    }
}
