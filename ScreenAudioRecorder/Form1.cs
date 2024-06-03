using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAudio.Wave;
using SharpAvi.Output;
using SharpAvi.Codecs;

namespace ScreenAudioRecorder
{
    public partial class Form1 : Form
    {
        private Stopwatch stopwatch;
        private string outputFileName;
        private SimpleRecorder recorder;
        private int frameRate = 10;

        public Form1()
        {
            InitializeComponent();
            stopwatch = new Stopwatch();
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            stopwatch = new Stopwatch();
        }

        private void BtnRecord_Clicked(object sender, EventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "AVI files | *.avi"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            outputFileName = dialog.FileName;
            btn_record.Enabled = false;
            btn_stop.Enabled = true;

            var screenBounds = Screen.PrimaryScreen.Bounds;
            recorder = new SimpleRecorder(outputFileName, screenBounds);

            stopwatch.Start();
        }

        private void BtnStop_Clicked(object sender, EventArgs e)
        {
            btn_record.Enabled = true;
            btn_stop.Enabled = false;
            recorder.Dispose();
            stopwatch.Stop();
            stopwatch.Reset();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            lbl_Timer.Text = string.Format("{0:hh\\:mm\\:ss}", stopwatch.Elapsed);
        }
    }

    public class SimpleRecorder : IDisposable
    {
        private const int framerate = 10;
        private const int channels = 2;
        private const int sampleRate = 44100;
        private const int bitsPerSample = 16;

        private readonly AviWriter writer;
        private readonly IAviVideoStream videoStream;
        private readonly IAviAudioStream audioStream;
        private readonly WasapiLoopbackCapture audioSource;
        private readonly WaitableTimer waitableTimer = new WaitableTimer();
        private readonly Rectangle captureRectangle;
        private readonly Bitmap captureBitmap;
        private readonly byte[] captureBuffer;
        private readonly Graphics captureGraphics;

        public SimpleRecorder(string fileName, Rectangle rectangle)
        {
            captureRectangle = rectangle;
            captureBitmap = new Bitmap(rectangle.Width, rectangle.Height);
            captureBuffer = new byte[rectangle.Width * rectangle.Height * 4];
            captureGraphics = Graphics.FromImage(captureBitmap);

            writer = new AviWriter(fileName)
            {
                FramesPerSecond = framerate,
                EmitIndex1 = true,
            };

            videoStream = writer.AddUncompressedVideoStream(rectangle.Width, rectangle.Height);
            videoStream.Name = "Screencast";

            audioStream = writer.AddAudioStream(channels, sampleRate, bitsPerSample);
            audioStream.Name = "Voice";

            audioSource = new WasapiLoopbackCapture
            {
                WaveFormat = new WaveFormat(sampleRate, bitsPerSample, channels)
            };
            audioSource.DataAvailable += AudioSource_DataAvailable;

            waitableTimer.IntervalTicks = TimeSpan.TicksPerSecond / framerate;
            waitableTimer.Elapsed += WaitableTimer_Elapsed;
            waitableTimer.Start();
            audioSource.StartRecording();
        }

        private void WaitableTimer_Elapsed(object sender, EventArgs e)
        {
            GetScreenshot(captureBuffer);
            lock (this)
            {
                videoStream.WriteFrame(true, captureBuffer, 0, captureBuffer.Length);
            }
        }

        private void AudioSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded > 0)
            {
                lock (this)
                {
                    audioStream.WriteBlock(e.Buffer, 0, e.BytesRecorded);
                }
            }
        }

        public void Dispose()
        {
            waitableTimer.Stop();
            audioSource?.StopRecording();
            writer.Close();
            captureGraphics?.Dispose();
            captureBitmap.Dispose();
        }

        private void GetScreenshot(byte[] buffer)
        {
            captureGraphics.CopyFromScreen(
                captureRectangle.Location, Point.Empty, captureRectangle.Size);
            var bits = captureBitmap.LockBits(new Rectangle(Point.Empty, captureBitmap.Size),
                                        ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);
            captureBitmap.UnlockBits(bits);
        }
    }

    public class WaitableTimer : IDisposable
    {
        private readonly System.Threading.Timer timer;
        public event EventHandler Elapsed;

        public WaitableTimer()
        {
            timer = new System.Threading.Timer(TimerCallback);
        }

        public long IntervalTicks { get; set; }

        public void Start()
        {
            var interval = TimeSpan.FromTicks(IntervalTicks).Milliseconds;
            timer.Change(interval, interval);
        }

        public void Stop()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void TimerCallback(object state)
        {
            Elapsed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}
