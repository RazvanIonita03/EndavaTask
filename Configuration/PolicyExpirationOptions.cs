namespace CarInsurance.Api.Configuration;

public class PolicyExpirationOptions
{
    public const string SectionName = "PolicyExpiration";
    
    public int CheckIntervalMinutes { get; set; } = 1;
    public int MaxHoursSinceExpiration { get; set; } = 24;
}