namespace JobTracker.Models;

public class InvalidDateRangeException(DateTime from, DateTime to)
    : ArgumentException("From date can't be after To date.")
{
    public DateTime From { get; } = from;

    public DateTime To { get; } = to;
}
