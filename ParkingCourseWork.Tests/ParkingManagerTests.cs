using ParkingCourseWork.Domain;
using Xunit;

namespace ParkingCourseWork.Tests;

public class ParkingManagerTests
{
    [Fact]
    public void StartSession_MarksPlaceAsOccupied()
    {
        var manager = new ParkingManager();
        manager.InitializeDefaultPlaces();

        var session = manager.StartSession(1, "AA1234BB");

        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(ParkingPlaceStatus.Occupied, manager.Places.Single(p => p.Id == 1).Status);
    }

    [Fact]
    public void EndSession_CalculatesAccruedAmountAndReleasesPlace()
    {
        var manager = new ParkingManager();
        manager.InitializeDefaultPlaces();

        var session = manager.StartSession(2, "BC5678CA", new DateTime(2026, 1, 1, 10, 0, 0));
        manager.EndSession(session.Id, new DateTime(2026, 1, 1, 11, 30, 0));

        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.True(session.AccruedAmount > 0);
        Assert.Equal(ParkingPlaceStatus.Free, manager.Places.Single(p => p.Id == 2).Status);
    }

    [Fact]
    public void FineAndPayment_UpdateDebt()
    {
        var manager = new ParkingManager();
        manager.InitializeDefaultPlaces();

        var session = manager.StartSession(3, "CE9999EE", new DateTime(2026, 1, 1, 10, 0, 0));
        manager.EndSession(session.Id, new DateTime(2026, 1, 1, 11, 0, 0));
        manager.IssueNoPaymentFine(session.Id);

        var debtBefore = session.GetOutstandingAmount();
        manager.PaySession(session.Id, 1000m, PaymentMethod.Card);

        Assert.True(debtBefore > 0);
        Assert.Equal(0m, session.GetOutstandingAmount());
    }
}
