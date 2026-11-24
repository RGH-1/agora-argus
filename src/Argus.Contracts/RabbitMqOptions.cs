namespace Argus.Contracts;

/// <summary>Where the RabbitMQ broker is (design draft §5.1). Bound from configuration; the password comes from user-secrets or .env.</summary>
public class RabbitMqOptions
{
    /// <summary>
    /// "127.0.0.1" when running from Visual Studio, "rabbitmq" (the Compose service name) inside Docker.
    /// Not "localhost": Compose listens on IPv4 only, and the failed IPv6 attempt first costs ~2 s per connect on Windows.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}