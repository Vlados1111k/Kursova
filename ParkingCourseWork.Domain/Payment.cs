namespace ParkingCourseWork.Domain;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
}
