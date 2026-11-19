using EByahe.Domain.Models;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed partial class PartnerPortalRepository
{
  private IQueryable<BusBooking> SearchBookings(string? search)
  {
    var terms = PartnerKeywordSearch.Tokens(search);
    if (terms.Length == 0) return database.BusBookings;
    // Search only displayed snapshot fields: never the whole JSON document,
    // which also contains private contact and passenger details.
    const string display = """
      CONCAT(b.[Reference], N' ',
        COALESCE(JSON_VALUE(b.[DetailsJson], '$.trip.origin'), N'Not recorded'), N' → ',
        COALESCE(JSON_VALUE(b.[DetailsJson], '$.trip.destination'), N'Not recorded'), N' ',
        COALESCE(JSON_VALUE(b.[DetailsJson], '$.trip.plateNumber'), N'Not recorded'), N' ',
        COALESCE(FORMAT(TRY_CONVERT(datetime2, CONCAT(JSON_VALUE(b.[DetailsJson], '$.trip.departureDate'), N'T', JSON_VALUE(b.[DetailsJson], '$.trip.departureTime'),
          CASE WHEN LEN(JSON_VALUE(b.[DetailsJson], '$.trip.departureTime')) = 5 THEN N':00' ELSE N'' END), 126), N'MMM d, yyyy, h:mm tt', 'en-US'), N'Not recorded'), N' ',
        JSON_VALUE(b.[DetailsJson], '$.trip.departureDate'), N' ', JSON_VALUE(b.[DetailsJson], '$.trip.departureTime'), N' ',
        CASE WHEN LEFT(LTRIM(JSON_QUERY(b.[DetailsJson], '$.booking.passengers')), 1) = N'['
          THEN (SELECT COUNT(*) FROM OPENJSON(b.[DetailsJson], '$.booking.passengers')) ELSE b.[ReservedSeats] END, N' ',
        b.[Total], N' ₱', FORMAT(b.[Total], N'N2', 'en-US'))
      """;
    var predicates = terms.Select((_, index) => $"({display}) COLLATE {PartnerKeywordSearch.Collation} LIKE {{{index}}} ESCAPE N'~'");
    // Only placeholder indexes and constant SQL are constructed; every keyword
    // is a bound parameter, with SQL wildcard characters escaped as literals.
    var sql = "SELECT b.* FROM [BusBookings] AS b WHERE " + string.Join(" AND ", predicates);
    return database.BusBookings.FromSqlRaw(sql, terms.Select(term => (object)PartnerKeywordSearch.Pattern(term)).ToArray());
  }
}
