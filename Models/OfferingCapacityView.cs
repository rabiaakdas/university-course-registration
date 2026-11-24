namespace UniversityRegistration.Models;

// Read-only view row; calculated occupancy is deliberately distinct from the stored counter.
public sealed class OfferingCapacityView
{
    public int OfferingId { get; set; }
    public int AcademicTermId { get; set; }
    public int Capacity { get; set; }
    public long CalculatedActiveCount { get; set; }
    public int StoredOccupiedSeats { get; set; }
}
