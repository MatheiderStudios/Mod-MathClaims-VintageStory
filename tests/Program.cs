using MathClaims.Models;
using MathClaims.Server.Claims;
using MathClaims.Client.Map;
using MathClaims.Server.Protection;
using MathClaims.Persistence;

static class Test
{
    static int checks;
    static void That(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static ClaimEngine Engine(int maxProperties = 3, int maxChunks = 64) => new(new ClaimState(), new ClaimConfig { MaxPropertiesPerPlayer = maxProperties, MaxChunksPerPlayer = maxChunks });
    static string? Valid(ChunkColumn _) => null;
    static void Main()
    {
        var a = new ChunkColumn(0, 0, 0); That(ClaimEngine.IsConnected([a, new(0, 0, 1)]), "north/south connects"); That(ClaimEngine.IsConnected([a, new(0, 1, 0)]), "east/west connects"); That(!ClaimEngine.IsConnected([a, new(0, 1, 1)]), "diagonal does not connect");
        That(ClaimGeometry.CellSize == 16 && ClaimGeometry.ToCell(-1) == -1 && ClaimGeometry.ToCell(-16) == -1 && ClaimGeometry.ToCell(-17) == -2, "16x16 geometry uses floor division on negative positions");
        That(ExplorationSector.FromCell(new ChunkColumn(0, 0, 0)) == new ExplorationSector(0, 0, 0) &&
             ExplorationSector.FromCell(new ChunkColumn(0, 1, 1)) == new ExplorationSector(0, 0, 0) &&
             ExplorationSector.FromCell(new ChunkColumn(0, -1, -1)) == new ExplorationSector(0, -1, -1),
             "exploration sectors preserve 32x32 boundaries including negative cells");
        var explored = new ClientExplorationState { Sectors = [new ExplorationSector(0, 5, 7)] };
        That(explored.Sectors.Contains(ExplorationSector.FromCell(new ChunkColumn(0, 10, 15))) && !explored.Sectors.Contains(ExplorationSector.FromCell(new ChunkColumn(0, 12, 15))),
             "undiscovered overlay cache only identifies previously loaded sectors");
        var legacy = new ClaimState { SchemaVersion = 1 }; var legacyProperty = new ClaimProperty { OwnerUid = "a", Name = "Legada", Chunks = [new ChunkColumn(0, -1, 2)] }; legacy.Properties.Add(legacyProperty.Id, legacyProperty);
        That(ClaimStateMigration.Upgrade(legacy) && legacy.SchemaVersion == 2 && legacyProperty.Chunks.SetEquals([new(0, -2, 4), new(0, -1, 4), new(0, -2, 5), new(0, -1, 5)]), "32x32 persisted cells expand losslessly into four 16x16 cells");
        var engine = Engine(); That(engine.Create("a", "Casa A", [a, new(0, 1, 0)], Valid).Success, "connected property creates"); That(engine.PropertyCount("a") == 1, "connected property count");
        That(engine.Create("a", "", [new(0, 2, 0)], Valid).Success && engine.PropertyCount("a") == 1, "adjacent addition does not require a new name");
        var multiCellDrag = Engine(); That(multiCellDrag.Create("a", "Base", [new(0, 3, 0)], Valid).Success && multiCellDrag.Create("a", "", [new(0, 1, 0), new(0, 2, 0)], Valid).Success && multiCellDrag.PropertyCount("a") == 1, "drag selection joins an adjacent property even when its first cell was not adjacent");
        That(!engine.Create("a", "", [new(0, 9, 0)], Valid).Success, "new separate property still requires a name");
        That(engine.Create("a", "Casa B", [new(0, 5, 0)], Valid).Success, "second property creates"); That(engine.Create("a", "Casa C", [new(0, 7, 0)], Valid).Success, "third property creates"); That(!engine.Create("a", "Casa D", [new(0, 9, 0)], Valid).Success, "fourth property denied");
        var bridge = Engine(); That(bridge.Create("a", "A", [new(0, 0, 0)], Valid).Success, "bridge left"); That(bridge.Create("a", "B", [new(0, 2, 0)], Valid).Success, "bridge right"); That(!bridge.Create("a", "x", [new(0, 1, 0)], Valid).Success, "bridge denied");
        var distance = new ClaimEngine(new ClaimState(), new ClaimConfig { MinimumDistanceEnabled = true, MinimumDistanceChunks = 1 }); That(distance.Create("a", "A", [a], Valid).Success, "distance source creates"); That(!distance.Create("b", "B", [new(0, 1, 0)], Valid).Success, "minimum distance denies different owner"); That(distance.Create("a", "A2", [new(0, 1, 0)], Valid).Success, "minimum distance allows same owner");
        var limits = Engine(maxChunks: 64); var columns = Enumerable.Range(0, 64).Select(x => new ChunkColumn(0, x, 0)); That(limits.Create("a", "64", columns, Valid).Success, "64 chunks accepted"); That(!limits.Create("a", "65", [new(0, 65, 0)], Valid).Success, "65 chunks denied");
        var split = Engine(); var created = split.Create("a", "Split", [new(0, 0, 0), new(0, 1, 0), new(0, 2, 0)], Valid); That(split.RemoveChunks("a", created.PropertyId!.Value, [new(0, 1, 0)], true).Success && split.PropertyCount("a") == 2, "split recalculates");
        var transfer = Engine(); var p = transfer.Create("a", "Gift", [new(0, 0, 0)], Valid); That(transfer.Create("b", "B", [new(0, 1, 0)], Valid).Success, "recipient neighboring property"); That(transfer.Transfer("a", p.PropertyId!.Value, "b").Success && transfer.PropertyCount("b") == 1, "transfer merges adjacency");
        var transferState = new ClaimState(); var transferSetup = new ClaimEngine(transferState, new ClaimConfig()); var transferSource = transferSetup.Create("a", "Source", [a], Valid); That(transferSetup.Create("b", "Other", [new(0, 1, 0)], Valid).Success, "transfer distance other owner source"); var distanceTransfer = new ClaimEngine(transferState, new ClaimConfig { MinimumDistanceEnabled = true, MinimumDistanceChunks = 1 }); That(!distanceTransfer.Transfer("a", transferSource.PropertyId!.Value, "c").Success, "transfer validates minimum distance before mutation");
        var property = transfer.State.Properties.Values.Single(); That(property.Name == "B", "recipient metadata survives merge"); property.Permissions["c"] = ClaimPermission.Doors; That(transfer.GetOverlay("b", property) == OverlayColor.Green && transfer.GetOverlay("c", property) == OverlayColor.Yellow && transfer.GetOverlay("d", property) == OverlayColor.Red, "overlay colors");
        That(ClaimEngine.HasPermission("b", property, ClaimPermission.BuildBreak) && ClaimEngine.HasPermission("c", property, ClaimPermission.Doors) && !ClaimEngine.HasPermission("c", property, ClaimPermission.BuildBreak) && !ClaimEngine.HasPermission("d", property, ClaimPermission.Doors), "multiplayer permission checks keep each action category server-authoritative");
        That(!transfer.Rename("d", property.Id, "Novo", false).Success && transfer.Rename("b", property.Id, " Novo ", true).Success && property.Name == "Novo", "rename validates permission and trim");
        That(transfer.SetPermissions("b", property.Id, "c", ClaimPermission.All, true).Success && property.Permissions["c"] == ClaimPermission.All, "permission update stores all flags");
        That(transfer.SetPermissions("b", property.Id, "c", ClaimPermission.None, true).Success && !property.Permissions.ContainsKey("c"), "zero permission removes access");
        That(!transfer.SetPermissions("d", property.Id, "c", ClaimPermission.BuildBreak, false).Success, "permission update requires management");
        That(transfer.SetPvp("b", property.Id, true).Success && property.PvpEnabled, "owner can enable property PvP");
        That(!transfer.SetPvp("c", property.Id, false).Success && property.PvpEnabled, "non-owner cannot alter property PvP");
        That(transfer.SetPvp("b", property.Id, false).Success && !property.PvpEnabled, "owner can disable property PvP");
        That(!PvpSafetyPolicy.ShouldBlock(false, false) && PvpSafetyPolicy.ShouldBlock(true, false) && PvpSafetyPolicy.ShouldBlock(false, true), "disabled own-claim PvP blocks player damage for both attacker and victim");
        That(UsePermissionClassifier.RequiredForBlockCode("door-solid") == ClaimPermission.Doors && UsePermissionClassifier.RequiredForBlockCode("crate") == ClaimPermission.Containers && UsePermissionClassifier.RequiredForBlockCode("water-still") == ClaimPermission.Liquids && UsePermissionClassifier.RequiredForBlockCode("quern") == ClaimPermission.Machines, "block interactions require their exact permission category");
        That(VanillaLandCommandGuard.IsVanillaLandCommand("/land claim") && VanillaLandCommandGuard.IsVanillaLandCommand("  /LAND list") && !VanillaLandCommandGuard.IsVanillaLandCommand("/landscape") && !VanillaLandCommandGuard.IsVanillaLandCommand("hello /land"), "only vanilla land command family is blocked");
        That(NpcStructureClaimPolicy.IsNpcStructureOwner("custommessage-nadiya") && NpcStructureClaimPolicy.IsNpcStructureOwner("custommessage-treasurehunter") && !NpcStructureClaimPolicy.IsNpcStructureOwner("Matheider") && !NpcStructureClaimPolicy.IsNpcStructureOwner("Nadiya"), "NPC house release only matches exact vanilla internal owner codes");
        var abandon = Engine(); var disposable = abandon.Create("a", "Temporária", [a], Valid); That(!abandon.Abandon("b", disposable.PropertyId!.Value).Success && abandon.Abandon("a", disposable.PropertyId.Value).Success && abandon.At(a) is null, "abandon is owner-only and clears index");
        var adminDelete = Engine(); var removedByAdmin = adminDelete.Create("a", "Admin", [a], Valid); That(adminDelete.Delete(removedByAdmin.PropertyId!.Value).Success && adminDelete.At(a) is null && adminDelete.State.Properties.Count == 0, "administrative delete clears only claim index and metadata");
        var expiry = Engine(); var doomed = expiry.Create("a", "Expira", [a], Valid); expiry.State.PlayerActivity["a"] = new PlayerActivity { OfflineUptimeSeconds = 61 }; var config = new ClaimConfig { ExpirationEnabled = true, ExpirationUptimeSeconds = 60 }; var expiring = new ClaimEngine(expiry.State, config); That(expiring.Expire(_ => false).Single().Id == doomed.PropertyId, "uptime expiration removes claim");
        var defaults = new ClaimConfig(); That(defaults.SpawnNoClaimSize == 3 && !defaults.SpawnProtectionEnabled && defaults.SpawnProtectionPermissions == ClaimPermission.All, "spawn is no-claim but unprotected by default");
        var batchRelease = Engine(); var batchProperty = batchRelease.Create("a", "Lote", [new(0, 0, 0), new(0, 1, 0)], Valid);
        That(batchRelease.RemoveChunks("a", batchProperty.PropertyId!.Value, [new(0, 0, 0), new(0, 1, 0)], true).Success && batchRelease.State.Properties.Count == 0, "releasing all selected cells clears only the property metadata");
        var selection = new MapSelection();
        selection.Begin(new(0, 0, 0), false, _ => true); selection.Rectangle(new(0, 2, 2), _ => true);
        That(selection.Columns.Count == 9, "drag expands selection");
        selection.Rectangle(new(0, 1, 1), _ => true); That(selection.Columns.Count == 4, "drag shrinking recalculates instead of accumulating");
        That(selection.Release() && !selection.Release(), "only one context request is released per drag");
        selection.Begin(new(0, 0, 0), true, _ => true); selection.Begin(new(0, 1, 0), true, _ => true);
        selection.Begin(new(0, 3, 0), true, _ => true); That(selection.Columns.Count == 2, "shift refuses disconnected island");
        selection.Begin(new(0, 2, 0), true, _ => true); selection.Begin(new(0, 1, 0), true, _ => true);
        That(selection.Columns.Count == 3, "shift refuses removal that would split selection");
        var trail = new MapSelection(); trail.Begin(new(0, 0, 0), true, _ => true); trail.Trail(new(0, 3, 2), _ => true);
        That(trail.Columns.Count == 6 && ClaimEngine.IsConnected(trail.Columns), "shift drag leaves a connected cell trail");
        Console.WriteLine($"PASS: {checks} checks");
    }
}
