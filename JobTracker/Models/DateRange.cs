namespace JobTracker.Models;

public readonly struct DateRange : IEquatable<DateRange>
{
    public DateRange(DateTime? from, DateTime? to)
    {
        if (from is not null && to is not null && from.Value.Date > to.Value.Date)
        {
            throw new InvalidDateRangeException(from.Value, to.Value);
        }

        From = from;
        To = to;
    }

    public DateTime? From { get; }

    public DateTime? To { get; }

    // start of the From day, in UTC because postgres timestamptz only compares with UTC values
    public DateTime? StartUtc => From is null ? null : DateTime.SpecifyKind(From.Value.Date, DateTimeKind.Utc);

    // start of the day after To, so the whole To day is included
    public DateTime? EndUtc => To is null ? null : DateTime.SpecifyKind(To.Value.Date.AddDays(1), DateTimeKind.Utc);

    public bool Equals(DateRange other) => From == other.From && To == other.To;

    public override bool Equals(object? obj) => obj is DateRange other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(From, To);

    public static bool operator ==(DateRange left, DateRange right) => left.Equals(right);

    public static bool operator !=(DateRange left, DateRange right) => !left.Equals(right);

    public bool Contains(DateTime date)
    {
        return (StartUtc is null || date >= StartUtc) &&
               (EndUtc is null || date < EndUtc);
    }
}
