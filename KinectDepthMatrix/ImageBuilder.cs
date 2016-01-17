using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace KinectDepthMatrix
{
    public class ImageBuilder
    {
        private BlockingCollection<Area> filterList;

        private bool busyUpdate = false;

        private int width;

        private int height;

        private Area[] renderMatrix;

        public ImageBuilder(int width, int height)
        {
            this.width = width;
            this.height = height;
            filterList = new BlockingCollection<Area>();
            renderMatrix = new Area[width * height];
        }

        public WriteableBitmap Update(DepthImagePixel[] depthPixels, int minDepth, int maxDepth)
        {
            if (busyUpdate)
                return null;
            
            //lock bitmap in ui thread
            WriteableBitmap img = new WriteableBitmap(640, 480, 100, 100, PixelFormats.Bgr32, null);
            img.Lock();
            int pBackBuffer = (int)img.BackBuffer;
            int backBufferStride = img.BackBufferStride;

            List<Area> tmpFilterList = new List<Area>();
            tmpFilterList.AddRange(filterList);

            foreach (Area a in tmpFilterList)
            {
                if (a != null)
                {
                    a.StartCollect();
                }
            }
            
            unsafe
            {
                for (int i = 0; i < depthPixels.Length; i++)
                {
                    // calc pixel position
                    int row = i / width;
                    int column = i % width;
                    // Get a pointer to the back buffer.
                    int cPointer = pBackBuffer;
                    // Find the address of the pixel to draw.
                    cPointer += row * backBufferStride;
                    cPointer += column * 4;

                    DepthImagePixel dpx = depthPixels[i];
                    int color_data = GetColor(i, dpx, minDepth, maxDepth);

                    *((int*)cPointer) = color_data;
                }
            }
            
            // Mark whole image as dirty
            // TODO may only mark pixels that change as dirty ... but that can be complex and this might be faster?
            img.AddDirtyRect(new Int32Rect(0, 0, width, height));

            // Release the back buffer and make it available for display.
            img.Unlock();
            img.Freeze();
            
            foreach (Area a in tmpFilterList)
            {
                if (a != null)
                {
                    a.StopCollect();
                }
            }
            
            return img;
        }

        public void AddToFilter(Area area)
        {
            filterList.Add(area);
        }

        public void UpdateMatrix()
        {
            updateVisibilityMatrix();
        }

        private void updateVisibilityMatrix()
        {
            busyUpdate = true;
            renderMatrix = new Area[width*height];
            foreach (Area area in filterList)
            {
                if (area != null)
                {
                    for (int sy = area.Y1; sy < area.Y2; sy++)
                    {
                        for (int sx = area.X1; sx < area.X2; sx++)
                        {
                            int pos = sx + sy * width;
                            if (pos > renderMatrix.Length -1)
                                pos = renderMatrix.Length - 1;

                            renderMatrix[pos] = area;
                        }
                    }
                }
            }
            busyUpdate = false;
        }

        private int GetColor(int index, DepthImagePixel dpx, int minDepth, int maxDepth)
        {
            // get intesity from depth
            if (dpx.IsKnownDepth)
            {
                short depth = dpx.Depth;
                if (depth < minDepth || depth > maxDepth)
                {
                    return 0;
                }

                byte red = 255;
                byte green = 255;
                byte blue = 255;

                Area pa = renderMatrix[index];
                if (pa != null && pa.MaxDepth >= depth && pa.MinDepth <= depth)
                {
                    red = pa.Red;
                    green = pa.Greem;
                    blue = pa.Blue;

                    pa.CollectDepth(depth);
                }

                int cPercentage = (int)Math.Round(100f / maxDepth * depth);

                int color_data;
                color_data = (red / 100 * cPercentage) << 16; // R
                color_data |= (green / 100 * cPercentage) << 8;   // G
                color_data |= (blue / 100 * cPercentage) << 0;   // B

                return color_data;
            }
            else
            {
                return 0;
            }
        }

        public void ResetFilter()
        {
            while (filterList.Count > 0)
            {
                Area item;
                filterList.TryTake(out item);
            }
        }
    }
}
