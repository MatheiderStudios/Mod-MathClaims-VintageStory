using MathClaims.Client.Rendering;
using MathClaims.Models;
using MathClaims.Networking;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace MathClaims.Client.Map;

public sealed class ClaimMapLayer : MapLayer
{
    public static ClaimMapLayer? Instance { get; private set; }
    public static bool PopupBusy { get; set; }
    public static Action<ChunkColumn, IReadOnlyCollection<ChunkColumn>, Vec3d>? OnContextRequested;
    public static Action<ChunkColumn, ClaimMapProperty>? OnPropertyRequested;
    public static Action<ClaimMapProperty, IReadOnlyCollection<ChunkColumn>>? OnReleaseRequested;
    private readonly MapSelection selection = new();
    private readonly MapSelection releaseSelection = new();
    private static readonly Dictionary<ChunkColumn, ClaimMapProperty> claimVisuals = [];
    private static readonly Dictionary<(int Dimension, int RegionX, int RegionZ), List<ChunkColumn>> worldSpatialIndex = [];
    private const int WorldIndexRegionSize = 8;
    public static SpawnVisualRegion? SpawnRegion { get; private set; }
    private static bool showClaimsOnMinimap = true;
    private static bool showClaimsInUndiscoveredAreas;
    private static readonly HashSet<ExplorationSector> exploredSectors = [];
    private static bool explorationDirty;
    private static double nextExplorationSaveHours;
    public static bool ShowClaimsInUndiscoveredAreas => showClaimsInUndiscoveredAreas;
    private ChunkColumn? hovered;
    private Vec3d clickedWorld = new();
    private bool ownedPress;
    private bool releasePress;
    private ClaimMapProperty? propertyPress;
    private ClaimMapProperty? releaseProperty;
    public override string Title => "MathClaims";
    public override string LayerGroupCode => "mathclaims";
    public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

