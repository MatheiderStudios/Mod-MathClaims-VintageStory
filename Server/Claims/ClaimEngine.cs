using MathClaims.Models;

namespace MathClaims.Server.Claims;

public sealed record ClaimResult(bool Success, string Message, Guid? PropertyId = null)
{
    public static ClaimResult Ok(Guid id, string message = "OK") => new(true, message, id);
    public static ClaimResult Fail(string message) => new(false, message);
}

public sealed class ClaimEngine
{
    private readonly ClaimConfig config;
    public ClaimState State { get; }
    private readonly Dictionary<ChunkColumn, Guid> index = [];
    public ClaimEngine(ClaimState state, ClaimConfig config)
    {
        State = state; this.config = config;
        foreach (var property in state.Properties.Values) foreach (var chunk in property.Chunks) index.Add(chunk, property.Id);
    }
    public ClaimProperty? At(ChunkColumn chunk) => index.TryGetValue(chunk, out var id) ? State.Properties[id] : null;
    public int ChunkCount(string uid) => State.Properties.Values.Where(p => p.OwnerUid == uid).Sum(p => p.Chunks.Count);
    public int PropertyCount(string uid) => State.Properties.Values.Count(p => p.OwnerUid == uid);
    public OverlayColor GetOverlay(string uid, ClaimProperty property) => property.OwnerUid == uid ? OverlayColor.Green : property.Permissions.TryGetValue(uid, out var flags) && flags != ClaimPermission.None ? OverlayColor.Yellow : OverlayColor.Red;
    public static bool HasPermission(string uid, ClaimProperty property, ClaimPermission required) =>
        property.OwnerUid == uid || (property.Permissions.TryGetValue(uid, out var granted) && (granted & required) != 0);

    public ClaimResult Rename(string actor, Guid id, string rawName, bool ownerOrManager)
    {
        if (!ownerOrManager || !State.Properties.TryGetValue(id, out var property)) return ClaimResult.Fail("Sem permissão para gerenciar esta propriedade.");
        if (!ClaimName.TryNormalize(rawName, out var name)) return ClaimResult.Fail("Informe um nome de propriedade válido (1–48 caracteres).");
        property.Name = name;
        return ClaimResult.Ok(id, "Propriedade renomeada.");
    }

    public ClaimResult SetPermissions(string actor, Guid id, string targetUid, ClaimPermission permissions, bool ownerOrManager)
    {
        if (!ownerOrManager || !State.Properties.TryGetValue(id, out var property)) return ClaimResult.Fail("Sem permissão para gerenciar esta propriedade.");
        if (string.IsNullOrWhiteSpace(targetUid) || targetUid == property.OwnerUid) return ClaimResult.Fail("Não é possível alterar as permissões do proprietário.");
        permissions &= ClaimPermission.All;
        if (permissions == ClaimPermission.None) property.Permissions.Remove(targetUid);
        else property.Permissions[targetUid] = permissions;
        return ClaimResult.Ok(id, permissions == ClaimPermission.None ? "Acesso removido." : "Permissões atualizadas.");
    }

    public ClaimResult SetPvp(string actor, Guid id, bool enabled)
    {
        if (!State.Properties.TryGetValue(id, out var property) || property.OwnerUid != actor)
            return ClaimResult.Fail("Somente o proprietário pode alterar o PvP.");
        property.PvpEnabled = enabled;
        return ClaimResult.Ok(id, enabled ? "PvP ativado nesta propriedade." : "PvP desativado nesta propriedade.");
    }

    public ClaimResult Create(string owner, string rawName, IEnumerable<ChunkColumn> requested, Func<ChunkColumn, string?> invalidReason)
    {
        var validation = ValidateCreate(owner, rawName, requested, invalidReason);
        if (!validation.Success) return validation;
        var chunks = requested.Distinct().ToHashSet();
        var adjacent = OwnedAdjacent(owner, chunks);
        var createsProperty = adjacent.Count == 0;
        ClaimName.TryNormalize(rawName, out var name);
        var target = adjacent.SingleOrDefault() ?? new ClaimProperty { OwnerUid = owner, Name = name };
        if (createsProperty) State.Properties.Add(target.Id, target);
        foreach (var chunk in chunks) { target.Chunks.Add(chunk); index.Add(chunk, target.Id); }
        return ClaimResult.Ok(target.Id, createsProperty ? "Propriedade criada." : "Terrenos adicionados à propriedade.");
    }

