using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VideoStreamPlayer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private StreamWebSocket _ws;
        private bool _askStop;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Load_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            filePicker.FileTypeFilter.Add("*");

            StorageFile file = await filePicker.PickSingleFileAsync();
            IRandomAccessStream readStream = await file.OpenAsync(FileAccessMode.Read);

            var options = new PropertySet();
            options.Add("framerate", "25");
            //options.Add("fflags", "nobuffer");

            options.Add("vcodec", "copy");

            //options.Add("allowed_media_types", "video");
            //options.Add("stimeout", 100000 * 5);
            //options.Add("reorder_queue_size", 1);
            //options.Add("packet-buffering", 0);
            //options.Add("fflags", "nobuffer");
            //options.Add("probesize", 32);

            var decoder = FFmpegInterop.FFmpegInteropMSS.CreateFFmpegInteropMSSFromStream(readStream, false, false, options);
            var source = decoder.GetMediaStreamSource();

            media.SetMediaStreamSource(source);
            //media.DefaultPlaybackRate = 0.1;
            media.Play();
        }

        private void StreamToDiskStop_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("ms-appx:///Assets/StopHS.png", UriKind.Absolute);
            imgRecord.Source = new BitmapImage(uri);
            if (_ws != null)
            {
                //_ws.Dispose();
                _askStop = true;
            }
        }

        private async void StreamToDiskStart_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("ms-appx:///Assets/RecordHS.png", UriKind.Absolute);
            imgRecord.Source = new BitmapImage(uri);
            _askStop = false;

            try
            {
                var options = new PropertySet();
                options.Add("framerate", "25");
                options.Add("vcodec", "copy");

                var filePicker = new FileSavePicker();
                filePicker.FileTypeChoices.Add("raw H264", new List<string>() { ".h264" });
                filePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;

                StorageFile file = await filePicker.PickSaveFileAsync();

                //var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                var stream = await file.OpenStreamForWriteAsync();
                //long offset = 0;

                _ws = new StreamWebSocket();
                await _ws.ConnectAsync(new Uri(wsuri.Text, UriKind.Absolute));
                DataReader reader = new DataReader(_ws.InputStream);
                do
                {
                    if (_askStop) return;

                    var loaded = await reader.LoadAsync(10240);
                    var buf = reader.ReadBuffer(loaded);
                    try
                    {
                        await stream.WriteAsync(buf.ToArray(), 0, (int)buf.Length);
                        //offset += buf.Length;
                    }
                    catch (Exception)
                    {
                        stream.Dispose();
                        return;
                    }
                }
                while (true);
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.Message);
            }
        }

        private async void Stream_Click1(object sender, RoutedEventArgs e)
        {
            byte[] startSequence = { 0, 0, 0, 1, 0x27, 0x4d };
            var options = new PropertySet();
            //options.Add("framerate", "25");
            //options.Add("vcodec", "copy");

            _ws = new StreamWebSocket();
            await _ws.ConnectAsync(new Uri(wsuri.Text, UriKind.Absolute));

            bool isStarted = false;
            bool isCreated = false;

            var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var output = stream.GetOutputStreamAt(0);
            var writer = new DataWriter(output);

            int maxdelay = 10;
            int delay = maxdelay;
            int index = 0;
            DataReader reader = new DataReader(_ws.InputStream);
            do
            {
                var loaded = await reader.LoadAsync(10 * 1024);
                var buf = reader.ReadBuffer(loaded);
                if (!isStarted)
                {
                    isStarted = true;
                    var signature = buf.ToArray();
                    index = Helpers.FindSequence(signature, 0, startSequence);
                    if (index == -1)
                    {
                        Debug.WriteLine("signature not found, continuing");
                        continue;
                    }

                    buf = signature.AsBuffer(index, signature.Length - index);
                    Debug.WriteLine("buffer created");
                    //await output.WriteAsync(buf);
                    writer.WriteBuffer(buf);
                }
                else
                {
                    //await output.WriteAsync(buf);
                    //await output.FlushAsync();
                    writer.WriteBuffer(buf);
                }

                await writer.StoreAsync();
                if (delay > 0)
                {
                    delay--;
                    Debug.Write("B");
                    //Debug.WriteLine(stream.Position);
                    continue;
                }
                //delay = maxdelay;

                //Debug.Write(".");
                try
                {
                    //await output.WriteAsync(buf);
                    if (!isCreated)
                    {
                        isCreated = true;
                        //stream.Seek((ulong)0);
                        var decoder = FFmpegInterop.FFmpegInteropMSS.CreateFFmpegInteropMSSFromStream(
                            stream, false, false, options);
                        if (decoder == null)
                        {
                            Debug.WriteLine("decoder was null, will retry");
                            continue;
                        }

                        var source = decoder.GetMediaStreamSource();
                        source.Closed += Source_Closed;
                        source.Starting += Source_Starting;
                        source.SampleRequested += Source_SampleRequested;
                        source.SampleRendered += Source_SampleRendered;
                        //source.BufferTime = TimeSpan.FromSeconds(0.2);

                        media.SetMediaStreamSource(source);
                        media.Play();
                    }
                }
                catch (Exception err)
                {
                    Debug.WriteLine(err.ToString());
                    stream.Dispose();
                    reader.Dispose();
                    return;
                }
            }
            while (true);

        }

        private void StartMedia(IRandomAccessStream stream)
        {
            var options = new PropertySet();
            options.Add("framerate", "25");
            options.Add("vcodec", "copy");

            try
            {
                var decoder = FFmpegInterop.FFmpegInteropMSS.CreateFFmpegInteropMSSFromStream(
                    stream, false, false, options);
                if (decoder == null)
                {
                    Debug.WriteLine("Fatal Error: decoder was null");
                    return;
                }

                var source = decoder.GetMediaStreamSource();
                source.Closed += Source_Closed;
                source.Starting += Source_Starting;

                //source.SampleRequested += Source_SampleRequested;
                //source.SampleRendered += Source_SampleRendered;
                //source.BufferTime = TimeSpan.FromSeconds(0.2);

                media.SetMediaStreamSource(source);
                media.Play();
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.ToString());
                stream.Dispose();
                return;
            }
        }

        private async void Stream_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileSavePicker();
            filePicker.FileTypeChoices.Add("raw H264", new List<string>() { ".h264" });
            filePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;

            StorageFile file = await filePicker.PickSaveFileAsync();

            //var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var fwrite = await file.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowReadersAndWriters);
            var fread = await file.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowReadersAndWriters);

            byte[] startSequence = { 0, 0, 0, 1, 0x27, 0x4d };

            // connect to the Pi
            _ws = new StreamWebSocket();
            await _ws.ConnectAsync(new Uri(wsuri.Text, UriKind.Absolute));

            // detect the start of the samples and write only the chunk of the start
            await H264Helper.Detect(_ws.InputStream, fwrite);

            // start a writing task
            /*await*/ Task.Run(async () => await Helpers.WriteFile(_ws.InputStream, fwrite));

            // buffer some data from the network
            //await Task.Delay(500);

            // start casting
            StartMedia(fread);

            Debug.Write("Started");
        }

        private void Source_SampleRendered(MediaStreamSource sender, MediaStreamSourceSampleRenderedEventArgs args)
        {
            Debug.WriteLine("Rendered");
        }

        private void Source_Closed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            Debug.WriteLine("Closed");
        }

        private void Source_Starting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            Debug.WriteLine("Starting");
        }

        private async void Source_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            //if (args.Request.Sample == null) return;
            //var length = args.Request.Sample.Buffer.Length;
            //Debug.WriteLine(length);
            //if (length < 20240)
            //{
            //    var deferral = args.Request.GetDeferral();
            //    //Debug.WriteLine("R");
            //    await Task.Delay(400);
            //    deferral.Complete();
            //}

            //Debug.WriteLine("Sample requested");
        }

    }



}
