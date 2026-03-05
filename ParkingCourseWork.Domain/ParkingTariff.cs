namespace ParkingCourseWork.Domain;

public class ParkingTariff
{
    public decimal HourlyRate { get; set; } = 30m;
    public decimal MinimumCharge { get; set; } = 15m;
    public decimal FineNoPayment { get; set; } = 400m;
    public decimal FineOverstay { get; set; } = 300m;

    public decimal CalculateFee(DateTime start, DateTime end)
    {
        if (end < start)
        {
            throw new ArgumentException("End time cannot be earlier than start time.");
        }

        var minutes = (decimal)(end - start).TotalMinutes;
        if (minutes <= 0)
        {
            return 0m;
        }

        var amount = minutes / 60m * HourlyRate;
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        if (amount > 0m && amount < MinimumCharge)
        {
            return MinimumCharge;
        }

        return amount;
    }
}
