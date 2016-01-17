using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.Windows.Controls;
using System.ComponentModel;
using System.Diagnostics;

namespace KinectDepthMatrix
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor sensor;

        private DepthImagePixel[] depthPixels;

        private ImageBuilder imageBuilder;

        private UdpSender udpSender;

        private int minDepth;

        private int maxDepth;

        private readonly BackgroundWorker worker = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = KinectDataContext.Instance;
            this.imageBuilder = new ImageBuilder(640, 480);
            this.udpSender = new UdpSender();

            initSensor();

            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_Completed;
        }

        private void initSensor()
        {
            bool hasFound = false;
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                    this.sensor.DepthFrameReady += this.DepthImageReady;
                    this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                    this.sensor.Start();

                    this.sensor.ElevationAngle = 0;

                    hasFound = true;
                    KinectDataContext.Instance.StatusMessage = "Initialized sensor";
                    break;
                }
            }

            if (!hasFound)
                throw new Exception("No Kinect connected");
        }

        private void AddAreaClick(object sender, RoutedEventArgs e)
        {
            Area newArea = new Area(new Point(0, 0), new Point(320, 240));
            newArea.AttachBuilder(imageBuilder);
            KinectDataContext.Instance.AddArea(newArea);
        }

        private void DeleteAreasClick(object sender, RoutedEventArgs e)
        {
            KinectDataContext.Instance.AreaList.Clear();
            KinectDataContext.Instance.OnPropertyChanged("AreaList");
        }

        private void OnWindowClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // stop sensor
            this.sensor.Stop();
            // free memory and terminate streams
            this.sensor.Dispose();
        }

        private void DepthImageReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            if (worker.IsBusy)
                return;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    minDepth = depthFrame.MinDepth;
                    maxDepth = depthFrame.MaxDepth;
                    depthFrame.CopyDepthImagePixelDataTo(depthPixels);
                    worker.RunWorkerAsync();
                }
                else
                {
                    KinectDataContext.Instance.StatusMessage = "No depth frame received";
                }
            }
        }

        private void FilterSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ListView)
            {
                ListView lvSender = (ListView)sender;
                imageBuilder.ResetFilter();
                foreach (Area item in lvSender.SelectedItems)
                {
                    imageBuilder.AddToFilter(item);
                    KinectDataContext.Instance.CurrentArea = item;
                }
                imageBuilder.UpdateMatrix();
            }
            
        }

        private void SetCurrentValueAsNorm(object sender, RoutedEventArgs e)
        {
            Area cArea = KinectDataContext.Instance.CurrentArea;
            if (cArea != null)
            {
                cArea.NormalValue = cArea.CurrentValue;
            }
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Stopwatch timer = Stopwatch.StartNew();            

            WriteableBitmap img = imageBuilder.Update(depthPixels, minDepth, maxDepth);

            timer.Stop();
            TimeSpan ts = timer.Elapsed;

            e.Result = img;
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    KinectDataContext.Instance.StatusMessage = String.Format("Frame took: {0:00}:{1:00}:{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                }
            ));
        }

        private void worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is WriteableBitmap)
            {
                WriteableBitmap img = (WriteableBitmap)e.Result;
                KinectDataContext.Instance.ImageSource = img;
            }

            foreach (Area a in KinectDataContext.Instance.AreaList)
            {
                if (a.direction == Direction.NOTHING)
                {
                    udpSender.Reset(a.ForwardFan);
                    udpSender.Reset(a.BackwardFan);
                    return;
                }
                
                int fanPercentage = (int)Math.Ceiling(100f / a.MaxFanDiff * a.Diff);
                if (fanPercentage > 100)
                    fanPercentage = 100;
                if (fanPercentage < 0)
                    fanPercentage = 0;

                byte power = (byte)(255f / 100 * fanPercentage);

                if (a.direction == Direction.CLOSER)
                {
                    udpSender.SendUpdate(a.ForwardFan, power);
                    udpSender.SendUpdate(a.BackwardFan, 0);
                }
                else
                {
                    udpSender.SendUpdate(a.ForwardFan, 0);
                    udpSender.SendUpdate(a.BackwardFan, power);
                }
            }
        }
    }
}
