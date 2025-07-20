using NATS.Net;

namespace Test;

public class TestNats 
{
    public void TestMethod()
    {
        // Let's see what's available
        var opts = new NatsOpts();
        var conn = new NatsConnection(opts);
    }
}
