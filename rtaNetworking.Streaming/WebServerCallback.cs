using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace rtaNetworking.Streaming
{
    public interface IWebServerCallback
    {
        void OnClientConnect(int chanel);
        void OnClientDisconnect(int chanel);
        void OnClientRequestShot(int chanel);
    }
}
