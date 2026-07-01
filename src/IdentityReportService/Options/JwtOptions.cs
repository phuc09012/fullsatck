namespace IdentityReportService.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "LibraryAuth";
    public string Audience { get; set; } = "LibraryUsers";
    public string Key { get; set; } = "ChangeThisKeyToSomethingAtLeast32CharsLong!";
    public int ExpiryMinutes { get; set; } = 480;
}
