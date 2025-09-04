namespace CarInsurance.Api.Dtos;

public record CarDto(long Id, string Vin, string? Make, string? Model, int Year, long OwnerId, string OwnerName, string? OwnerEmail);
public record InsuranceValidityResponse(long CarId, string Date, bool Valid);

public record CreateClaimRequest(DateOnly ClaimDate, string Description, decimal Amount);
public record ClaimResponse(long Id, long CarId, DateOnly ClaimDate, string Description, decimal Amount);
