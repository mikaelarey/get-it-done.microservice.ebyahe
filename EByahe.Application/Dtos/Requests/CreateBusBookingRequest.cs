using EByahe.Application.Validation;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EByahe.Application.Dtos.Requests;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateBusBookingRequest : IValidatableObject
{
  public Guid RequestId { get; set; }
  [Required] public BusTripRequest Trip { get; set; } = null!;
  [Required] public BusBookingDetailsRequest Booking { get; set; } = null!;
  [Required] public BusPaymentRequest Payment { get; set; } = null!;
  [Required] public BusFareRequest Fare { get; set; } = null!;

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    if (RequestId == Guid.Empty) yield return new("A non-empty request ID is required.", [nameof(RequestId)]);
    if (Trip is not null)
    {
      if (string.Equals(Trip.Origin?.Trim(), Trip.Destination?.Trim(), StringComparison.OrdinalIgnoreCase))
        yield return new("Origin and destination must differ.", ["Trip.Destination"]);
      if (!DateOnly.TryParseExact(Trip.DepartureDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        yield return new("Use a valid YYYY-MM-DD departure date.", ["Trip.DepartureDate"]);
      if (!TimeOnly.TryParseExact(Trip.DepartureTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        yield return new("Use a valid HH:mm departure time.", ["Trip.DepartureTime"]);
    }
    if (Booking?.Passengers?.Any(passenger => passenger is null) == true)
      yield return new("Each passenger must have details.", ["Booking.Passengers"]);
    if (Booking?.Contact is { } contact)
    {
      foreach (var (number, key, optional) in new[] { (contact.Phone, "Phone", false), (contact.EmergencyPhone, "EmergencyPhone", true) })
        if (!(optional && string.IsNullOrEmpty(number)) && !Regex.IsMatch(Regex.Replace(number ?? "", @"[\s()-]", ""), @"^\+?\d{10,15}$"))
          yield return new("Enter a phone number containing 10 to 15 digits.", [$"Booking.Contact.{key}"]);
    }
    if (Fare?.Fares is { } fares && Booking?.Passengers is { } passengers)
    {
      if (fares.Any(line => line is null) || fares.Count != passengers.Count)
        yield return new("Provide one fare line per passenger.", ["Fare.Fares"]);
      else if (fares.Select(line => line.Amount).Concat([Fare.Protection, Fare.Baggage, Fare.Fee, Fare.Total])
        .Any(value => Math.Round(value, 2) != value))
        yield return new("Fare amounts must have at most two decimal places.", [nameof(Fare)]);
      else if (Fare.Protection != (Booking.Protection ? 1200m * passengers.Count : 0m)
        || Fare.Baggage != (Booking.Baggage ? 200m : 0m)
        || Fare.Fee != Math.Round((fares.Sum(line => line.Amount) + Fare.Protection + Fare.Baggage) * .03m, 2, MidpointRounding.AwayFromZero)
        || Fare.Total != fares.Sum(line => line.Amount) + Fare.Protection + Fare.Baggage + Fare.Fee)
        yield return new("Fare totals do not match the submitted passengers and add-ons.", [nameof(Fare)]);
    }
    if (Payment is { } payment)
    {
      var validProvider = payment.Method switch
      {
        "card" => payment.Provider == "Card",
        "cash" => payment.Provider == "Bus station",
        "wallet" => payment.Provider is "GCash" or "Maya",
        "bank" => payment.Provider is "BDO" or "BPI" or "Metrobank",
        _ => false
      };
      if (!validProvider) yield return new("Select a supported payment provider.", ["Payment.Provider"]);
      if (payment.Method == "card" && (string.IsNullOrWhiteSpace(payment.Cardholder) || string.IsNullOrWhiteSpace(payment.Last4) || string.IsNullOrWhiteSpace(payment.Expiry)))
        yield return new("Provide the cardholder, last four digits and expiry metadata.", [nameof(Payment)]);
      if (payment.Method != "card" && (payment.Cardholder is not null || payment.Last4 is not null || payment.Expiry is not null))
        yield return new("Card metadata is only allowed for the card method.", [nameof(Payment)]);
    }
  }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BusTripRequest
{
  [NonEmptyGuid] public Guid TripId { get; set; }
  [Required, MaxLength(100)] public string Origin { get; set; } = "";
  [Required, MaxLength(100)] public string Destination { get; set; } = "";
  [Required, MaxLength(100)] public string Operator { get; set; } = "";
  [Required, MaxLength(100)] public string BusClass { get; set; } = "";
  [MaxLength(20), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PlateNumber { get; set; }
  [Required, MaxLength(10)] public string DepartureDate { get; set; } = "";
  [Required, MaxLength(5)] public string DepartureTime { get; set; } = "";
  [Required, MaxLength(100)] public string TravelTime { get; set; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BusBookingDetailsRequest
{
  [Required, MaxLength(254)] public string Boarding { get; set; } = "";
  [Required, MaxLength(254)] public string Dropping { get; set; } = "";
  [Required, MinLength(1), MaxLength(20)] public List<BusPassengerRequest> Passengers { get; set; } = null!;
  [Required] public BusContactRequest Contact { get; set; } = null!;
  public bool Protection { get; set; }
  public bool Baggage { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BusPassengerRequest
{
  [Required, MaxLength(100)] public string FullName { get; set; } = "";
  [Range(0, 120)] public int? Age { get; set; }
  [MaxLength(32), RegularExpression(@"^(Female|Male|Prefer not to say)?$")] public string Gender { get; set; } = "";
  [Required, RegularExpression(@"^(adult|student|senior)$")] public string Type { get; set; } = "";
  [MaxLength(100), RegularExpression(@"^(Aisle|Extra legroom|Wheelchair assistance|Window)?$")] public string Request { get; set; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BusContactRequest
{
  [Required, MaxLength(100)] public string FullName { get; set; } = "";
  [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
  [Required, MaxLength(32)] public string Phone { get; set; } = "";
  [MaxLength(32)] public string EmergencyPhone { get; set; } = "";
  public bool Updates { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BusPaymentRequest
{
  [Required, RegularExpression(@"^(card|wallet|bank|cash)$")] public string Method { get; set; } = "";
  [Required, MaxLength(32)] public string Provider { get; set; } = "";
  [MaxLength(100)] public string? Cardholder { get; set; }
  [RegularExpression(@"^\d{4}$")] public string? Last4 { get; set; }
  [RegularExpression(@"^(0[1-9]|1[0-2])/\d{2}$")] public string? Expiry { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BusFareRequest
{
  [Required, MinLength(1), MaxLength(20)] public List<BusFareLineRequest> Fares { get; set; } = null!;
  [Range(typeof(decimal), "0", "1000000000")] public decimal Protection { get; set; }
  [Range(typeof(decimal), "0", "1000000000")] public decimal Baggage { get; set; }
  [Range(typeof(decimal), "0", "1000000000")] public decimal Fee { get; set; }
  [Range(typeof(decimal), "0", "1000000000")] public decimal Total { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BusFareLineRequest
{
  [Required, MaxLength(100)] public string Label { get; set; } = "";
  [Range(typeof(decimal), "0", "1000000")] public decimal Amount { get; set; }
}
