namespace ParkingCourseWork.Domain;

public class ParkingManager
{
    private readonly List<ParkingPlace> _places = new();
    private readonly List<ParkingSession> _sessions = new();

    public ParkingTariff Tariff { get; private set; } = new();

    public IReadOnlyList<ParkingPlace> Places => _places;
    public IReadOnlyList<ParkingSession> Sessions => _sessions;

    public event Action? StateChanged;

    public void InitializeDefaultPlaces()
    {
        _places.Clear();
        _sessions.Clear();

        for (var i = 1; i <= 20; i++)
        {
            var type = ParkingPlaceType.Standard;
            if (i <= 2)
            {
                type = ParkingPlaceType.Disabled;
            }
            else if (i is 3 or 4)
            {
                type = ParkingPlaceType.Electric;
            }

            _places.Add(new ParkingPlace
            {
                Id = i,
                Code = $"P{i:00}",
                Type = type,
                Status = ParkingPlaceStatus.Free
            });
        }

        NotifyChanged();
    }

    public ParkingSession StartSession(int placeId, string vehiclePlate, DateTime? startTime = null)
    {
        if (string.IsNullOrWhiteSpace(vehiclePlate))
        {
            throw new ArgumentException("Vehicle plate is required.");
        }

        var place = _places.SingleOrDefault(p => p.Id == placeId);
        if (place is null)
        {
            throw new InvalidOperationException("Parking place was not found.");
        }

        if (!place.CanBeOccupied())
        {
            throw new InvalidOperationException("Parking place is not available.");
        }

        var normalized = vehiclePlate.Trim().ToUpperInvariant();
        if (_sessions.Any(s => s.Status == SessionStatus.Active && s.VehiclePlate.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("This vehicle already has an active session.");
        }

        var session = new ParkingSession
        {
            VehiclePlate = normalized,
            ParkingPlaceId = placeId,
            StartTime = startTime ?? DateTime.Now,
            Status = SessionStatus.Active
        };

        _sessions.Add(session);
        place.Status = ParkingPlaceStatus.Occupied;

        NotifyChanged();
        return session;
    }

    public ParkingSession EndSession(Guid sessionId, DateTime? endTime = null)
    {
        var session = _sessions.SingleOrDefault(s => s.Id == sessionId);
        if (session is null)
        {
            throw new InvalidOperationException("Session was not found.");
        }

        if (session.Status != SessionStatus.Active)
        {
            throw new InvalidOperationException("Session is already completed.");
        }

        var end = endTime ?? DateTime.Now;
        session.EndTime = end;
        session.Status = SessionStatus.Completed;
        session.AccruedAmount = Tariff.CalculateFee(session.StartTime, end);

        var place = _places.Single(p => p.Id == session.ParkingPlaceId);
        if (place.Status == ParkingPlaceStatus.Occupied)
        {
            place.Status = ParkingPlaceStatus.Free;
        }

        NotifyChanged();
        return session;
    }

    public Payment PaySession(Guid sessionId, decimal amount, PaymentMethod method)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var session = _sessions.SingleOrDefault(s => s.Id == sessionId);
        if (session is null)
        {
            throw new InvalidOperationException("Session was not found.");
        }

        var payment = new Payment
        {
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Method = method,
            PaidAt = DateTime.Now
        };

        session.AddPayment(payment);

        if (session.GetOutstandingAmount() == 0)
        {
            foreach (var fine in session.Fines.Where(f => f.Status == FineStatus.Unpaid))
            {
                fine.MarkPaid();
            }
        }

        NotifyChanged();
        return payment;
    }

    public Fine IssueFine(Guid sessionId, string reason, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Fine amount must be greater than zero.");
        }

        var session = _sessions.SingleOrDefault(s => s.Id == sessionId);
        if (session is null)
        {
            throw new InvalidOperationException("Session was not found.");
        }

        var fine = new Fine
        {
            Reason = string.IsNullOrWhiteSpace(reason) ? "Порушення правил паркування" : reason.Trim(),
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Status = FineStatus.Unpaid,
            IssuedAt = DateTime.Now
        };

        session.AddFine(fine);
        NotifyChanged();
        return fine;
    }

    public Fine IssueNoPaymentFine(Guid sessionId)
    {
        return IssueFine(sessionId, "Несплата за паркування", Tariff.FineNoPayment);
    }

    public Fine IssueOverstayFine(Guid sessionId)
    {
        return IssueFine(sessionId, "Перевищення дозволеного часу", Tariff.FineOverstay);
    }

    public decimal GetOccupancyRate()
    {
        if (_places.Count == 0)
        {
            return 0m;
        }

        var occupied = _places.Count(p => p.Status == ParkingPlaceStatus.Occupied);
        var ratio = (decimal)occupied / _places.Count * 100m;
        return decimal.Round(ratio, 2, MidpointRounding.AwayFromZero);
    }

    public ParkingStateSnapshot CreateSnapshot()
    {
        return new ParkingStateSnapshot
        {
            Places = _places.Select(ClonePlace).ToList(),
            Sessions = _sessions.Select(CloneSession).ToList(),
            Tariff = new ParkingTariff
            {
                HourlyRate = Tariff.HourlyRate,
                MinimumCharge = Tariff.MinimumCharge,
                FineNoPayment = Tariff.FineNoPayment,
                FineOverstay = Tariff.FineOverstay
            }
        };
    }

    public void LoadSnapshot(ParkingStateSnapshot snapshot)
    {
        _places.Clear();
        _sessions.Clear();

        _places.AddRange(snapshot.Places.Select(ClonePlace));
        _sessions.AddRange(snapshot.Sessions.Select(CloneSession));
        Tariff = new ParkingTariff
        {
            HourlyRate = snapshot.Tariff.HourlyRate,
            MinimumCharge = snapshot.Tariff.MinimumCharge,
            FineNoPayment = snapshot.Tariff.FineNoPayment,
            FineOverstay = snapshot.Tariff.FineOverstay
        };

        NotifyChanged();
    }

    private static ParkingPlace ClonePlace(ParkingPlace p)
    {
        return new ParkingPlace
        {
            Id = p.Id,
            Code = p.Code,
            Type = p.Type,
            Status = p.Status
        };
    }

    private static ParkingSession CloneSession(ParkingSession s)
    {
        return new ParkingSession
        {
            Id = s.Id,
            VehiclePlate = s.VehiclePlate,
            ParkingPlaceId = s.ParkingPlaceId,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Status = s.Status,
            AccruedAmount = s.AccruedAmount,
            PaidAmount = s.PaidAmount,
            Payments = s.Payments.Select(p => new Payment
            {
                Id = p.Id,
                PaidAt = p.PaidAt,
                Amount = p.Amount,
                Method = p.Method
            }).ToList(),
            Fines = s.Fines.Select(f => new Fine
            {
                Id = f.Id,
                IssuedAt = f.IssuedAt,
                Reason = f.Reason,
                Amount = f.Amount,
                Status = f.Status,
                PaidAt = f.PaidAt
            }).ToList()
        };
    }

    private void NotifyChanged()
    {
        StateChanged?.Invoke();
    }
}
