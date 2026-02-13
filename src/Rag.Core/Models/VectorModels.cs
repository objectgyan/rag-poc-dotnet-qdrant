namespace Rag.Core.Models;

public record VectorRecord(string Id, float[] Vector, Dictionary<string, object> Payload);
public record VectorHit(string Id, double Score, Dictionary<string, object> Payload);