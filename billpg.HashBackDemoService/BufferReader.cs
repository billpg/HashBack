using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackDemoService;

internal class BufferReader
{
    private readonly NetworkStream str;
    private byte[] buffer;
    private int readIndex;
    private int freeIndex;
    private bool expectLF;
    private int size => freeIndex - readIndex;
    private int freeCount => buffer.Length - freeIndex;

    private const byte CR = (byte)'\r';
    private const byte LF = (byte)'\n';

    internal BufferReader(NetworkStream str)
    {
        this.str = str;
        this.buffer = new byte[1024];
        this.readIndex = 0;
        this.freeIndex = 0;
        this.expectLF = false;             
    }

    internal async Task<string> ReadLineAsString()
    {
        var lineAsBytes = await ReadLine();
        return Encoding.UTF8.GetString(lineAsBytes);
    }

    internal async Task<Array> ReadBlock(int blockSize)
        => await ReadInternal(() => blockSize, () => { });    

    internal async Task<byte[]> ReadLine()
    {
        return await ReadInternal(FindLineLength, EatEndOfLine);
        int? FindLineLength()
        {
            for (int curr = readIndex; curr < freeIndex; curr++)
            {
                byte atCurr = buffer[curr];
                if (atCurr == CR || atCurr == LF)
                    return curr - readIndex;
            }
            return null;
        }

        void EatEndOfLine()
        {
            /* Consume one byte, which will be the CR or LF that
             * FindLineLength must have found. */
            byte atRead = buffer[readIndex];
            readIndex++;

            /* If it was a CR, expect an LF next. */
            expectLF = atRead == CR;
        }
    }

    private async Task<byte[]> ReadInternal(Func<int?> findBlockLength, Action consumeTerminator)
    {
        /* Handle when the next byte is an LF after a previous CR. */
        if (expectLF && size > 0)
        {
            expectLF = false;
            if (buffer[readIndex] == LF)
                readIndex++;
        }

        /* Keep trying until we find a complete block. */
        while (true)
        {
            /* Ask the caller to find the block termination. */
            int? blockLength = findBlockLength();
            if (blockLength.HasValue)
            {
                var block = ExtractBlock(blockLength.Value);
                consumeTerminator();
                return block;
            }

            /* If no space left, start a new buffer. */
            if (freeCount == 0)
            {
                /* If the buffer is full, we can't continue. */
                if (readIndex == 0)
                    throw new BadRequestException("Line too long.");

                /* Make a new buffer and initialize with the old buffer. */
                var newBuffer = new byte[1024];
                Buffer.BlockCopy(buffer, readIndex, newBuffer, 0, size);
                buffer = newBuffer;
                freeIndex = size;
                readIndex = 0;
            }

            /* Populate the free space in the buffer. */
            int bytesIn = await str.ReadAsync(buffer, freeIndex, freeCount);
            freeIndex += bytesIn;

            /* Loop around and try finding a complete block now. */
        }
    }

    private byte[] ExtractBlock(int blockLength)
    {
        var block = new byte[blockLength];
        Buffer.BlockCopy(buffer, readIndex, block, 0, blockLength);
        readIndex += blockLength;
        return block;
    }

}
