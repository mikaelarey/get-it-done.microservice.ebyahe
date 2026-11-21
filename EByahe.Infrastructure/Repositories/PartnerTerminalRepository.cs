using System.Data;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using EByahe.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerTerminalRepository(EByaheDbContext db, IOptions<KafkaOptions>? kafka = null, IRecordChangePublisher? changes = null) : IPartnerTerminalRepository
{
  private IQueryable<Partner> BusOperators(string userId) => db.Partners.Where(partner => partner.Status == "Approved"
    && db.PartnerUsers.Any(member => member.PartnerId == partner.Id && member.IsActive && member.RefUser.ExternalUserId == userId)
    && db.PartnerApplications.Any(application => application.Id == partner.PartnerApplicationId
      && application.Status == "Approved" && application.BusinessCategory == "bus-operator"));

  public async Task<IReadOnlyList<PartnerTerminalWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken) =>
    await BusOperators(userId).AsNoTracking().OrderBy(partner => partner.DisplayName).ThenBy(partner => partner.Id)
      .Select(partner => new PartnerTerminalWorkspace(partner.Id, partner.DisplayName)).ToArrayAsync(cancellationToken);

  public async Task<IReadOnlyList<PartnerTerminalLocation>> LocationsAsync(string userId, CancellationToken cancellationToken)
  {
    if (!await BusOperators(userId).AnyAsync(cancellationToken)) return [];
    return await db.Locations.AsNoTracking().Where(location => location.IsActive).OrderBy(location => location.Name).ThenBy(location => location.Id)
      .Select(location => new PartnerTerminalLocation(location.Id, location.Name, location.CountryCode)).ToArrayAsync(cancellationToken);
  }

  public async Task<PartnerPortalTerminal?> AddAsync(string userId, AddPartnerTerminal command, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    await LockPartnerAsync(command.PartnerId, cancellationToken);
    var partner = await BusOperators(userId).SingleOrDefaultAsync(partner => partner.Id == command.PartnerId, cancellationToken);
    if (partner is null) return null;
    var (location, barangay) = await ResolveLocationAsync(command.CodeDecodeRegion, command.CodeDecodeProvince,
      command.CodeDecodeMunicipality, command.CodeDecodeBarangay, cancellationToken);
    var terminal = new BusTerminal { PartnerId = partner.Id, LocationId = location.Id, Location = location, Name = command.Name, Address = command.Address };
    db.BusTerminals.Add(terminal);
    CatalogAudit.Record(db, terminal, userId, "Created");
    await PartnerCatalogActivityOutbox.QueueAsync(db, partner.Id, userId, "terminal.created", terminal.Id,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await GenerateRoutesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerTerminalConflictException(); }
    if (changes is not null) await changes.PublishAsync(["terminals", "travel-times", "scheduled-trips"], userId, partner.Id, cancellationToken);
    return new(terminal.Id, terminal.Name, location.Name,
      TerminalAddress.Format(terminal.Address, barangay.Decode, barangay.Municipality.Decode,
        barangay.Municipality.Province.Decode, barangay.Municipality.Province.Region.Decode), partner.DisplayName, terminal.IsActive);
  }

  public Task<PartnerTerminalForUpdate?> GetForUpdateAsync(string userId, Guid id, CancellationToken cancellationToken)
  {
    var ids = BusOperators(userId).Select(partner => partner.Id);
    return db.BusTerminals.AsNoTracking().Where(row => row.Id == id && ids.Contains(row.PartnerId))
      .Select(row => new PartnerTerminalForUpdate(row.Id, row.PartnerId, row.Name, row.Address, row.Location.Name, row.IsActive,
        row.Location.CodeDecodeRegion, row.Location.CodeDecodeProvince, row.Location.CodeDecodeMunicipality, row.Location.CodeDecodeBarangay))
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<PartnerPortalTerminal?> UpdateAsync(string userId, Guid id, UpdatePartnerTerminal command, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    await db.Partners.FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id IN (SELECT PartnerId FROM BusTerminals WHERE Id = {id})")
      .SingleOrDefaultAsync(cancellationToken);
    var ids = BusOperators(userId).Select(partner => partner.Id);
    var terminal = await db.BusTerminals.Include(row => row.Location)
      .SingleOrDefaultAsync(row => row.Id == id && ids.Contains(row.PartnerId), cancellationToken);
    if (terminal is null) return null;
    var previous = CatalogAudit.Snapshot(terminal);
    var codes = new[] { command.CodeDecodeRegion, command.CodeDecodeProvince, command.CodeDecodeMunicipality, command.CodeDecodeBarangay };
    if (codes.All(code => code.HasValue))
    {
      var (location, _) = await ResolveLocationAsync(command.CodeDecodeRegion!.Value, command.CodeDecodeProvince!.Value,
        command.CodeDecodeMunicipality!.Value, command.CodeDecodeBarangay!.Value, cancellationToken);
      // Reassign this terminal only; shared locations and other terminals retain their geography.
      terminal.Location = location; terminal.LocationId = location.Id;
    }
    else if (codes.Any(code => code.HasValue) || terminal.Location.CodeDecodeBarangay is not null)
      throw new PartnerTerminalLocationUnavailableException();
    terminal.Name = command.Name; terminal.Address = command.Address; terminal.IsActive = command.IsActive;
    CatalogAudit.Record(db, terminal, userId, "Updated", previous);
    await PartnerCatalogActivityOutbox.QueueAsync(db, terminal.PartnerId, userId, "terminal.updated", terminal.Id,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await GenerateRoutesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerTerminalConflictException(); }
    if (changes is not null) await changes.PublishAsync(["terminals", "travel-times", "scheduled-trips"], userId, terminal.PartnerId, cancellationToken);
    return await db.BusTerminals.AsNoTracking().Where(row => row.Id == id)
      .Select(row => new PartnerPortalTerminal(row.Id, row.Name, row.Location.Name,
        TerminalAddress.Format(row.Address, row.Location.Barangay == null ? null : row.Location.Barangay.Decode,
          row.Location.Municipality == null ? null : row.Location.Municipality.Decode,
          row.Location.Province == null ? null : row.Location.Province.Decode,
          row.Location.Region == null ? null : row.Location.Region.Decode), row.Partner.DisplayName, row.IsActive))
      .SingleAsync(cancellationToken);
  }

  private Task LockPartnerAsync(Guid partnerId, CancellationToken ct) => db.Partners
    .FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id = {partnerId}").SingleOrDefaultAsync(ct);

  private async Task GenerateRoutesAsync(CancellationToken ct)
  {
    // Called inside the terminal transaction. Never connect terminals from different partners.
    var partners = db.ChangeTracker.Entries<BusTerminal>().Select(entry => entry.Entity.PartnerId).Distinct().ToArray();
    foreach (var partnerId in partners)
      await db.Database.ExecuteSqlInterpolatedAsync($@"
        INSERT INTO BusRoutes (Id, PartnerId, OriginTerminalId, DestinationTerminalId, DurationMinutes, IsActive, IsDemo)
        SELECT NEWID(), origin.PartnerId, origin.Id, destination.Id, NULL, 1, 0
        FROM BusTerminals origin JOIN BusTerminals destination ON destination.PartnerId = origin.PartnerId AND destination.Id <> origin.Id
        WHERE origin.PartnerId = {partnerId} AND NOT EXISTS (
          SELECT 1 FROM BusRoutes route WHERE route.PartnerId = origin.PartnerId
            AND route.OriginTerminalId = origin.Id AND route.DestinationTerminalId = destination.Id)", ct);
  }

  private async Task<(Location Location, CodeDecodeBarangay Barangay)> ResolveLocationAsync(int region, int province, int municipality, int barangayCode,
    CancellationToken cancellationToken)
  {
    var barangay = await db.CodeDecodeBarangays.AsNoTracking().Include(row => row.Municipality).ThenInclude(row => row.Province).ThenInclude(row => row.Region)
      .SingleOrDefaultAsync(row => row.Code == barangayCode, cancellationToken);
    if (barangay is null || barangay.CodeDecodeMunicipality != municipality
      || barangay.Municipality.CodeDecodeProvince != province || barangay.Municipality.Province.CodeDecodeRegion != region)
      throw new PartnerTerminalLocationUnavailableException();
    var location = await db.Locations.FromSqlInterpolated($"SELECT * FROM [Locations] WITH (UPDLOCK, HOLDLOCK) WHERE [CodeDecodeBarangay] = {barangayCode}")
      .SingleOrDefaultAsync(cancellationToken);
    if (location is not null && !location.IsActive) throw new PartnerTerminalLocationUnavailableException();
    var name = $"{barangay.Municipality.Decode} ({barangay.Municipality.Province.Decode})";
    if (location is null)
    {
      location = new Location { Code = $"PH-BRG-{barangay.Code}", Name = name, CodeDecodeRegion = region,
        CodeDecodeProvince = province, CodeDecodeMunicipality = municipality, CodeDecodeBarangay = barangayCode };
      db.Locations.Add(location);
    }
    else location.Name = name;
    return (location, barangay);
  }

  public async Task<IReadOnlyList<GeographyOption>> RegionsAsync(string userId, CancellationToken cancellationToken)
  {
    if (!await BusOperators(userId).AnyAsync(cancellationToken)) return [];
    return await db.CodeDecodeRegions.AsNoTracking().OrderBy(row => row.Code).Select(row => new GeographyOption(row.Code, row.Decode)).ToArrayAsync(cancellationToken);
  }

  public async Task<PartnerTerminalImportResult?> ImportAsync(string userId, Guid partnerId, IReadOnlyList<PartnerTerminalImportRow> rows, Guid uploadId, CancellationToken cancellationToken)
  {
    if (rows.Count is < 1 or > 500 || rows.Any(row => row.Terminal.PartnerId != partnerId)) throw new ArgumentException("Invalid terminal import batch.");
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    await LockPartnerAsync(partnerId, cancellationToken);
    var partner = await BusOperators(userId).SingleOrDefaultAsync(partner => partner.Id == partnerId, cancellationToken);
    if (partner is null) return null;
    if (!await db.Uploads.AnyAsync(upload => upload.Id == uploadId && upload.CreatedBy == userId && upload.UploadType == UploadTypeCodes.Terminals, cancellationToken))
      throw new ArgumentException("A matching upload log is required.", nameof(uploadId));
    var codes = rows.Select(row => row.Terminal.CodeDecodeBarangay).Distinct().Order().ToArray();
    var geography = await db.CodeDecodeBarangays.AsNoTracking().Include(row => row.Municipality).ThenInclude(row => row.Province).ThenInclude(row => row.Region)
      .Where(row => codes.Contains(row.Code)).ToDictionaryAsync(row => row.Code, cancellationToken);
    var errors = new List<PartnerTerminalImportError>();
    var seen = new HashSet<(int, string)>();
    foreach (var row in rows)
    {
      var command = row.Terminal;
      if (!geography.TryGetValue(command.CodeDecodeBarangay, out var barangay)) errors.Add(new(row.Row, "Barangay", "Select a registered barangay from the reference data."));
      else
      {
        if (barangay.CodeDecodeMunicipality != command.CodeDecodeMunicipality) errors.Add(new(row.Row, "City / Municipality", "The barangay does not belong to this city/municipality."));
        if (barangay.Municipality.CodeDecodeProvince != command.CodeDecodeProvince) errors.Add(new(row.Row, "Province", "The city/municipality does not belong to this province."));
        if (barangay.Municipality.Province.CodeDecodeRegion != command.CodeDecodeRegion) errors.Add(new(row.Row, "Region", "The province does not belong to this region."));
      }
      if (!seen.Add((command.CodeDecodeBarangay, command.Name.ToUpperInvariant()))) errors.Add(new(row.Row, "Terminal name", "This terminal and barangay are repeated in the file."));
    }
    if (errors.Count != 0) return new(0, errors);
    var locations = new Dictionary<int, Location>();
    // A consistent lock order lets concurrent imports share locations safely.
    foreach (var code in codes)
    {
      var location = await db.Locations.FromSqlInterpolated($"SELECT * FROM [Locations] WITH (UPDLOCK, HOLDLOCK) WHERE [CodeDecodeBarangay] = {code}")
        .SingleOrDefaultAsync(cancellationToken);
      if (location is not null)
      {
        locations.Add(code, location);
        if (!location.IsActive)
          errors.AddRange(rows.Where(row => row.Terminal.CodeDecodeBarangay == code).Select(row => new PartnerTerminalImportError(row.Row, "Location", "This location is inactive. Contact an administrator.")));
      }
    }
    var locationIds = locations.Values.Select(location => location.Id).ToArray();
    var existing = await db.BusTerminals.AsNoTracking().Where(terminal => terminal.PartnerId == partnerId && locationIds.Contains(terminal.LocationId))
      .Select(terminal => new { terminal.Name, terminal.Location.CodeDecodeBarangay }).ToArrayAsync(cancellationToken);
    foreach (var row in rows)
      if (existing.Any(terminal => terminal.CodeDecodeBarangay == row.Terminal.CodeDecodeBarangay && string.Equals(terminal.Name, row.Terminal.Name, StringComparison.OrdinalIgnoreCase)))
        errors.Add(new(row.Row, "Terminal name", "A terminal with this name already exists at this location for your partner."));
    if (errors.Count != 0) return new(0, errors);
    foreach (var row in rows)
    {
      var command = row.Terminal; var barangay = geography[command.CodeDecodeBarangay];
      var name = $"{barangay.Municipality.Decode} ({barangay.Municipality.Province.Decode})";
      if (!locations.TryGetValue(barangay.Code, out var location))
      {
        location = new Location { Code = $"PH-BRG-{barangay.Code}", Name = name,
          CodeDecodeRegion = command.CodeDecodeRegion, CodeDecodeProvince = command.CodeDecodeProvince,
          CodeDecodeMunicipality = command.CodeDecodeMunicipality, CodeDecodeBarangay = barangay.Code };
        db.Locations.Add(location); locations.Add(barangay.Code, location);
      }
      else location.Name = name;
      var terminal = new BusTerminal { PartnerId = partner.Id, LocationId = location.Id, Location = location, Name = command.Name, Address = command.Address };
      db.BusTerminals.Add(terminal);
      db.UploadRecords.Add(new UploadRecord { UploadId = uploadId, TerminalId = terminal.Id });
      CatalogAudit.Record(db, terminal, userId, "Imported", uploadId: uploadId);
    }
    await PartnerCatalogActivityOutbox.QueueImportAsync(db, partner.Id, userId, "terminals.imported", rows.Count, uploadId,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await GenerateRoutesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerTerminalConflictException(); }
    if (changes is not null) await changes.PublishAsync(["terminals", "travel-times", "scheduled-trips"], userId, partner.Id, cancellationToken);
    return new(rows.Count, []);
  }

  public async Task<IReadOnlyList<GeographyOption>> ProvincesAsync(string userId, int region, CancellationToken cancellationToken)
  {
    if (!await BusOperators(userId).AnyAsync(cancellationToken)) return [];
    return await db.CodeDecodeProvinces.AsNoTracking().Where(row => row.CodeDecodeRegion == region).OrderBy(row => row.Decode)
      .Select(row => new GeographyOption(row.Code, row.Decode)).ToArrayAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<GeographyOption>> MunicipalitiesAsync(string userId, int province, CancellationToken cancellationToken)
  {
    if (!await BusOperators(userId).AnyAsync(cancellationToken)) return [];
    return await db.CodeDecodeMunicipalities.AsNoTracking().Where(row => row.CodeDecodeProvince == province).OrderBy(row => row.Decode)
      .Select(row => new GeographyOption(row.Code, row.Decode)).ToArrayAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<GeographyOption>> BarangaysAsync(string userId, int municipality, CancellationToken cancellationToken)
  {
    if (!await BusOperators(userId).AnyAsync(cancellationToken)) return [];
    return await db.CodeDecodeBarangays.AsNoTracking().Where(row => row.CodeDecodeMunicipality == municipality).OrderBy(row => row.Decode)
      .Select(row => new GeographyOption(row.Code, row.Decode)).ToArrayAsync(cancellationToken);
  }
}
