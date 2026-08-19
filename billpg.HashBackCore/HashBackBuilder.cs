using System;
namespace billpg.HashBackCore;

public static class HashBackBuilder
{
    public static (string token, string hash) Build(string host, long now, string unus, string verify)
        => Helpers.Build(host, now, unus, verify);

    public static (string token, string hash) Build(string host, DateTime now, string unus, string verify)
        => Build(host, now.ToUnixTimeSeconds(), unus, verify);

    public static (string token, string hash) Build(string host, DateTime now, string verify)
        => Build(host, now, Helpers.GenerateUnus(), verify);

    public static (string token, string hash) Build(string host, string verify)
        => Build(host, DateTime.UtcNow, Helpers.GenerateUnus(), verify);
}
