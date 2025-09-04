namespace CarInsurance.Api.Models;

public class ProcessedExpiration
{
    public long Id { get; set; }
    public long PolicyId { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public DateTime ProcessedAt { get; set; }
}