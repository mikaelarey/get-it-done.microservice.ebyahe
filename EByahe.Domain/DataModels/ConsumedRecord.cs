namespace EByahe.Domain.DataModels;

public sealed record ConsumedRecord(string Topic, int Partition, long Offset, string? Key, string? Payload);
