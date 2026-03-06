namespace ParkingCourseWork.Domain;

public class ParkingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string VehiclePlate { get; set; } = string.Empty;
    public int ParkingPlaceId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public decimal AccruedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public List<Payment> Payments { get; set; } = new();
    public List<Fine> Fines { get; set; } = new();

    public decimal GetUnpaidFinesAmount()
    {
        return Fines.Where(f => f.Status == FineStatus.Unpaid).Sum(f => f.Amount);
    }

    public bool IsFullyPaid()
    {
        return PaidAmount >= AccruedAmount + GetUnpaidFinesAmount();
    }

    public decimal GetOutstandingAmount()
    {
        var due = AccruedAmount + GetUnpaidFinesAmount() - PaidAmount;
        return due < 0 ? 0 : decimal.Round(due, 2, MidpointRounding.AwayFromZero);
    }

    public void AddPayment(Payment payment)
    {
        Payments.Add(payment);
        PaidAmount = decimal.Round(PaidAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);
    }

    public void AddFine(Fine fine)
    {
        Fines.Add(fine);
    }
}
