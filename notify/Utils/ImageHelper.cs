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
            switch (format.Name)
            {
                case "Jpeg":
                    formatString = "image/jpeg";
                    break;
                case "Png":
                    formatString = "image/png";
                    break;
                case "Gif":
                    formatString = "image/gif";
                    break;
                case "WebP":
                    formatString = "image/webp";
                    break;
            }
        }
        return formatString;
    }
}