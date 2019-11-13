namespace rtaNetworking.Streaming
{
    public interface IWebServerCallback
    {
        void OnClientConnect(int chanel, int stream = 1);
        void OnClientDisconnect(int chanel);
        void OnClientRequestShot(int chanel, int stream = 1);
    }
}
