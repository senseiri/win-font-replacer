using System;
using System.Text;

namespace WinFontReplacer
{
    internal static class LogFontHelper
    {
        public const int Size = 92;
        public const int FaceOffset = 28;
        public const int FaceBytes = 64;

        public static string GetFaceName(byte[] value)
        {
            if (value == null || value.Length < FaceOffset + FaceBytes)
            {
                return string.Empty;
            }

            string name = Encoding.Unicode.GetString(value, FaceOffset, FaceBytes);
            int nullIndex = name.IndexOf('\0');
            if (nullIndex >= 0)
            {
                name = name.Substring(0, nullIndex);
            }
            return name.Trim();
        }

        public static byte[] WithFaceName(byte[] existing, string faceName)
        {
            return WithFaceName(existing, faceName, -12);
        }

        public static byte[] WithFaceName(byte[] existing, string faceName, int defaultHeight)
        {
            byte[] data;
            if (existing != null && existing.Length == Size)
            {
                data = (byte[])existing.Clone();
            }
            else
            {
                data = CreateDefault(defaultHeight);
            }

            // Always write a sane height. Windows normalizes heights when
            // SPI_SETNONCLIENTMETRICS is used, and reusing that normalized
            // value on the next apply made the font shrink.
            SetInt32(data, 0, defaultHeight);

            int weight = ReadInt32(data, 16);
            if (weight < 100 || weight > 900)
            {
                SetInt32(data, 16, 400);
            }

            // SystemParametersInfo rejects a face change when the LOGFONT
            // retains an unusual height/quality from the current theme.
            SetInt32(data, 4, 0);
            SetInt32(data, 8, 0);
            SetInt32(data, 12, 0);
            data[20] = 0;
            data[21] = 0;
            data[22] = 0;
            data[23] = 1;
            data[24] = 0;
            data[25] = 0;
            data[26] = 5;
            data[27] = 0;

            Array.Clear(data, FaceOffset, FaceBytes);
            string face = faceName ?? string.Empty;
            if (face.Length > 31)
            {
                face = face.Substring(0, 31);
            }

            byte[] bytes = Encoding.Unicode.GetBytes(face);
            int copy = Math.Min(bytes.Length, FaceBytes - 2);
            Array.Copy(bytes, 0, data, FaceOffset, copy);
            return data;
        }

        public static byte[] CreateDefault()
        {
            return CreateDefault(-12);
        }

        public static byte[] CreateDefault(int height)
        {
            byte[] data = new byte[Size];
            SetInt32(data, 0, height);  // lfHeight
            SetInt32(data, 4, 0);       // lfWidth
            SetInt32(data, 8, 0);       // lfEscapement
            SetInt32(data, 12, 0);      // lfOrientation
            SetInt32(data, 16, 400);    // lfWeight
            data[20] = 0;               // lfItalic
            data[21] = 0;               // lfUnderline
            data[22] = 0;               // lfStrikeOut
            data[23] = 1;               // lfCharSet = DEFAULT_CHARSET
            data[24] = 0;               // lfOutPrecision
            data[25] = 0;               // lfClipPrecision
            data[26] = 5;               // lfQuality = CLEARTYPE_QUALITY
            data[27] = 0;               // lfPitchAndFamily
            return data;
        }

        public static int ReadHeight(byte[] value)
        {
            if (value == null || value.Length < 4)
            {
                return -12;
            }
            return ReadInt32(value, 0);
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24);
        }

        private static void SetInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xff);
            data[offset + 1] = (byte)((value >> 8) & 0xff);
            data[offset + 2] = (byte)((value >> 16) & 0xff);
            data[offset + 3] = (byte)((value >> 24) & 0xff);
        }
    }
}
