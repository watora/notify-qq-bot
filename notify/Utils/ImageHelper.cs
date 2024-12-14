using SixLabors.ImageSharp;

namespace Notify.Utils;

public static class ImageHelper 
{   
   public static string GetMediaType(Span<byte> bytes) 
    {
        var image = Image.Load(bytes);
        var format = image.Metadata.DecodedImageFormat;
        var formatString = "";
        if (format != null)
        {
            switch (format.Name.ToLower())
            {
                case "jpeg":
                    formatString = "image/jpeg";
                    break;
                case "png":
                    formatString = "image/png";
                    break;
                case "gif":
                    formatString = "image/gif";
                    break;
                case "webp":
                    formatString = "image/webp";
                    break;
            }
        }
        return formatString;
    }
}