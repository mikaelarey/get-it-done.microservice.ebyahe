using System.Text.Json;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Persistence;

public sealed partial class EByaheDbContext
{
  private readonly Dictionary<Guid, PartnerApplicationHistory> pendingPartnerHistories = [];

  private Guid[] ChangedPartnerApplications()
  {
    ChangeTracker.DetectChanges();
    foreach (var entry in ChangeTracker.Entries<PartnerApplication>())
    {
      if (entry.State == EntityState.Deleted)
        throw new InvalidOperationException("Partner applications retain their audit trail and cannot be physically deleted.");
      if (entry.State == EntityState.Added && (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy)
        || entry.Entity.CreatedBy.Length > 255 || entry.Entity.CreatedBy != entry.Entity.CreatedBy.Trim()))
        throw new InvalidOperationException("New partner applications require an authenticated creator.");
      if (entry.State == EntityState.Modified && entry.Property(row => row.CreatedBy).IsModified
        && entry.Property(row => row.CreatedBy).OriginalValue != entry.Entity.CreatedBy)
        throw new InvalidOperationException("The original application creator cannot be changed.");
    }
    foreach (var entry in ChangeTracker.Entries<PartnerApplicationDocument>())
    {
      if (entry.State is EntityState.Added or EntityState.Modified
        && (string.IsNullOrWhiteSpace(entry.Entity.StorageBucket) || string.IsNullOrWhiteSpace(entry.Entity.StorageKey)))
        throw new InvalidOperationException("A document requires a private object storage reference.");
      if (entry.State == EntityState.Modified && entry.Property(row => row.PartnerApplicationId).IsModified
        && entry.Property(row => row.PartnerApplicationId).OriginalValue != entry.Entity.PartnerApplicationId)
        throw new InvalidOperationException("Documents cannot be moved between partner applications.");
    }
    return ChangeTracker.Entries<PartnerApplication>()
      .Where(entry => entry.State is EntityState.Added or EntityState.Modified).Select(entry => entry.Entity.Id)
      .Concat(ChangeTracker.Entries<PartnerApplicationDocument>()
        .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
        .Select(entry => entry.Entity.PartnerApplicationId)).Distinct().Order().ToArray();
  }

  private void PreparePartnerHistory(Guid id, PartnerApplication? previous)
  {
    var previousJson = previous is null ? null : SerializePartner(previous);
    var entry = ChangeTracker.Entries<PartnerApplication>().SingleOrDefault(candidate => candidate.Entity.Id == id);
    var current = entry?.State == EntityState.Added
      ? (PartnerApplication)entry.CurrentValues.ToObject()
      : previous ?? throw new InvalidOperationException("The application for this document could not be found.");
    if (entry?.State == EntityState.Modified)
      foreach (var property in entry.Properties.Where(property => property.IsModified))
        property.Metadata.PropertyInfo!.SetValue(current, property.CurrentValue);

    var documents = (previous?.Documents ?? []).ToDictionary(document => document.Id);
    foreach (var document in ChangeTracker.Entries<PartnerApplicationDocument>()
      .Where(candidate => candidate.Entity.PartnerApplicationId == id))
    {
      if (document.State == EntityState.Deleted) documents.Remove(document.Entity.Id);
      else if (document.State == EntityState.Added)
        documents[document.Entity.Id] = (PartnerApplicationDocument)document.CurrentValues.ToObject();
      else if (document.State == EntityState.Modified && documents.TryGetValue(document.Entity.Id, out var stored))
        foreach (var property in document.Properties.Where(property => property.IsModified))
          property.Metadata.PropertyInfo!.SetValue(stored, property.CurrentValue);
    }
    current.Documents = documents.Values.OrderBy(document => document.DocumentType).ThenBy(document => document.Id).ToList();
    var currentJson = SerializePartner(current);
    if (currentJson == previousJson) return;
    if (!pendingPartnerHistories.TryGetValue(id, out var history) || Entry(history).State != EntityState.Added)
    {
      history = new PartnerApplicationHistory { Id = Guid.NewGuid(), PartnerApplicationId = id };
      PartnerApplicationHistories.Add(history);
      pendingPartnerHistories[id] = history;
    }
    history.PreviousValue = previousJson;
    history.CurrentValue = currentJson;
    history.Status = current.Status;
    history.DateTimeCreated = previousJson is null ? current.SubmittedAtUtc : DateTimeOffset.UtcNow;
  }

  // Immutable storage keys retain every document version without embedding file bytes in SQL history.
  private static string SerializePartner(PartnerApplication application)
  {
    application.Documents = application.Documents.OrderBy(document => document.DocumentType).ThenBy(document => document.Id).ToList();
    return JsonSerializer.Serialize(application);
  }
}
