using System.Security.Claims;
using LandaDoc.Review.Services;
using LandaDoc.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandaDoc.Review.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController(IReviewService reviews) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Submit([FromBody] CreateReviewRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var patientId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");

        var result = await reviews.SubmitAsync(patientId, token, req);
        return result.Status switch
        {
            SubmitReviewResultStatus.AppointmentNotFound => NotFound(),
            SubmitReviewResultStatus.NotYourAppointment => Forbid(),
            SubmitReviewResultStatus.NotCompleted => Conflict(new { error = "Appointment is not completed yet" }),
            SubmitReviewResultStatus.AlreadyReviewed => Conflict(new { error = "Appointment already reviewed" }),
            SubmitReviewResultStatus.Success => StatusCode(201, MapToDto(result.Review!)),
            _ => Problem()
        };
    }

    [HttpGet("doctor/{doctorId:guid}")]
    public async Task<IActionResult> GetForDoctor(Guid doctorId) =>
        Ok((await reviews.GetForDoctorAsync(doctorId)).Select(MapToDto));

    private static ReviewDto MapToDto(Models.Review r) =>
        new(r.Id, r.AppointmentId, r.DoctorId, r.PatientId, r.Rating, r.Comment, r.CreatedAt);
}
