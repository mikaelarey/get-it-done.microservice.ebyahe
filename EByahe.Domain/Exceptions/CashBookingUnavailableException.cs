namespace EByahe.Domain.Exceptions;

public sealed class CashBookingUnavailableException : Exception
{
  public CashBookingUnavailableException() : base("Cash booking is only available when departure is at least 24 hours away. Choose an online payment method.") { }
}
