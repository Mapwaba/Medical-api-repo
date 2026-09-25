using System.ComponentModel.DataAnnotations;

namespace LandaDoc.Shared.DTOs;

public record RefreshRequest([Required] string RefreshToken);
