using LandaDoc.Shared.Models;

namespace LandaDoc.Availability.Models;

// A doctor's recurring weekly availability template for one day of the week.
public class Schedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public DayOfWeekEnum Day { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public int SlotMinutes { get; set; } = 30;
}
