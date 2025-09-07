using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api")]
public class CarsController(CarService service, IPolicyExpirationService expirationService) : ControllerBase
{
    private readonly CarService _service = service;
    private readonly IPolicyExpirationService _expirationService = expirationService;

    [HttpGet("cars")]
    public async Task<ActionResult<List<CarDto>>> GetCars()
        => Ok(await _service.ListCarsAsync());

    [HttpGet("cars/{carId:long}/insurance-valid")]
    public async Task<ActionResult<InsuranceValidityResponse>> IsInsuranceValid(long carId, [FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var parsed))
            return BadRequest("Invalid date format. Use YYYY-MM-DD.");

        try
        {
            var valid = await _service.IsInsuranceValidAsync(carId, parsed);
            return Ok(new InsuranceValidityResponse(carId, parsed.ToString("yyyy-MM-dd"), valid));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("cars")]
    public async Task<ActionResult<CarDto>> CreateCar([FromBody] CreateCarRequest request)
    {
        try
        {
            var car = await _service.CreateCarAsync(request);
            return CreatedAtAction(nameof(GetCars), new { id = car.Id }, car);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("cars/{carId:long}/policies")]
    public async Task<ActionResult<InsurancePolicyDto>> CreateInsurancePolicy(long carId, [FromBody] CreateInsurancePolicyRequest request)
    {
        try
        {
            var policy = await _service.CreateInsurancePolicyAsync(carId, request);
            return CreatedAtAction(nameof(CreateInsurancePolicy), new { carId = policy.CarId, policyId = policy.Id }, policy);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("cars/{carId:long}/claims")]
    public async Task<ActionResult<ClaimResponse>> RegisterClaim(long carId, [FromBody] CreateClaimRequest request)
    {
        try
        {
            var claim = await _service.RegisterClaimAsync(carId, request);
            return CreatedAtAction(nameof(RegisterClaim), new { carId = claim.CarId, claimId = claim.Id }, claim);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("cars/{carId:long}/history")]
    public async Task<ActionResult<CarHistoryResponse>> GetCarHistory(long carId)
    {
        try
        {
            var history = await _service.GetCarHistoryAsync(carId);
            return Ok(history);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // Development/Testing endpoint to manually trigger expiration check
    [HttpPost("admin/check-expirations")]
    public async Task<ActionResult> CheckExpirations()
    {
        try
        {
            await _expirationService.CheckAndLogExpiredPoliciesAsync();
            return Ok(new { message = "Expiration check completed. Check logs for details." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
