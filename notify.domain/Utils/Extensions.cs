using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;

namespace Notify.Domain.Utils;

public static class NotifyExtension
{
    public static string ToCustomString<T>(this T e) where T : Enum
    {
        return e.GetType().GetField(e.ToString())?.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? "";
    }

    public static T DeserializeAnonymousType<T>(string json, T defaultValue) where T : class
    {
        var obj = System.Text.Json.JsonSerializer.Deserialize(json, defaultValue!.GetType());
        if (obj == null)
        {
            return defaultValue;
        }
        return (T)obj;
    }
}