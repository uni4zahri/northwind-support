namespace SupportDesk.Presentation.Auth;
public sealed class JwtSettings
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "NorthwindSupport";
    public string Audience { get; set; } = "NorthwindSupportWeb";
    public int LifetimeMinutes { get; set; } = 60;
}
