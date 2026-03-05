namespace ParkingCourseWork.Domain;

public class Fine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime IssuedAt { get; set; } = DateTime.Now;
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public FineStatus Status { get; set; } = FineStatus.Unpaid;
    public DateTime? PaidAt { get; set; }

    public void MarkPaid()
    {
        Status = FineStatus.Paid;
        PaidAt = DateTime.Now;
    }
}
