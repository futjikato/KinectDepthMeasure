using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;

namespace KinectDepthMatrix
{
    class UdpSender
    {
        private const int PORT_FANUDP = 11099;

        private const int PORT_PROJUDP = 11098;

        private UdpClient fanUdpClient;

        private UdpClient projectionUdpClient;

        private readonly Dictionary<byte, byte> powerMap = new Dictionary<byte, byte>();

        private readonly Dictionary<byte, DateTime> lastSendMap = new Dictionary<byte, DateTime>();

        public UdpSender()
        {
            fanUdpClient = new UdpClient();
            fanUdpClient.Connect("127.0.0.1", PORT_FANUDP);

            projectionUdpClient = new UdpClient();
            projectionUdpClient.Connect("127.0.0.1", PORT_PROJUDP);
        }

        public void SendUpdate(byte fanId, byte power)
        {
            if (fanId <= 0)
            {
                return;
            }

            DateTime now = DateTime.Now;
            DateTime lastUpdateSend;
            if (!lastSendMap.TryGetValue(fanId, out lastUpdateSend))
            {
                Trace.WriteLine(string.Format("Inital set last send for fan {0}", fanId));
                lastSendMap[fanId] = DateTime.Now;
            }
            else
            {
                TimeSpan delta = now - lastUpdateSend;
                if (delta.TotalMilliseconds < 500)
                {
                    // skip data
                    Trace.WriteLine(string.Format("skip {0} for frame after {1}", fanId, delta.TotalMilliseconds));
                    return;
                }
                lastSendMap[fanId] = DateTime.Now;
            }

            if (power < 150)
            {
                power = 0;
            }

            try
            {
                fanUdpClient.Send(new byte[] { fanId, power }, 2);
                projectionUdpClient.Send(new byte[] { fanId, power }, 2);
                Trace.WriteLine(string.Format("{0} => {1}", fanId, power));
                powerMap[fanId] = power;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
            }
        }

        public void Reset(byte fanId)
        {
            byte power;
            if (powerMap.TryGetValue(fanId, out power))
            {
                if (power > 0)
                {
                    fanUdpClient.Send(new byte[] { fanId, 0 }, 2);
                    projectionUdpClient.Send(new byte[] { fanId, 0 }, 2);
                    powerMap[fanId] = 0;
                }
            }
        }
    }
}
