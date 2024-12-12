namespace Notify.Domain.Config.Options;

public class AuthOption
{
    public required string Owner { get; set; }

    public required List<string> Admin { get; set; }

    public required List<string> User { get; set; }
}