    public ClaimMapLayer(ICoreAPI api, IWorldMapManager sink) : base(api, sink)
    {
        if (api is ICoreClientAPI)
        {
            Instance = this;
            PopupBusy = false;
        }
    }
    public static void ClearSelection()
    {
        Instance?.selection.Clear();
        Instance?.releaseSelection.Clear();
    }
    public static void RecordClientExploration(ICoreClientAPI api)
    {
        var entity = api.World.Player?.Entity;
        if (entity is null) return;
        var totalHours = api.World.Calendar?.TotalHours ?? 0;
        var pos = entity.Pos;
        var center = new ChunkColumn(pos.Dimension, ClaimGeometry.ToCell((int)Math.Floor(pos.X)), ClaimGeometry.ToCell((int)Math.Floor(pos.Z)));
        var sector = ExplorationSector.FromCell(center);
        var changed = false;
        for (var x = sector.X - 8; x <= sector.X + 8; x++)
        for (var z = sector.Z - 8; z <= sector.Z + 8; z++)
            changed |= exploredSectors.Add(new ExplorationSector(sector.Dimension, x, z));
        if (changed) explorationDirty = true;
        if (!explorationDirty) return;
        if (totalHours < nextExplorationSaveHours) return;
        api.StoreModConfig(new ClientExplorationState { Sectors = exploredSectors.ToList() }, "mathclaims-exploration.json");
        explorationDirty = false;
        nextExplorationSaveHours = totalHours + 1;
    }
    public static void LoadClientExploration(ICoreClientAPI api)
    {
        exploredSectors.Clear();
        foreach (var sector in (api.LoadModConfig<ClientExplorationState>("mathclaims-exploration.json")?.Sectors ?? [])) exploredSectors.Add(sector);
        explorationDirty = false;
        nextExplorationSaveHours = 0;
    }
    private static bool ShouldRenderClaim(ChunkColumn column) => showClaimsInUndiscoveredAreas || exploredSectors.Contains(ExplorationSector.FromCell(column));
    public static void ReplaceClaimVisuals(ClaimVisualSnapshot snapshot)
    {
        claimVisuals.Clear();
        worldSpatialIndex.Clear();
        foreach (var c in snapshot.Chunks)
        {
            var property = new ClaimMapProperty(c.PropertyId, c.PropertyName, c.OwnerName, c.PropertyChunkCount, (OverlayColor)c.Color, c.CanManage, c.CanAdminister, c.PvpEnabled);
            var column = new ChunkColumn(c.Dimension, c.ChunkX, c.ChunkZ);
            claimVisuals[column] = property;
            var region = (column.Dimension, FloorDiv(column.X, WorldIndexRegionSize), FloorDiv(column.Z, WorldIndexRegionSize));
            if (!worldSpatialIndex.TryGetValue(region, out var columns)) worldSpatialIndex[region] = columns = [];
            columns.Add(column);
        }
        SpawnRegion = snapshot.SpawnProtected
            ? new SpawnVisualRegion(snapshot.SpawnDimension, snapshot.SpawnMinChunkX, snapshot.SpawnMaxChunkX, snapshot.SpawnMinChunkZ, snapshot.SpawnMaxChunkZ)
            : null;
        showClaimsOnMinimap = snapshot.ShowClaimsOnMinimap;
        showClaimsInUndiscoveredAreas = snapshot.ShowClaimsInUndiscoveredAreas;
    }
    public static bool TryGetClaim(ChunkColumn column, out ClaimMapProperty property) => claimVisuals.TryGetValue(column, out property!);
    public static IEnumerable<(ChunkColumn Column, ClaimMapProperty Property)> NearbyWorldClaims(int dimension, int chunkX, int chunkZ, int radiusChunks)
    {
        var minRegionX = FloorDiv(chunkX - radiusChunks, WorldIndexRegionSize);
        var maxRegionX = FloorDiv(chunkX + radiusChunks, WorldIndexRegionSize);
        var minRegionZ = FloorDiv(chunkZ - radiusChunks, WorldIndexRegionSize);
        var maxRegionZ = FloorDiv(chunkZ + radiusChunks, WorldIndexRegionSize);
        for (var regionX = minRegionX; regionX <= maxRegionX; regionX++)
        for (var regionZ = minRegionZ; regionZ <= maxRegionZ; regionZ++)
        {
            if (!worldSpatialIndex.TryGetValue((dimension, regionX, regionZ), out var columns)) continue;
            foreach (var column in columns)
                if (Math.Abs(column.X - chunkX) <= radiusChunks && Math.Abs(column.Z - chunkZ) <= radiusChunks)
                    yield return (column, claimVisuals[column]);
        }
    }
    private GuiDialogWorldMap? FullMap => (mapSink as WorldMapManager)?.worldMapDlg is { } map &&
        map.IsOpened() && map.DialogType == EnumDialogType.Dialog ? map : null;
    private bool Allowed(ChunkColumn c) => !claimVisuals.ContainsKey(c) &&
        (SpawnRegion is not { } spawn || c.Dimension != spawn.Dimension || c.X < spawn.MinChunkX || c.X > spawn.MaxChunkX || c.Z < spawn.MinChunkZ || c.Z > spawn.MaxChunkZ);
    private string? InvalidReason(ChunkColumn c)
    {
        if (claimVisuals.ContainsKey(c)) return "Este terreno já está protegido e não pode ser selecionado.";
        if (SpawnRegion is { } spawn && c.Dimension == spawn.Dimension && c.X >= spawn.MinChunkX && c.X <= spawn.MaxChunkX && c.Z >= spawn.MinChunkZ && c.Z <= spawn.MaxChunkZ)
            return "Não é possível proteger terrenos dentro da região de spawn.";
        return null;
    }
    private bool Shift => api is ICoreClientAPI client &&
        (client.Input.KeyboardKeyStateRaw[(int)GlKeys.ShiftLeft] || client.Input.KeyboardKeyStateRaw[(int)GlKeys.ShiftRight]);
    private static GuiElementMap? Element(GuiDialogWorldMap map) => map.SingleComposer?.GetElement("mapElem") as GuiElementMap;
    private static bool Inside(GuiElementMap map, int x, int y) => map.Bounds.PointInside(x, y);

