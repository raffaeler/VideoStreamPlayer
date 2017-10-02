using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace VideoStreamPlayer
{
    public static class H264Helper
    {
        public async static Task Detect(IInputStream network, IOutputStream destination)
        {
            byte[] startSequence = { 0, 0, 0, 1, 0x27, 0x4d };
            DataReader reader = new DataReader(network);
            do
            {
                var loaded = await reader.LoadAsync(100 * 1024);
                var buf = reader.ReadBuffer(loaded);
                var signature = buf.ToArray();

                int index = Helpers.FindSequence(signature, 0, startSequence);
                if (index != -1)
                {
                    buf = signature.AsBuffer(index, signature.Length - index);
                    await Helpers.WriteFile(buf, destination);
                    Debug.WriteLine("signature found");
                    return;
                }
                Debug.WriteLine("signature not found, continuing");
            }

            while (true);
        }

    }
}
