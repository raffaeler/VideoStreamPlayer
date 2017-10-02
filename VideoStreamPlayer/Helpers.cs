using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace VideoStreamPlayer
{
    public static class Helpers
    {
        public async static Task WriteFile(IBuffer buffer, IOutputStream file)
        {
            try
            {
                await file.WriteAsync(buffer);
            }
            catch (Exception)
            {
                file.Dispose();
                return;
            }
        }

        public async static Task WriteFile(IInputStream incoming, IOutputStream file)
        {
            DataReader reader = new DataReader(incoming);
            do
            {
                var loaded = await reader.LoadAsync(10240);
                var buf = reader.ReadBuffer(loaded);
                try
                {
                    await file.WriteAsync(buf);
                    Debug.Write("W");
                }
                catch (Exception)
                {
                    file.Dispose();
                    return;
                }
            }
            while (true);
        }

        //https://stackoverflow.com/questions/25400610/most-efficient-way-to-find-pattern-in-byte-array
        /// <summary>Looks for the next occurrence of a sequence in a byte array</summary>
        /// <param name="array">Array that will be scanned</param>
        /// <param name="start">Index in the array at which scanning will begin</param>
        /// <param name="sequence">Sequence the array will be scanned for</param>
        /// <returns>
        ///   The index of the next occurrence of the sequence of -1 if not found
        /// </returns>
        public static int FindSequence(byte[] array, int start, byte[] sequence)
        {
            int end = array.Length - sequence.Length; // past here no match is possible
            byte firstByte = sequence[0]; // cached to tell compiler there's no aliasing

            while (start < end)
            {
                // scan for first byte only. compiler-friendly.
                if (array[start] == firstByte)
                {
                    // scan for rest of sequence
                    for (int offset = 1; offset < sequence.Length; ++offset)
                    {
                        if (array[start + offset] != sequence[offset])
                        {
                            break; // mismatch? continue scanning with next byte
                        }
                        else if (offset == sequence.Length - 1)
                        {
                            return start; // all bytes matched!
                        }
                    }
                }
                ++start;
            }

            // end of array reached without match
            return -1;
        }

    }
}