    public bool MouseDown(GuiDialogWorldMap map, MouseEvent e)
    {
        if (PopupBusy || map != FullMap || !Active || !map.Focused || Element(map) is not { } element ||
            !Inside(element, e.X, e.Y) || (e.Button != EnumMouseButton.Right && !(Shift && e.Button == EnumMouseButton.Left))) return false;
        if (e.Button == EnumMouseButton.Right && IsWaypointAt(element, e.X, e.Y))
        {
            ClearSelection();
            return false;
        }
        clickedWorld = WorldAt(element, e.X, e.Y);
        var column = Column(clickedWorld);
        if (e.Button == EnumMouseButton.Right && Shift)
        {
            var property = claimVisuals.GetValueOrDefault(column);
            if (property is null)
            {
                ((ICoreClientAPI)api).ShowChatMessage("MathClaims: use Shift+botão direito sobre um terreno protegido para desprotegê-lo.");
                e.Handled = true;
                return true;
            }
            if (!property.CanManage)
            {
                ((ICoreClientAPI)api).ShowChatMessage("MathClaims: você não tem permissão para desproteger terrenos desta propriedade.");
                e.Handled = true;
                return true;
            }
            releaseProperty = property;
            releaseSelection.Begin(column, false, cell => BelongsToProperty(cell, property));
            releasePress = true;
            e.Handled = true;
            return true;
        }
        propertyPress = e.Button == EnumMouseButton.Right ? claimVisuals.GetValueOrDefault(column) : null;
        if (propertyPress is null)
        {
            if (!Allowed(column))
            {
                ((ICoreClientAPI)api).ShowChatMessage($"MathClaims: {InvalidReason(column) ?? "Este terreno não pode ser protegido."}");
                return false;
            }
            selection.Begin(column, Shift, Allowed);
        }
        ownedPress = true;
        e.Handled = true;
        return true;
    }
    public bool MouseUp(GuiDialogWorldMap map, MouseEvent e)
    {
        if (releasePress)
        {
            releasePress = false;
            e.Handled = true;
            if (PopupBusy || map != FullMap || releaseProperty is null || e.Button != EnumMouseButton.Right) return true;
            if (Element(map) is { } releaseElement && Inside(releaseElement, e.X, e.Y))
            {
                releaseSelection.Rectangle(Column(WorldAt(releaseElement, e.X, e.Y)), cell => BelongsToProperty(cell, releaseProperty));
                if (releaseSelection.Release()) RequestRelease(releaseProperty);
            }
            else releaseSelection.Clear();
            return true;
        }
        if (!ownedPress || PopupBusy || map != FullMap ||
            (e.Button != EnumMouseButton.Right && e.Button != EnumMouseButton.Left)) return false;
        ownedPress = false;
        e.Handled = true;
        if (propertyPress is { } property)
        {
            propertyPress = null;
            if (Element(map) is { } propertyElement && Inside(propertyElement, e.X, e.Y)) RequestProperty(property);
            return true;
        }
        if (Element(map) is { } element && Inside(element, e.X, e.Y))
        {
            selection.Rectangle(Column(WorldAt(element, e.X, e.Y)), Allowed);
            if (selection.Release()) RequestContext();
        }
        else selection.Clear();
        return true;
    }
    public override void OnMouseMoveClient(MouseEvent e, GuiElementMap map, StringBuilder text)
    {
        if (PopupBusy || FullMap is not { Focused: true } || !Inside(map, e.X, e.Y)) { hovered = null; return; }
        hovered = Column(WorldAt(map, e.X, e.Y));
        if (ownedPress)
        {
            if (selection.Manual) selection.Trail(hovered.Value, Allowed);
            else selection.Rectangle(hovered.Value, Allowed);
        }
        if (releasePress && releaseProperty is not null)
        {
            releaseSelection.Rectangle(hovered.Value, cell => BelongsToProperty(cell, releaseProperty));
            text.AppendLine($"MathClaims • {releaseSelection.Columns.Count} terreno(s) serão desprotegidos");
            return;
        }
        text.AppendLine($"MathClaims • {selection.Columns.Count} terreno(s) selecionado(s)");
    }
    public override void OnTick(float dt)
    {
        if (FullMap is null) { ClearSelection(); ownedPress = false; releasePress = false; releaseProperty = null; hovered = null; return; }
        if (!PopupBusy && selection.Manual && !Shift)
        {
            ownedPress = false;
            if (selection.ReleaseShift()) RequestContext();
        }
    }
    private void RequestContext()
    {
        if (PopupBusy || selection.Columns.Count == 0) return;
        PopupBusy = true;
        var snapshot = selection.Columns.ToArray();
        var position = clickedWorld.Clone();
        ((ICoreClientAPI)api).Event.EnqueueMainThreadTask(() =>
        {
            if (FullMap is null) { PopupBusy = false; selection.Clear(); return; }
            OnContextRequested?.Invoke(snapshot[0], snapshot, position);
        }, "mathclaims-open-context");
    }
    private void RequestProperty(ClaimMapProperty property)
    {
        if (PopupBusy) return;
        PopupBusy = true;
        ((ICoreClientAPI)api).Event.EnqueueMainThreadTask(() =>
        {
            if (FullMap is null) { PopupBusy = false; return; }
            OnPropertyRequested?.Invoke(Column(clickedWorld), property);
        }, "mathclaims-open-property-info");
    }
    private void RequestRelease(ClaimMapProperty property)
    {
        if (PopupBusy || releaseSelection.Columns.Count == 0) return;
        PopupBusy = true;
        var snapshot = releaseSelection.Columns.ToArray();
        ((ICoreClientAPI)api).Event.EnqueueMainThreadTask(() =>
        {
            if (FullMap is null) { PopupBusy = false; ClearSelection(); return; }
            OnReleaseRequested?.Invoke(property, snapshot);
        }, "mathclaims-confirm-release");
    }
    public override void Render(GuiElementMap map, float dt)
    {
        if (!Active) return;
        var bounds = map.CurrentBlockViewBounds;
        var fullMap = FullMap;
        if (fullMap is not null || showClaimsOnMinimap)
        {
            foreach (var (c, property) in claimVisuals)
            {
                if (!ShouldRenderClaim(c)) continue;
                if (ClaimGeometry.ToWorld(c.X) > bounds.X2 || ClaimGeometry.ToWorld(c.X + 1) < bounds.X1 ||
                    ClaimGeometry.ToWorld(c.Z) > bounds.Z2 || ClaimGeometry.ToWorld(c.Z + 1) < bounds.Z1) continue;
                OutlineClaimBoundary(map, c, property, ClaimVisualPalette.ForClaim(property.Color), 56);
            }
        }
        if (SpawnRegion is { } spawn && spawn.Dimension == 0)
        {
            OutlineRegion(map, spawn.MinChunkX, spawn.MinChunkZ, spawn.MaxChunkX + 1, spawn.MaxChunkZ + 1, ClaimVisualPalette.Spawn, 56);
        }
        if (fullMap is null) return;
        foreach (var c in selection.Columns) Outline(map, c, ClaimVisualPalette.Selection, 58);
        foreach (var c in releaseSelection.Columns) Outline(map, c, ClaimVisualPalette.RemovalSelection, 59);
        if (!PopupBusy && hovered is { } h && !selection.Columns.Contains(h))
            Outline(map, h, ColorUtil.ToRgba(210, 255, 225, 70), 57);
    }
    private static Vec3d WorldAt(GuiElementMap map, int x, int y)
    {
        var world = new Vec3d();
        map.TranslateViewPosToWorldPos(new Vec2f((float)(x-map.Bounds.renderX), (float)(y-map.Bounds.renderY)), ref world);
        return world;
    }

