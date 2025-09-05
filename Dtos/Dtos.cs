using System.Text.Json.Serialization;

namespace CarInsurance.Api.Dtos;

public record CarDto(long Id, string Vin, string? Make, string? Model, int Year, long OwnerId, string OwnerName, string? OwnerEmail);
public record InsuranceValidityResponse(long CarId, string Date, bool Valid);

public record CreateClaimRequest(DateOnly ClaimDate, string Description, decimal Amount);
public record ClaimResponse(long Id, long CarId, DateOnly ClaimDate, string Description, decimal Amount);

public record CarHistoryResponse(long CarId, List<CarHistoryEvent> Events);

// Additional DTOs for creating cars and insurance policies
public record CreateCarRequest(string Vin, string? Make, string? Model, int YearOfManufacture, long OwnerId);
public record CreateInsurancePolicyRequest(string Provider, DateOnly StartDate, DateOnly EndDate);
public record InsurancePolicyDto(long Id, long CarId, string? Provider, DateOnly StartDate, DateOnly EndDate);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "eventType")]
[JsonDerivedType(typeof(PolicyStartEvent), "PolicyStart")]
[JsonDerivedType(typeof(PolicyEndEvent), "PolicyEnd")]
[JsonDerivedType(typeof(ClaimEvent), "Claim")]
public abstract record CarHistoryEvent(DateOnly Date, string EventType);

public record PolicyStartEvent(DateOnly Date, string Provider) : CarHistoryEvent(Date, "PolicyStart");
public record PolicyEndEvent(DateOnly Date, string Provider) : CarHistoryEvent(Date, "PolicyEnd");
public record ClaimEvent(DateOnly Date, string Description, decimal Amount) : CarHistoryEvent(Date, "Claim");
