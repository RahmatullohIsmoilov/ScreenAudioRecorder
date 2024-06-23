using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NAudio.Wave;
using SharpAvi;
using SharpAvi.Codecs;
using SharpAvi.Output;

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
            timer1.Start();
        }

        private void BtnStop_Clicked(object sender, EventArgs e)
        {
            timer1.Stop();
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
        private readonly Stopwatch silenceWatch = new Stopwatch();
        private readonly byte[] silenceBuffer = new byte[bitsPerSample * channels * sampleRate / 8];
        private long silenceTicks;

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

            //videoStream = writer.AddUncompressedVideoStream(rectangle.Width, rectangle.Height);
            videoStream = writer.AddMpeg4VcmVideoStream(
                        rectangle.Width, rectangle.Height, framerate, codec: CodecIds.X264);
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
            silenceWatch.Start();
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
            var elapsedTicks = silenceWatch.ElapsedTicks;
            var silenceTime = elapsedTicks - silenceTicks;
            silenceTicks = elapsedTicks;
            lock (this)
            {
                if (e.BytesRecorded > 0)
                {
                    audioStream.WriteBlock(e.Buffer, 0, e.BytesRecorded);
                }
                else if (silenceTime > 0)
                {
                    var slienceBytes = silenceBuffer.Length * silenceTime / TimeSpan.TicksPerSecond;
                    audioStream.WriteBlock(silenceBuffer, 0, (int)slienceBytes);
                }
            }
        }

        public void Dispose()
        {
            waitableTimer?.Stop();
            audioSource?.StopRecording();
            writer?.Close();
            silenceWatch?.Stop();
            waitableTimer?.Dispose();
            captureGraphics?.Dispose();
            captureBitmap?.Dispose();
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

    public class WaitableTimer : Component
    {
        public long IntervalTicks { get; set; }
        public decimal Interval
        {
            get
            {
                return (decimal)IntervalTicks / TimeSpan.TicksPerMillisecond;
            }
            set
            {
                IntervalTicks = (long)value * TimeSpan.TicksPerMillisecond;
            }
        }

        public event EventHandler Elapsed;

        private readonly TimerHandle hTimer;
        private readonly EventWaitHandle cancelHandle = new ManualResetEvent(false);
        private Thread thread;
        private bool enabled;

        public WaitableTimer()
        {
            hTimer = new TimerHandle();
        }

        public WaitableTimer(IContainer container) : this()
        {
            if (container == null)
            {
                throw new ArgumentNullException("container");
            }
            container.Add(this);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            hTimer.Dispose();
            cancelHandle.Dispose();
        }

        public void Start()
        {
            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
        }

        public bool Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                if (enabled != value)
                {
                    enabled = value;
                    if (value)
                    {
                        cancelHandle.Reset();
                        thread = new Thread(new ParameterizedThreadStart(Loop));
                        thread.Start(this);
                    }
                    else
                    {
                        cancelHandle.Set();
                        thread.Join();
                        thread = null;
                    }
                }
            }
        }

        protected virtual void OnElapsed(EventArgs e)
        {
            Elapsed?.Invoke(this, e);
        }

        private static void Loop(object args)
        {
            var owner = (WaitableTimer)args;
            WaitHandle[] handles = new WaitHandle[] { owner.cancelHandle, owner.hTimer };
            long ticks = owner.IntervalTicks;
            DateTime utc = DateTime.UtcNow;
            long dueTime = utc.ToFileTimeUtc() + ticks;
            owner.hTimer.SetDueTime(dueTime);
            while (WaitHandle.WaitAny(handles) == WAIT_1)
            {
                owner.OnElapsed(EventArgs.Empty);
                dueTime += ticks;
                owner.hTimer.SetDueTime(dueTime);
            }
        }

        private class TimerHandle : WaitHandle
        {
            public TimerHandle()
            {
                var hTimer = CreateWaitableTimer(IntPtr.Zero, false, null);
                if (hTimer == null || hTimer.IsInvalid)
                {
                    throw new Win32Exception();
                }
                SafeWaitHandle = hTimer;
            }

            public void SetTicks(long ticks)
            {
                long dueTime = -1 * ticks;
                if (!SetWaitableTimer(SafeWaitHandle, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false))
                {
                    throw new Win32Exception();
                }
            }

            public void SetDueTime(long dueTime)
            {
                if (!SetWaitableTimer(SafeWaitHandle, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false))
                {
                    throw new Win32Exception();
                }
            }

            public void Cancel()
            {
                if (!CancelWaitableTimer(SafeWaitHandle))
                {
                    throw new Win32Exception();
                }
            }

        }

        const int WAIT_1 = 1;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeWaitHandle CreateWaitableTimer(
            IntPtr lpTimerAttributes, bool bManualReset, string lpTimerName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetWaitableTimer(
            SafeWaitHandle hTimer, [In] ref long pDueTime, int lPeriod,
            IntPtr pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, bool fResume);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CancelWaitableTimer(SafeWaitHandle hTimer);
    }
}