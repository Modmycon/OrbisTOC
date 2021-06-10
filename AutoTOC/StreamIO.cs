/*
 * C# Stream Helpers
 *
 * Copyright (C) 2015-2018 Pawel Kolodziejski
 * Copyright (C) 2019 ME3Explorer
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

/*
 *  This is modified code from the LegendaryExplorerCore codebase
 *  Taken from LEC on 2021/6/10, at https://github.com/ME3Tweaks/ME3Explorer/commit/0e01b9cfb85668a775afa22ba268e94b525ddc2e
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AutoTOC
{
    public static class StreamHelpers
    {

        public static byte[] ReadToBuffer(this Stream stream, int count)
        {
            var buffer = new byte[count];
            if (stream.Read(buffer, 0, count) != count)
                throw new Exception("Stream read error!");
            return buffer;
        }

        public static byte[] ReadToBuffer(this Stream stream, uint count)
        {
            return stream.ReadToBuffer((int)count);
        }

        public static byte[] ReadToBuffer(this Stream stream, long count)
        {
            return stream.ReadToBuffer((int)count);
        }

        public static void WriteFromBuffer(this Stream stream, byte[] buffer)
        {
            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteGuid(this Stream stream, Guid value)
        {
            var data = value.ToByteArray();

            Debug.Assert(data.Length == 16);

            stream.WriteInt32(BitConverter.ToInt32(data, 0));
            stream.WriteInt16(BitConverter.ToInt16(data, 4));
            stream.WriteInt16(BitConverter.ToInt16(data, 6));
            stream.Write(data, 8, 8);
        }


        public static void WriteToFile(this MemoryStream stream, string outfile)
        {
            long oldPos = stream.Position;
            stream.Position = 0;
            using (FileStream file = new FileStream(outfile, FileMode.Create, System.IO.FileAccess.Write))
                stream.CopyTo(file);
            stream.Position = oldPos;
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, int count)
        {
            var buffer = new byte[0x10000];
            do
            {
                int readed = inputStream.Read(buffer, 0, Math.Min(buffer.Length, count));
                if (readed > 0)
                    stream.Write(buffer, 0, readed);
                else
                    break;
                count -= readed;
            } while (count != 0);
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, uint count)
        {
            WriteFromStream(stream, inputStream, (int)count);
        }

        public static void WriteFromStream(this Stream stream, Stream inputStream, long count)
        {
            WriteFromStream(stream, inputStream, (int)count);
        }

        public static string ReadStringLatin1Null(this Stream stream)
        {
            string str = "";
            for (; ; )
            {
                char c = (char)stream.ReadByte();
                if (c == 0)
                    break;
                str += c;
            }
            return str;
        }

        public static string ReadStringUnicode(this Stream stream, int count)
        {
            var buffer = stream.ReadToBuffer(count);
            return Encoding.Unicode.GetString(buffer);
        }

        public static string ReadStringUnicodeNull(this Stream stream, int count)
        {
            return stream.ReadStringUnicode(count).Trim('\0');
        }

        // DO NOT REMOVE ASCII CODE
        #region ASCII SUPPORT
        public static string ReadStringASCII(this Stream stream, int count)
        {
            byte[] buffer = stream.ReadToBuffer(count);
            return Encoding.ASCII.GetString(buffer);
        }

        public static string ReadStringASCIINull(this Stream stream)
        {
            string str = "";
            for (; ; )
            {
                char c = (char)stream.ReadByte();
                if (c == 0)
                    break;
                str += c;
            }
            return str;
        }

        public static string ReadStringASCIINull(this Stream stream, int count)
        {
            return stream.ReadStringASCII(count).Trim('\0');
        }

        public static void WriteStringASCII(this Stream stream, string str)
        {
            stream.Write(Encoding.ASCII.GetBytes(str), 0, Encoding.ASCII.GetByteCount(str));
        }

        public static void WriteStringASCIINull(this Stream stream, string str)
        {
            stream.WriteStringASCII(str + "\0");
        }

        #endregion

        public static void WriteStringUnicode(this Stream stream, string str)
        {
            stream.Write(Encoding.Unicode.GetBytes(str), 0, Encoding.Unicode.GetByteCount(str));
        }

        public static void WriteStringUnicodeNull(this Stream stream, string str)
        {
            stream.WriteStringUnicode(str);
            stream.WriteByte(0);
            stream.WriteByte(0);
        }

        public static void WriteUInt64(this Stream stream, ulong data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(ulong));
        }

        public static void WriteInt64(this Stream stream, long data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(long));
        }

        public static void WriteUInt32(this Stream stream, uint data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(uint));
        }

        public static void WriteInt32(this Stream stream, int data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(int));
        }

        /// <summary>
        /// Writes the stream to file from the beginning. This should only be used on streams that support seeking. The position is restored after the file has been written.
        /// </summary>
        /// <param name="stream">Stream to write from</param>
        /// <param name="outfile">File to write to</param>
        public static void WriteToFile(this Stream stream, string outfile)
        {
            long oldPos = stream.Position;
            stream.Position = 0;
            using (var file = new FileStream(outfile, FileMode.Create, System.IO.FileAccess.Write))
                stream.CopyTo(file);
            stream.Position = oldPos;
        }

        public static void WriteUInt16(this Stream stream, ushort data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(ushort));
        }

        public static void WriteInt16(this Stream stream, short data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(short));
        }

        public static void WriteDouble(this Stream stream, double data)
        {
            stream.Write(BitConverter.GetBytes(data), 0, sizeof(double));
        }

        private const int DefaultBufferSize = 8 * 1024;

        public static void WriteZeros(this Stream stream, uint count)
        {
            for (int i = 0; i < count; i++)
                stream.WriteByte(0);
        }

        public static void WriteZeros(this Stream stream, int count)
        {
            WriteZeros(stream, (uint)count);
        }

        private static void ThrowEndOfStreamException() => throw new EndOfStreamException();
    }
}
