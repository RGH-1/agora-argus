using System.Text.Json;

namespace Argus.Contracts;

/// <summary>The one JSON setting both sides use, so a message serialized by a client reads back the same in Argus.</summary>
public static class ArgusJson
{
    /// <summary>camelCase names, case-insensitive reading (the ASP.NET Core defaults).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}