using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace KinectDepthMatrix
{
    class UdpSender
    {
        private UdpClient udpClient;

        public UdpSender()
        {
            udpClient = new UdpClient();
        }

        public void SendUpdate(byte fanId, byte power)
        {
            try
            {
                udpClient.Connect("127.0.0.1", 11099);
                udpClient.Send(new byte[] { fanId, power }, 2);
                Trace.WriteLine(string.Format("Send: fan {0} power {1}", fanId, power));
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
            }
        }
    }
}
