namespace rtaNetworking.Streaming
{
    public interface IWebServerCallback
    {
        void OnClientConnect(int chanel);
        void OnClientDisconnect(int chanel);
        void OnClientRequestShot(int chanel);
    }
}
