using System.ComponentModel.DataAnnotations;

public class JwtSettingsOptions
{
    public const string sectionName = "JwtSettings";

    [Required, MaxLength(10)]
    public string SecretKey { get; set; }

    [CreditCard]
    public string Issuer { get; set; }

    [Required]
    public string Audience { get; set; }

    [Required, Range(5, 10)]
    public int ExpiryTime { get; set; }
}