﻿using System;
using System.IO;

namespace PuyoTools.Modules.Compression
{
    public class Lz00Compression : CompressionBase
    {
        /*
         * LZ00 decompression support by QPjDDYwQLI thanks to the author of ps2dis
         */

        public override string Name
        {
            get { return "LZ00"; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// Decompress data from a stream.
        /// </summary>
        /// <param name="source">The stream to read from.</param>
        /// <param name="destination">The stream to write to.</param>
        /// <param name="length">Number of bytes to read.</param>
        public override void Decompress(Stream source2, Stream destination, int length)
        {
            byte[] source = new byte[length];
            source2.Read(source, 0, length);
            long offset = 0;

            // Set up information for decompression
            int sourcePointer = 0x40;
            int destPointer   = 0x0;
            int bufferPointer = 0xFEE;

            int sourceLength = (int)(BitConverter.ToUInt32(source, (int)offset + 0x4));
            int destLength   = (int)(BitConverter.ToUInt32(source, (int)offset + 0x30));

            uint key = BitConverter.ToUInt32(source, (int)offset + 0x34);

            byte[] buffer = new byte[0x1000];

            // Start Decompression
            while (sourcePointer < sourceLength && destPointer < destLength)
            {
                byte flag = Get(source[offset + sourcePointer], ref key); // Compression Flag
                sourcePointer++;

                for (int i = 0; i < 8; i++)
                {
                    if ((flag & 0x1) != 0) // Data is not compressed
                    {
                        buffer[bufferPointer] = Get(source[offset + sourcePointer], ref key);
                        destination.WriteByte(buffer[bufferPointer]);
                        sourcePointer++;
                        destPointer++;
                        bufferPointer = (bufferPointer + 1) & 0xFFF;
                    }
                    else // Data is compressed
                    {
                        byte b1 = Get(source[offset + sourcePointer], ref key), b2 = Get(source[offset + sourcePointer + 1], ref key);

                        int bufferOffset = b1 | ((b2 & 0xF0) << 4);
                        int amount = (b2 & 0xF) + 3;
                        sourcePointer += 2;

                        // Copy the data
                        for (int j = 0; j < amount; j++)
                        {
                            buffer[bufferPointer] = buffer[bufferOffset];
                            destination.WriteByte(buffer[bufferPointer]);
                            destPointer++;
                            bufferPointer = (bufferPointer + 1) & 0xFFF;
                            bufferOffset = (bufferOffset + 1) & 0xFFF;
                        }
                    }

                    // Check for out of range
                    if (sourcePointer >= length || destPointer >= destLength)
                        break;

                    flag >>= 1;
                }
            }
        }

        /// <summary>
        /// Compress data from a stream.
        /// </summary>
        /// <param name="source">The stream to read from.</param>
        /// <param name="destination">The stream to write to.</param>
        /// <param name="length">Number of bytes to read.</param>
        /// <param name="settings">Settings to use when compressing.</param>
        public override void Compress(Stream source, Stream destination, int length, ModuleWriterSettings settings)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines if the data is in the specified format.
        /// </summary>
        /// <param name="source">The stream to read from.</param>
        /// <param name="length">Number of bytes to read.</param>
        /// <param name="fname">Name of the file.</param>
        /// <returns>True if the data is in the specified format, false otherwise.</returns>
        public override bool Is(Stream source, int length, string fname)
        {
            if (length > 64 && PTStream.Contains(source, 0, new byte[] { (byte)'L', (byte)'Z', (byte)'0', (byte)'0' }))
            {
                source.Position += 4;

                byte[] buffer = new byte[4];
                source.Read(buffer, 0, 4);

                source.Position -= 8;

                if ((int)BitConverter.ToUInt32(buffer, 0) == length)
                {
                    return true;
                }
            }

            return false;
        }

        private static byte Get(byte value, ref uint key)
        {
            // Generate a new key
            uint x = (((((((key << 1) + key) << 5) - key) << 5) + key) << 7) - key;
            x = (x << 6) - x;
            x = (x << 4) - x;

            key = ((x << 2) - x) + 12345;

            // Now return the value since we have the key
            uint t = (key >> 16) & 0x7FFF;
            return (byte)(value ^ ((((t << 8) - t) >> 15)));
        }
    }
}