using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KinectDepthMatrix
{
    public class KinectDataContext : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Area> AreaList { get; set; }

        private WriteableBitmap source;
        public WriteableBitmap ImageSource {
            get
            {
                return source;
            }
            set
            {
                source = value;
                OnPropertyChanged("ImageSource");
            }
        }

        private Area current;
        public Area CurrentArea
        {
            get
            {
                return current;
            }
            set
            {
                current = value;
                OnPropertyChanged("CurrentArea");
            }
        }

        private string status;
        public string StatusMessage
        {
            get
            {
                return status;
            }
            set
            {
                status = value;
                OnPropertyChanged("StatusMessage");
            }
        }

        private static KinectDataContext instance;
        
        public static KinectDataContext Instance {
            get
            {
                if (instance == null)
                {
                    instance = new KinectDataContext();
                    instance.AreaList = new ObservableCollection<Area>();
                    instance.ImageSource = new WriteableBitmap(640, 480, 100, 100, PixelFormats.Bgr32, null);
                    instance.StatusMessage = "Loading ...";
                }

                return instance;
            }
        }

        public void AddArea(Area a)
        {
            AreaList.Add(a);
            OnPropertyChanged("AreaList");
        }

        public void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }

    public class Point
    {
        private int xVal;
        public int X
        {
            get
            {
                return xVal;
            }
            set
            {
                xVal = value;
            }
        }

        private int yVal;
        public int Y
        {
            get
            {
                return yVal;
            }
            set
            {
                yVal = value;
            }
        }

        public Point(int x, int y)
        {
            xVal = x;
            yVal = y;
        }

        public override string ToString()
        {
            return string.Format("({0}/{1})", xVal, yVal);
        }
    }

    public class Area : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static int nextIndex = 0;

        private int index;
        private Point p1;
        private Point p2;

        private byte colorRed = 255;
        private byte colorGreen = 0;
        private byte colorBlue = 0;

        private short minDepth = 0;
        private short maxDepth = short.MaxValue;

        private int threshold = 10;

        private ImageBuilder imgBuilder;

        public bool Active = false;

        private int totalDepth;
        private int normalValue = 0;
        private int maxFanDiff = 1000;
        private byte adjustmentRate = 20;

        private byte forwardFan = 0;
        private byte backwardFan = 0;

        public int Diff;
        public Direction direction;

        public Point[] Edges
        {
            get
            {
                return new Point[] { p1, p2 };
            }
        }

        public int Index
        {
            get
            {
                return index;
            }
        }

        public string Ident
        {
            get
            {
                return string.Format("#{0} ({1},{2})", Index, p1, p2);
            }
        }

        public int X1
        {
            get
            {
                return p1.X;
            }
            set
            {
                p1.X = value;
                OnPropertyChanged("Edges");
                OnPropertyChanged("Ident");
                imgBuilder.UpdateMatrix();
            }
        }

        public int Y1
        {
            get
            {
                return p1.Y;
            }
            set
            {
                p1.Y = value;
                OnPropertyChanged("Edges");
                OnPropertyChanged("Ident");
                imgBuilder.UpdateMatrix();
            }
        }

        public int X2
        {
            get
            {
                return p2.X;
            }
            set
            {
                p2.X = value;
                OnPropertyChanged("Edges");
                OnPropertyChanged("Ident");
                imgBuilder.UpdateMatrix();
            }
        }

        public int Y2
        {
            get
            {
                return p2.Y;
            }
            set
            {
                p2.Y = value;
                OnPropertyChanged("Edges");
                OnPropertyChanged("Ident");
                imgBuilder.UpdateMatrix();
            }
        }

        public short MaxDepth
        {
            get
            {
                return maxDepth;
            }
            set
            {
                maxDepth = value;
                OnPropertyChanged("MaxDepth");
            }
        }

        public short MinDepth
        {
            get
            {
                return minDepth;
            }
            set
            {
                minDepth = value;
                OnPropertyChanged("MinDepth");
            }
        }

        public int Threshold
        {
            get
            {
                return threshold;
            }
            set
            {
                threshold = value;
                OnPropertyChanged("Threshold");
            }
        }

        public int NormalValue
        {
            get
            {
                return normalValue;
            }
            set
            {
                normalValue = value;
                OnPropertyChanged("NormalValue");
            }
        }

        public string ColorCode
        {
            get
            {
                return string.Format("{0},{1},{2}", colorRed, colorGreen, colorBlue);
            }
            set
            {
                string[] splitted = value.Split(',');
                if (splitted.Length != 3)
                {
                    return;
                }

                int iRed;
                if (!int.TryParse(splitted[0], out iRed))
                    return;

                int iGreen;
                if (!int.TryParse(splitted[1], out iGreen))
                    return;

                int iBlue;
                if (!int.TryParse(splitted[2], out iBlue))
                    return;

                colorBlue = Convert.ToByte(iBlue);
                colorGreen = Convert.ToByte(iGreen);
                colorRed = Convert.ToByte(iRed);
                OnPropertyChanged("ColorCode");
            }
        }

        public byte Red
        {
            get
            {
                return colorRed;
            }
        }

        public byte Greem
        {
            get
            {
                return colorGreen;
            }
        }

        public byte Blue
        {
            get
            {
                return colorBlue;
            }
        }

        public int CurrentValue
        {
            get
            {
                return totalDepth;
            }
        }

        public int MaxFanDiff
        {
            get
            {
                return maxFanDiff;
            }
            set
            {
                maxFanDiff = value;
                OnPropertyChanged("MaxFanDiff");
            }
        }

        public byte ForwardFan
        {
            get
            {
                return forwardFan;
            }
            set
            {
                forwardFan = value;
                OnPropertyChanged("ForwardFan");
            }
        }

        public byte BackwardFan
        {
            get
            {
                return backwardFan;
            }
            set
            {
                backwardFan = value;
                OnPropertyChanged("BackwardFan");
            }
        }

        public byte AdjustRate
        {
            get
            {
                return adjustmentRate;
            }
            set
            {
                if (adjustmentRate > 100)
                    adjustmentRate = 100;

                adjustmentRate = value;
                OnPropertyChanged("AdjustRate");
            }
        }

        public Area(Point p1, Point p2)
        {
            this.p1 = p1;
            this.p2 = p2;
            this.index = Area.nextIndex++;
        }

        public void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public void AttachBuilder(ImageBuilder builder)
        {
            this.imgBuilder = builder;
        }

        public void CollectDepth(short depth)
        {
            totalDepth += depth;
        }

        public void StartCollect()
        {
            totalDepth = 0;
        }

        public void StopCollect()
        {
            int diff = totalDepth - normalValue;
            Direction dir = Direction.AWAY;

            normalValue += (int)Math.Floor(diff / 100f * adjustmentRate);
            if (diff < 0)
            {
                dir = Direction.CLOSER;
                diff = -diff;
            }

            Diff = diff;
            direction = dir;

            OnPropertyChanged("NormalValue");
            OnPropertyChanged("CurrentValue");
        }
    }

    public enum Direction
    {
        CLOSER, AWAY
    }
}
