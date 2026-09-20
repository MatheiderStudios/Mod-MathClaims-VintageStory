using MathClaims.Models;
using MathClaims.Server.Claims;

namespace MathClaims.Client.Map;

public sealed class MapSelection
{
    public HashSet<ChunkColumn> Columns { get; } = [];
    public bool Dragging { get; private set; }
    public bool Manual { get; private set; }
    private ChunkColumn anchor;
    private ChunkColumn trailLast;
    public void Begin(ChunkColumn column, bool shift, Func<ChunkColumn, bool> allowed)
    {
        if (shift)
        {
            if (!Manual) Columns.Clear();
            Manual = true;
            trailLast = column;
            if (Columns.Contains(column))
            {
                var proposed = Columns.Where(c => c != column).ToArray();
                if (proposed.Length == 0 || ClaimEngine.IsConnected(proposed)) Columns.Remove(column);
            }
            else if (allowed(column) && Columns.Count < 64 &&
                     (Columns.Count == 0 || column.Neighbors().Any(Columns.Contains))) Columns.Add(column);
            return;
        }
        Manual = false;
        Dragging = true;
        anchor = column;
        Rectangle(column, allowed);
    }
    public void Trail(ChunkColumn end, Func<ChunkColumn, bool> allowed)
    {
        if (!Manual || Columns.Count >= 64) return;
        var current = trailLast;
        while (current.X != end.X && Columns.Count < 64)
        {
            current = current with { X = current.X + Math.Sign(end.X - current.X) };
            AddTrailCell(current, allowed);
        }
        while (current.Z != end.Z && Columns.Count < 64)
        {
            current = current with { Z = current.Z + Math.Sign(end.Z - current.Z) };
            AddTrailCell(current, allowed);
        }
        trailLast = end;
    }
    private void AddTrailCell(ChunkColumn column, Func<ChunkColumn, bool> allowed)
    {
        if (allowed(column) && (Columns.Count == 0 || column.Neighbors().Any(Columns.Contains))) Columns.Add(column);
    }
    public void Rectangle(ChunkColumn end, Func<ChunkColumn, bool> allowed)
    {
        if (!Dragging) return;
        Columns.Clear();
        var pending = new Queue<ChunkColumn>();
        var visited = new HashSet<ChunkColumn>();
        pending.Enqueue(anchor);
        while (pending.Count > 0 && Columns.Count < 64)
        {
            var c = pending.Dequeue();
            if (!visited.Add(c) || c.X < Math.Min(anchor.X, end.X) || c.X > Math.Max(anchor.X, end.X) ||
                c.Z < Math.Min(anchor.Z, end.Z) || c.Z > Math.Max(anchor.Z, end.Z) || !allowed(c)) continue;
            Columns.Add(c);
            foreach (var next in c.Neighbors()) pending.Enqueue(next);
        }
    }
    public bool Release() { var completed = Dragging; Dragging = false; return completed && Columns.Count > 0; }
    public bool ReleaseShift() { var completed = Manual; Manual = false; return completed && Columns.Count > 0; }
    public void Clear() { Columns.Clear(); Dragging = Manual = false; }
}
