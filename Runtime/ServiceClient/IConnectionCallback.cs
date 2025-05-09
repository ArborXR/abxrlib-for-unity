public interface IConnectionCallback
{
    void OnConnected(ISdkService service);

    void OnDisconnected(bool isRetrying);
}