    public ClaimResult ValidateCreate(string owner, string rawName, IEnumerable<ChunkColumn> requested, Func<ChunkColumn, string?> invalidReason)
    {
        var chunks = requested.Distinct().ToHashSet();
        if (chunks.Count == 0) return ClaimResult.Fail("Selecione ao menos um terreno válido.");
        if (!IsConnected(chunks)) return ClaimResult.Fail("A seleção precisa ser conectada lateralmente.");
        foreach (var chunk in chunks) { var reason = invalidReason(chunk); if (reason is not null) return ClaimResult.Fail(reason); if (At(chunk) is not null) return ClaimResult.Fail("Um terreno selecionado já possui proteção."); }
        if (ViolatesMinimumDistance(owner, chunks)) return ClaimResult.Fail("A seleção está próxima demais de uma propriedade de outro jogador.");
        var adjacent = OwnedAdjacent(owner, chunks);
        if (adjacent.Count > 1) return ClaimResult.Fail("Esse terreno conectaria duas propriedades existentes.");
        var createsProperty = adjacent.Count == 0;
        var name = "";
        if (createsProperty && !ClaimName.TryNormalize(rawName, out name)) return ClaimResult.Fail("Informe um nome de propriedade válido (1–48 caracteres).");
        if (ChunkCount(owner) + chunks.Count > config.MaxChunksPerPlayer) return ClaimResult.Fail("Limite total de terrenos atingido.");
        if (createsProperty && PropertyCount(owner) >= config.MaxPropertiesPerPlayer) return ClaimResult.Fail("Limite de propriedades atingido.");
        return ClaimResult.Ok(adjacent.SingleOrDefault()?.Id ?? Guid.Empty, createsProperty ? "Seleção válida para nova propriedade." : "Seleção válida para adicionar à propriedade adjacente.");
    }

    public ClaimResult RemoveChunks(string actor, Guid id, IEnumerable<ChunkColumn> removal, bool ownerOrManager)
    {
        if (!ownerOrManager || !State.Properties.TryGetValue(id, out var property)) return ClaimResult.Fail("Sem permissão para gerenciar esta propriedade.");
        var next = property.Chunks.Except(removal).ToHashSet();
        var components = Components(next);
        var projected = PropertyCount(property.OwnerUid) - 1 + components.Count;
        if (projected > config.MaxPropertiesPerPlayer) return ClaimResult.Fail("A remoção excederia o limite de propriedades.");
        foreach (var old in property.Chunks) index.Remove(old); State.Properties.Remove(id);
        foreach (var component in components)
        {
            var split = new ClaimProperty { OwnerUid = property.OwnerUid, Name = property.Name, Chunks = component, Permissions = new(property.Permissions, StringComparer.OrdinalIgnoreCase), PvpEnabled = property.PvpEnabled };
            State.Properties.Add(split.Id, split); foreach (var chunk in component) index.Add(chunk, split.Id);
        }
        return new(true, "Terrenos removidos; componentes recalculados.");
    }

    public ClaimResult Transfer(string actor, Guid id, string recipient, bool force = false)
    {
        if (!State.Properties.TryGetValue(id, out var property)) return ClaimResult.Fail("Propriedade não encontrada.");
        if (!force && property.OwnerUid != actor) return ClaimResult.Fail("Somente o proprietário pode transferir.");
        if (recipient == property.OwnerUid) return ClaimResult.Fail("O destinatário já é proprietário.");
        if (ViolatesMinimumDistance(recipient, property.Chunks, property.Id))
            return ClaimResult.Fail("A transferência violaria a distância mínima entre propriedades de donos diferentes.");
        if (!config.AdminIgnoresLimits || !force)
        {
            if (ChunkCount(recipient) + property.Chunks.Count > config.MaxChunksPerPlayer) return ClaimResult.Fail("Destinatário sem espaço para os terrenos.");
            var adjacentRecipients = AdjacentProperties(property.Chunks, recipient, property.Id);
            if (PropertyCount(recipient) + (adjacentRecipients.Count == 0 ? 1 : 0) > config.MaxPropertiesPerPlayer) return ClaimResult.Fail("Destinatário atingiria o limite de propriedades.");
        }
        property.OwnerUid = recipient; property.Permissions.Clear();
        var merges = AdjacentProperties(property.Chunks, recipient, property.Id);
        if (merges.Count > 0)
        {
            var recipientProperty = merges[0];
            foreach (var chunk in property.Chunks) { recipientProperty.Chunks.Add(chunk); index[chunk] = recipientProperty.Id; }
            foreach (var other in merges.Skip(1))
            {
                foreach (var chunk in other.Chunks) { recipientProperty.Chunks.Add(chunk); index[chunk] = recipientProperty.Id; }
                State.Properties.Remove(other.Id);
            }
            State.Properties.Remove(property.Id);
            return ClaimResult.Ok(recipientProperty.Id, "Propriedade transferida e fundida.");
        }
        return ClaimResult.Ok(property.Id, "Propriedade transferida.");
    }

