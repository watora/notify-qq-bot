namespace Notify.Client.Utils;

public static class Helper 
{
    public static string GetTag()
    {
        if (OperatingSystem.IsBrowser())
        {
            return ":wasm";
        }
        return ":server";
    }
}