    private bool IsWaypointAt(GuiElementMap map, int x, int y)
    {
        var layer = ((ICoreClientAPI)api).ModLoader.GetModSystem<WorldMapManager>().MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();
        if (layer is null) return false;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var waypointCollections = layer.GetType().GetFields(flags)
            .Where(field => field.Name.Contains("waypoint", StringComparison.OrdinalIgnoreCase) && typeof(System.Collections.IEnumerable).IsAssignableFrom(field.FieldType))
            .Select(field => field.GetValue(layer) as System.Collections.IEnumerable)
            .Concat(layer.GetType().GetProperties(flags)
                .Where(property => property.Name.Contains("waypoint", StringComparison.OrdinalIgnoreCase) && typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType))
                .Select(property => property.GetValue(layer) as System.Collections.IEnumerable)
                )
            .Where(collection => collection is not null)
            .Cast<System.Collections.IEnumerable>();
        foreach (var waypointCollection in waypointCollections)
        {
            foreach (var waypoint in waypointCollection)
            {
                if (waypoint is null) continue;
                var type = waypoint.GetType();
                var rawPosition = type.GetProperty("Position", flags)?.GetValue(waypoint)
                    ?? type.GetProperty("Pos", flags)?.GetValue(waypoint)
                    ?? type.GetField("Position", flags)?.GetValue(waypoint)
                    ?? type.GetField("Pos", flags)?.GetValue(waypoint);
                var position = rawPosition as Vec3d;
                if (position is null && rawPosition is BlockPos blockPos) position = new Vec3d(blockPos.X, blockPos.Y, blockPos.Z);
                if (position is null) continue;
                var view = new Vec2f();
                map.TranslateWorldPosToViewPos(position, ref view);
                var dx = x - (view.X + map.Bounds.renderX);
                var dy = y - (view.Y + map.Bounds.renderY);
                if (dx * dx + dy * dy <= 196) return true;
            }
        }
        return false;
    }
    private static ChunkColumn Column(Vec3d world) => new(0, ClaimGeometry.ToCell((int)Math.Floor(world.X)), ClaimGeometry.ToCell((int)Math.Floor(world.Z)));
    private static void Outline(GuiElementMap map, ChunkColumn c, int color, float depth)
    {
        OutlineRegion(map, c.X, c.Z, c.X + 1, c.Z + 1, color, depth);
    }
    private static bool BelongsToProperty(ChunkColumn column, ClaimMapProperty property) =>
        claimVisuals.TryGetValue(column, out var found) && found.Id == property.Id;
    private static void OutlineClaimBoundary(GuiElementMap map, ChunkColumn column, ClaimMapProperty property, int color, float depth)
    {
        var x = ClaimGeometry.ToWorld(column.X);
        var z = ClaimGeometry.ToWorld(column.Z);
        var size = ClaimGeometry.CellSize;
        if (!BelongsToProperty(column with { X = column.X - 1 }, property)) OutlineEdge(map, x, z, x, z + size, color, depth);
        if (!BelongsToProperty(column with { X = column.X + 1 }, property)) OutlineEdge(map, x + size, z, x + size, z + size, color, depth);
        if (!BelongsToProperty(column with { Z = column.Z - 1 }, property)) OutlineEdge(map, x, z, x + size, z, color, depth);
        if (!BelongsToProperty(column with { Z = column.Z + 1 }, property)) OutlineEdge(map, x, z + size, x + size, z + size, color, depth);
    }
    private static void OutlineEdge(GuiElementMap map, int x1, int z1, int x2, int z2, int color, float depth)
    {
        var a = new Vec2f(); var b = new Vec2f();
        map.TranslateWorldPosToViewPos(new Vec3d(x1, 0, z1), ref a);
        map.TranslateWorldPosToViewPos(new Vec3d(x2, 0, z2), ref b);
        var ax = (float)map.Bounds.renderX + a.X;
        var ay = (float)map.Bounds.renderY + a.Y;
        var bx = (float)map.Bounds.renderX + b.X;
        var by = (float)map.Bounds.renderY + b.Y;
        if (Math.Abs(bx - ax) >= Math.Abs(by - ay))
            map.Api.Render.RenderRectangle(Math.Min(ax, bx), Math.Min(ay, by), depth, Math.Max(1, Math.Abs(bx - ax)), 0, color);
        else
            map.Api.Render.RenderRectangle(Math.Min(ax, bx), Math.Min(ay, by), depth, 0, Math.Max(1, Math.Abs(by - ay)), color);
    }
    private static void OutlineRegion(GuiElementMap map, int minChunkX, int minChunkZ, int maxChunkXExclusive, int maxChunkZExclusive, int color, float depth)
    {
        var a = new Vec2f(); var b = new Vec2f();
        map.TranslateWorldPosToViewPos(new Vec3d(ClaimGeometry.ToWorld(minChunkX), 0, ClaimGeometry.ToWorld(minChunkZ)), ref a);
        map.TranslateWorldPosToViewPos(new Vec3d(ClaimGeometry.ToWorld(maxChunkXExclusive), 0, ClaimGeometry.ToWorld(maxChunkZExclusive)), ref b);
        map.Api.Render.RenderRectangle((float)map.Bounds.renderX+a.X, (float)map.Bounds.renderY+a.Y, depth, b.X-a.X, b.Y-a.Y, color);
    }
    public override void Dispose()
    {
        if (ReferenceEquals(Instance, this))
        {
            if (explorationDirty && api is ICoreClientAPI clientApi)
                clientApi.StoreModConfig(new ClientExplorationState { Sectors = exploredSectors.ToList() }, "mathclaims-exploration.json");
            Instance = null; PopupBusy = false; claimVisuals.Clear(); worldSpatialIndex.Clear(); SpawnRegion = null; showClaimsOnMinimap = true; showClaimsInUndiscoveredAreas = false; OnContextRequested = null; OnPropertyRequested = null; OnReleaseRequested = null;
        }
        base.Dispose();
    }
    private static int FloorDiv(int value, int divisor) => value >= 0 ? value / divisor : (value - divisor + 1) / divisor;
}

public sealed record ClaimMapProperty(string Id, string Name, string OwnerName, int ChunkCount, OverlayColor Color, bool CanManage, bool CanAdminister, bool PvpEnabled)
{
    public string AccessLabel => Color switch
    {
        OverlayColor.Green => "Acesso: proprietário",
        OverlayColor.Yellow => "Acesso: autorizado",
        _ => "Acesso: sem permissões"
    };
}

public sealed record SpawnVisualRegion(int Dimension, int MinChunkX, int MaxChunkX, int MinChunkZ, int MaxChunkZ);