    public ClaimResult Abandon(string actor, Guid id)
    {
        if (!State.Properties.TryGetValue(id, out var property)) return ClaimResult.Fail("Propriedade não encontrada.");
        if (property.OwnerUid != actor) return ClaimResult.Fail("Somente o proprietário pode abandonar a propriedade.");
        foreach (var chunk in property.Chunks) index.Remove(chunk);
        State.Properties.Remove(id);
        return ClaimResult.Ok(id, "Propriedade abandonada. Os blocos do mundo não foram alterados.");
    }

    public ClaimResult Delete(Guid id)
    {
        if (!State.Properties.TryGetValue(id, out var property)) return ClaimResult.Fail("Propriedade não encontrada.");
        foreach (var chunk in property.Chunks) index.Remove(chunk);
        State.Properties.Remove(id);
        return ClaimResult.Ok(id, "Propriedade excluída. Os blocos do mundo não foram alterados.");
    }

    public IReadOnlyList<ClaimProperty> Expire(Func<string, bool> immune)
    {
        if (!config.ExpirationEnabled) return [];
        var expired = State.Properties.Values.Where(p => !immune(p.OwnerUid) && State.PlayerActivity.TryGetValue(p.OwnerUid, out var a) && a.OfflineUptimeSeconds >= config.ExpirationUptimeSeconds).ToList();
        foreach (var property in expired) { foreach (var chunk in property.Chunks) index.Remove(chunk); State.Properties.Remove(property.Id); }
        return expired;
    }
    public void AdvanceUptime(TimeSpan elapsed, IEnumerable<string> online) { var set = online.ToHashSet(StringComparer.OrdinalIgnoreCase); foreach (var uid in State.Properties.Values.Select(p => p.OwnerUid).Distinct()) { var activity = GetActivity(uid); activity.OfflineUptimeSeconds = set.Contains(uid) ? 0 : activity.OfflineUptimeSeconds + elapsed.TotalSeconds; } }
    public PlayerActivity GetActivity(string uid) => State.PlayerActivity.TryGetValue(uid, out var a) ? a : State.PlayerActivity[uid] = new PlayerActivity();

    private bool ViolatesMinimumDistance(string owner, IReadOnlyCollection<ChunkColumn> requested, Guid? ignoredProperty = null)
    {
        if (!config.MinimumDistanceEnabled || config.MinimumDistanceChunks <= 0) return false;
        var radius = config.MinimumDistanceChunks;
        foreach (var chunk in requested)
        {
            for (var dx = -radius; dx <= radius; dx++)
            for (var dz = -radius; dz <= radius; dz++)
            {
                var neighboringClaim = At(new ChunkColumn(chunk.Dimension, chunk.X + dx, chunk.Z + dz));
                if (neighboringClaim is not null && neighboringClaim.Id != ignoredProperty && neighboringClaim.OwnerUid != owner) return true;
            }
        }
        return false;
    }
    private List<ClaimProperty> AdjacentProperties(IEnumerable<ChunkColumn> chunks, string owner, Guid excluded) => chunks.SelectMany(c => c.Neighbors()).Select(At).Where(p => p is not null && p.Id != excluded && p.OwnerUid == owner).Cast<ClaimProperty>().DistinctBy(p => p.Id).ToList();
    private List<ClaimProperty> OwnedAdjacent(string owner, IReadOnlySet<ChunkColumn> chunks) => chunks.SelectMany(c => c.Neighbors()).Where(n => !chunks.Contains(n)).Select(At).Where(p => p?.OwnerUid == owner).Cast<ClaimProperty>().DistinctBy(p => p.Id).ToList();
    public static bool IsConnected(IReadOnlyCollection<ChunkColumn> chunks) => chunks.Count > 0 && Components(chunks).Count == 1;
    public static List<HashSet<ChunkColumn>> Components(IEnumerable<ChunkColumn> source)
    {
        var remaining = source.ToHashSet(); var result = new List<HashSet<ChunkColumn>>();
        while (remaining.Count > 0) { var component = new HashSet<ChunkColumn>(); var queue = new Queue<ChunkColumn>(); queue.Enqueue(remaining.First()); while (queue.Count > 0) { var c = queue.Dequeue(); if (!remaining.Remove(c)) continue; component.Add(c); foreach (var n in c.Neighbors()) if (remaining.Contains(n)) queue.Enqueue(n); } result.Add(component); }
        return result;
    }
}
