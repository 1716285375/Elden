using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    /// <summary>
    /// Authored source of truth for the LV01 greybox geometry.
    /// </summary>
    /// <remarks>
    /// Every dimension below is expressed in metres at 1 unit = 1 metre and then
    /// multiplied by <see cref="Scale"/>, which is derived from the real Player
    /// prefab rather than an assumed 1.8 m human.
    /// Rebuild the layout asset from this spec, then hand-tweak individual boxes in
    /// the Inspector and regenerate: the layout asset, not this file, drives output.
    /// </remarks>
    public static class LV01GreyboxSpec
    {
        // ---------------------------------------------------------------------
        // Measured from the real Player prefab, not assumed.
        // Player.prefab: CharacterController height 2, radius 0.35, centre y 1.
        // PlayerCamera: pivot height 1.65, follow distance 2.5, occlusion probe 0.2.
        // ---------------------------------------------------------------------
        public const float PlayerHeight = 2f;
        public const float PlayerRadius = 0.35f;
        public const float CameraPivotHeight = 1.65f;
        public const float CameraDistance = 2.5f;
        public const float ReferenceHeight = 1.8f;

        /// <summary>Design-table multiplier: real player height over reference height.</summary>
        public const float Scale = PlayerHeight / ReferenceHeight;

        // ---------------------------------------------------------------------
        // Design table (spec section 4) already multiplied by Scale.
        // ---------------------------------------------------------------------
        public const float DoorWidth = 1.8f * Scale;
        public const float DoorHeight = 2.8f * Scale;
        public const float GateWidth = 6f * Scale;
        public const float GateHeight = 6f * Scale;
        public const float CurtainWallHeight = 12f * Scale;
        public const float CorridorWidth = 4f * Scale;
        public const float CombatRoadWidth = 7f * Scale;
        public const float SmallCombatSize = 14f * Scale;
        public const float MediumCombatSize = 24f * Scale;
        public const float IndoorCeiling = 5f * Scale;
        public const float HallCeiling = 8f * Scale;
        public const float CoverHeight = 1.2f * Scale;
        public const float RailingHeight = 1.1f * Scale;
        public const float StairWidth = 2.5f * Scale;

        // ---------------------------------------------------------------------
        // Structural constants. StepHeight stays under the controller's 0.3
        // stepOffset so every generated stair is walkable without jumping.
        // ---------------------------------------------------------------------
        public const float StepHeight = 0.25f;
        public const float RoadThickness = 1f;
        public const float SlabThickness = 1f;
        public const float WallThickness = 1.5f;
        public const float RailingThickness = 0.3f;
        public const float ColumnSize = 1f;
        public const float CliffDropDepth = 30f;
        public const float MountainWallHeight = 8f;
        public const float PerimeterWallHeight = 6f;

        /// <summary>
        /// Road spans are lengthened at both ends by this multiple of their width so
        /// consecutive spans overlap enough to seal the wedge that opens at a turn.
        /// Must exceed 1.0 or outside corners develop holes.
        /// </summary>
        public const float CornerExtension = 1.2f;

        // ---------------------------------------------------------------------
        // Areas. Region index matches WorldScenePathLayout region folders.
        // ---------------------------------------------------------------------
        public const int RegionOutskirts = 0;
        public const int RegionInterior = 1;

        public const string CliffPath = "A01_CliffPath";
        public const string Graveyard = "A02_Graveyard";
        public const string MainGate = "A03_MainGate";
        public const string GateTower = "A04_GateTower";
        public const string EntranceHall = "A01_EntranceHall";
        public const string Cloister = "A02_Cloister";

        public const string Base = "Base";
        public const string Props = "Props";
        public const string Effects = "Effects";
        public const string Spawners = "Spawners";

        // ---------------------------------------------------------------------
        // Key positions shared with the Spawners pass so geometry and gameplay
        // can never drift apart.
        // ---------------------------------------------------------------------
        public static readonly Vector3 PlayerSpawn = new(0f, 1f, 0f);
        public static readonly Vector3 CliffEnemyOne = new(-2f, 1.5f, 22f);
        public static readonly Vector3 CliffEnemyTwo = new(9f, 3f, 40f);
        public static readonly Vector3 GraveyardPatrolOne = new(12f, 5f, 70f);
        public static readonly Vector3 GraveyardPatrolTwo = new(20f, 5f, 74f);
        public static readonly Vector3 GraveyardArcher = new(26f, 8f, 78f);
        public static readonly Vector3 GraveyardLootAlcove = new(2f, 5f, 75f);
        public static readonly Vector3 GateTowerGuard = new(24f, 11f, 112f);
        public static readonly Vector3 GateTowerLever = new(26f, 17f, 110f);
        public static readonly Vector3 HallEnemy = new(0f, 9f, 130f);
        public static readonly Vector3 CloisterCheckpoint = new(0f, 10f, 158f);

        /// <summary>Centre of the Main Gate opening, used to place the gate prefab.</summary>
        public static readonly Vector3 GateOpeningCentre = new(0f, 10.34f, 108f);

        // Graveyard footprint: x -7..39, z 54..94, floor top y 5.
        private const float k_GraveyardMinX = -7f;
        private const float k_GraveyardMaxX = 39f;
        private const float k_GraveyardMinZ = 54f;
        private const float k_GraveyardMaxZ = 94f;
        private const float k_GraveyardFloorY = 5f;

        // Main Gate: ground y 7, curtain wall at z 108.
        private const float k_GateGroundY = 7f;
        private const float k_GateZ = 108f;
        private const float k_GateWallThickness = 2.2f;

        // Gate Tower footprint: x 16..32, z 100..116.
        private const float k_TowerMinX = 16f;
        private const float k_TowerMaxX = 32f;
        private const float k_TowerMinZ = 100f;
        private const float k_TowerMaxZ = 116f;
        private const float k_TowerFloorY = 11f;
        private const float k_TowerUpperY = 17f;

        // Entrance Hall: x -10..10, z 110..141, floor y 9.
        private const float k_HallMinX = -10f;
        private const float k_HallMaxX = 10f;
        private const float k_HallMinZ = 110f;
        private const float k_HallMaxZ = 141f;
        private const float k_HallFloorY = 9f;

        // Cloister: x -25.5..25.5, z 146.5..193.5, floor y 10.
        private const float k_CloisterMinX = -25.5f;
        private const float k_CloisterMaxX = 25.5f;
        private const float k_CloisterMinZ = 146.5f;
        private const float k_CloisterMaxZ = 193.5f;
        private const float k_CloisterFloorY = 10f;

        /// <summary>Builds the complete geometry set for every Area and Slice.</summary>
        public static List<GreyboxBox> Build()
        {
            Builder builder = new();
            builder.AddCliffPath();
            builder.AddGraveyard();
            builder.AddMainGate();
            builder.AddGateTower();
            builder.AddEntranceHall();
            builder.AddCloister();
            return builder.Boxes;
        }

        /// <summary>Accumulates boxes and holds the geometry helpers the Areas are written in terms of.</summary>
        private sealed class Builder
        {
            private readonly List<GreyboxBox> m_boxes = new();

            public List<GreyboxBox> Boxes => m_boxes;

            // ---- Primitives -------------------------------------------------

            /// <summary>Adds one axis-aligned box from an explicit centre.</summary>
            public void Box(
                int region,
                string area,
                string slice,
                string objectName,
                GreyboxRole role,
                Vector3 centre,
                Vector3 size,
                string purpose)
            {
                m_boxes.Add(new GreyboxBox(
                    region, area, slice, objectName, role,
                    centre, Vector3.zero, size, purpose));
            }

            /// <summary>Adds a horizontal floor plate whose top surface sits at <paramref name="surfaceY"/>.</summary>
            public void Floor(
                int region,
                string area,
                string slice,
                string objectName,
                Vector3 surfaceCentre,
                float sizeX,
                float sizeZ,
                string purpose,
                float thickness = SlabThickness,
                GreyboxRole role = GreyboxRole.Walkable)
            {
                Vector3 centre = new(
                    surfaceCentre.x,
                    surfaceCentre.y - thickness * 0.5f,
                    surfaceCentre.z);
                Box(region, area, slice, objectName, role, centre,
                    new Vector3(sizeX, thickness, sizeZ), purpose);
            }

            /// <summary>Adds a wall standing on <paramref name="baseCentre"/>.y and growing upward.</summary>
            public void Wall(
                int region,
                string area,
                string slice,
                string objectName,
                Vector3 baseCentre,
                float sizeX,
                float height,
                float sizeZ,
                string purpose,
                GreyboxRole role = GreyboxRole.Blocking)
            {
                Vector3 centre = new(baseCentre.x, baseCentre.y + height * 0.5f, baseCentre.z);
                Box(region, area, slice, objectName, role, centre,
                    new Vector3(sizeX, height, sizeZ), purpose);
            }

            /// <summary>
            /// Adds a box whose local +Z axis runs from <paramref name="from"/> to
            /// <paramref name="to"/>, whose top face sits <paramref name="height"/>
            /// above that line, whose bottom face sits <paramref name="depth"/> below
            /// it, and which is pushed sideways by <paramref name="lateral"/>.
            /// This is the primitive every road, wall run and stair tread is built from.
            /// </summary>
            public void Beam(
                int region,
                string area,
                string slice,
                string objectName,
                Vector3 from,
                Vector3 to,
                float width,
                float height,
                float depth,
                float lateral,
                GreyboxRole role,
                string purpose)
            {
                Vector3 delta = to - from;
                float run = new Vector3(delta.x, 0f, delta.z).magnitude;
                if (run < 0.0001f && Mathf.Abs(delta.y) < 0.0001f)
                {
                    return;
                }

                float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(delta.y, run) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

                Vector3 right = rotation * Vector3.right;
                Vector3 up = rotation * Vector3.up;
                Vector3 centre = (from + to) * 0.5f +
                    right * lateral +
                    up * ((height - depth) * 0.5f);
                Vector3 size = new(width, height + depth, delta.magnitude);

                m_boxes.Add(new GreyboxBox(
                    region, area, slice, objectName, role,
                    centre, rotation.eulerAngles, size, purpose));
            }

            // ---- Roads and stairs -------------------------------------------

            /// <summary>
            /// Adds a walkable span between two nodes. A level span becomes one flat
            /// slab; a rising span becomes solid step boxes whose shared base keeps
            /// the staircase closed from every angle.
            /// </summary>
            public void PathSpan(
                int region,
                string area,
                string slice,
                string objectName,
                Vector3 from,
                Vector3 to,
                float width,
                PathSides sides,
                string purpose)
            {
                Vector3 horizontal = to - from;
                horizontal.y = 0f;
                float run = horizontal.magnitude;
                if (run < 0.0001f)
                {
                    return;
                }

                Vector3 direction = horizontal / run;
                float extension = width * CornerExtension;

                if (Mathf.Abs(to.y - from.y) < 0.001f)
                {
                    Beam(region, area, slice, objectName + "_Road",
                        from - direction * extension, to + direction * extension,
                        width, 0f, RoadThickness, 0f, GreyboxRole.Walkable, purpose);
                }
                else
                {
                    AddStair(region, area, slice, objectName, from, to, width,
                        direction, run, extension, purpose);
                }

                float wallOffset = width * 0.5f + WallThickness * 0.5f;
                float lipOffset = width * 0.5f + RailingThickness * 0.5f;
                float cliffOffset = width * 0.5f + WallThickness * 0.5f + 0.4f;

                if ((sides & PathSides.Mountain) != 0)
                {
                    Beam(region, area, slice, objectName + "_MountainWall",
                        from - direction * extension, to + direction * extension,
                        WallThickness, MountainWallHeight, RoadThickness, wallOffset,
                        GreyboxRole.Blocking, "Cliff face sheltering the path");
                }

                if ((sides & PathSides.Drop) != 0)
                {
                    Beam(region, area, slice, objectName + "_DropLip",
                        from - direction * extension, to + direction * extension,
                        RailingThickness, CoverHeight, RoadThickness, -lipOffset,
                        GreyboxRole.Cover, "Low lip: see over the drop, cannot walk off");

                    Beam(region, area, slice, objectName + "_CliffFace",
                        from - direction * extension, to + direction * extension,
                        WallThickness, 0f, CliffDropDepth, -cliffOffset,
                        GreyboxRole.Blocking, "Cliff face below the drop lip");
                }
            }

            private void AddStair(
                int region,
                string area,
                string slice,
                string objectName,
                Vector3 from,
                Vector3 to,
                float width,
                Vector3 direction,
                float run,
                float extension,
                string purpose)
            {
                float rise = to.y - from.y;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(rise) / StepHeight));
                float treadDepth = run / steps;
                float stepRise = rise / steps;

                for (int i = 0; i < steps; i++)
                {
                    float start = i == 0 ? -extension : i * treadDepth;
                    float end = i == steps - 1 ? run + extension : (i + 1) * treadDepth;
                    float treadTop = from.y + stepRise * (i + 1);

                    Vector3 stepFrom = from + direction * start;
                    Vector3 stepTo = from + direction * end;
                    stepFrom.y = treadTop;
                    stepTo.y = treadTop;

                    // Every tread reaches down to the same base so the flight is solid,
                    // whichever way the flight climbs.
                    float depth = Mathf.Abs(treadTop - from.y) + RoadThickness;
                    Beam(region, area, slice, $"{objectName}_Step_{i:00}",
                        stepFrom, stepTo, width, 0f, depth, 0f,
                        GreyboxRole.Walkable, purpose);
                }
            }

            /// <summary>
            /// Adds a see-over parapet along one horizontal edge of a platform.
            /// Edges are named after the compass direction they face.
            /// </summary>
            public void Parapet(
                int region,
                string area,
                string slice,
                string objectName,
                ParapetEdge edge,
                float surfaceY,
                float fixedCoordinate,
                float from,
                float to,
                string purpose)
            {
                bool runsAlongX = edge is ParapetEdge.South or ParapetEdge.North;
                float length = to - from;
                Vector3 centre = runsAlongX
                    ? new Vector3((from + to) * 0.5f, surfaceY, fixedCoordinate)
                    : new Vector3(fixedCoordinate, surfaceY, (from + to) * 0.5f);
                Vector3 size = runsAlongX
                    ? new Vector3(length, RailingHeight, RailingThickness)
                    : new Vector3(RailingThickness, RailingHeight, length);
                Wall(region, area, slice, objectName, centre, size.x, size.y, size.z,
                    purpose, GreyboxRole.Cover);
            }

            // ---- Areas -------------------------------------------------------

            /// <summary>R01 A01: the teaching path. Spawn to Graveyard, 0 to +5 m.</summary>
            public void AddCliffPath()
            {
                int region = RegionOutskirts;
                string area = CliffPath;
                float width = CombatRoadWidth;
                PathSides sides = PathSides.Mountain | PathSides.Drop;

                // Non-linear route: the player drifts right and climbs, so the
                // monastery only reveals itself at the corner.
                PathSpan(region, area, Base, "PB_A01_SpawnToVista",
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 12f),
                    width, sides, "Teach move and camera: straight, safe, no threats");

                PathSpan(region, area, Base, "PB_A01_VistaToEnemyOne",
                    new Vector3(0f, 0f, 12f), CliffEnemyOne,
                    width, sides, "First climb, first 1v1 arena");

                PathSpan(region, area, Base, "PB_A01_EnemyOneToCorner",
                    CliffEnemyOne, new Vector3(6f, 1.5f, 32f),
                    width, sides, "Turn reveals the monastery landmark");

                PathSpan(region, area, Base, "PB_A01_CornerToEnemyTwo",
                    new Vector3(6f, 1.5f, 32f), CliffEnemyTwo,
                    width, sides, "Second climb into the light ambush");

                PathSpan(region, area, Base, "PB_A01_EnemyTwoToGraveyard",
                    CliffEnemyTwo, new Vector3(12f, 5f, 55f),
                    width, sides, "Final climb, opens onto the graveyard");

                // Props: dead trees and boulders that break up the sightline.
                Prop(region, area, "PB_A01_Prop_DeadTree_00",
                    new Vector3(4.6f, 0f, 6f), new Vector3(0.6f, 6f, 0.6f), "Dead tree silhouette");
                Prop(region, area, "PB_A01_Prop_DeadTree_01",
                    new Vector3(2.4f, 1.5f, 27f), new Vector3(0.6f, 5f, 0.6f), "Dead tree silhouette");
                Prop(region, area, "PB_A01_Prop_DeadTree_02",
                    new Vector3(10.8f, 3f, 46f), new Vector3(0.6f, 5f, 0.6f), "Dead tree silhouette");
                Prop(region, area, "PB_A01_Prop_Boulder_00",
                    new Vector3(-4.4f, 0f, 17f), new Vector3(1.6f, 1.2f, 1.6f), "Boulder cover");
                Prop(region, area, "PB_A01_Prop_Boulder_01",
                    new Vector3(11.5f, 1.5f, 36f), new Vector3(1.9f, 1.4f, 1.9f), "Boulder cover");
                Prop(region, area, "PB_A01_Prop_BrokenCart_00",
                    new Vector3(-1.2f, 0f, 9f), new Vector3(1.8f, 1f, 2.6f), "Broken cart waymark");

                Fog(region, area, new Vector3(0f, 4f, 28f), new Vector3(40f, 20f, 70f),
                    "Damp valley fog, hides the graveyard until the corner");
                LightPlaceholder(region, area, new Vector3(6f, 4f, 32f), "Warm rim light on the corner vista");
            }

            /// <summary>R01 A02: the first real combat space, with a raised archer and a hidden loot branch.</summary>
            public void AddGraveyard()
            {
                int region = RegionOutskirts;
                string area = Graveyard;
                float y = k_GraveyardFloorY;

                Floor(region, area, Base, "PB_A02_MainFloor",
                    new Vector3(16f, y, 74f), 46f, 40f,
                    "Primary combat floor: two patrols plus a raised archer");

                // Perimeter. North and south walls carry the two gateways.
                Wall(region, area, Base, "PB_A02_Wall_W",
                    new Vector3(k_GraveyardMinX, y, 74f), WallThickness,
                    PerimeterWallHeight, 40f, "West perimeter");
                Wall(region, area, Base, "PB_A02_Wall_E",
                    new Vector3(k_GraveyardMaxX, y, 74f), WallThickness,
                    PerimeterWallHeight, 40f, "East perimeter");

                Wall(region, area, Base, "PB_A02_Wall_NA",
                    new Vector3(0.5f, y, k_GraveyardMinZ), 15f,
                    PerimeterWallHeight, WallThickness, "North wall, west of the entrance gap");
                Wall(region, area, Base, "PB_A02_Wall_NB",
                    new Vector3(27.5f, y, k_GraveyardMinZ), 23f,
                    PerimeterWallHeight, WallThickness, "North wall, east of the entrance gap");

                Wall(region, area, Base, "PB_A02_Wall_SA",
                    new Vector3(-3.5f, y, k_GraveyardMaxZ), 7f,
                    PerimeterWallHeight, WallThickness, "South wall, west of the gate approach gap");
                Wall(region, area, Base, "PB_A02_Wall_SB",
                    new Vector3(15f, y, k_GraveyardMaxZ), 10f,
                    PerimeterWallHeight, WallThickness, "South wall between the two gateways");
                Wall(region, area, Base, "PB_A02_Wall_SC",
                    new Vector3(32.5f, y, k_GraveyardMaxZ), 13f,
                    PerimeterWallHeight, WallThickness, "South wall, east of the tower path gap");

                // Raised archer platform, deliberately reachable so the ranged
                // threat never becomes an unanswerable problem.
                Box(region, area, Base, "PB_A02_ArcherPlatform", GreyboxRole.Walkable,
                    new Vector3(25f, y + 1.5f, 78f), new Vector3(10f, 3f, 8f),
                    "Raised platform for the archer, y 8");
                PathSpan(region, area, Base, "PB_A02_ArcherStair",
                    new Vector3(25f, y, 66f), new Vector3(25f, y + 3f, 74f),
                    CorridorWidth, PathSides.None, "Climb to the archer platform");

                // The south parapet is split so the stair actually reaches the platform.
                float archerTop = y + 3f;
                float stairHalf = CorridorWidth * 0.5f + 0.3f;
                Parapet(region, area, Base, "PB_A02_ArcherParapet_N", ParapetEdge.North,
                    archerTop, 82f, 20f, 30f, "Archer platform parapet");
                Parapet(region, area, Base, "PB_A02_ArcherParapet_E", ParapetEdge.East,
                    archerTop, 30f, 74f, 82f, "Archer platform parapet");
                Parapet(region, area, Base, "PB_A02_ArcherParapet_W", ParapetEdge.West,
                    archerTop, 20f, 74f, 82f, "Archer platform parapet");
                Parapet(region, area, Base, "PB_A02_ArcherParapet_SW", ParapetEdge.South,
                    archerTop, 74f, 20f, 25f - stairHalf, "Archer platform parapet");
                Parapet(region, area, Base, "PB_A02_ArcherParapet_SE", ParapetEdge.South,
                    archerTop, 74f, 25f + stairHalf, 30f, "Archer platform parapet");

                // Cover: enough to break sightlines without hiding the patrols.
                Cover(region, area, "PB_A02_Cover_00", new Vector3(10f, y, 76f), 4f, 0f);
                Cover(region, area, "PB_A02_Cover_01", new Vector3(18f, y, 68f), 5f, 30f);
                Cover(region, area, "PB_A02_Cover_02", new Vector3(22f, y, 72f), 4f, 90f);
                Cover(region, area, "PB_A02_Cover_03", new Vector3(14f, y, 80f), 4.5f, 0f);

                // Approach to the Main Gate: narrows and climbs, framing the gate.
                PathSpan(region, area, Base, "PB_A02_GateApproach",
                    new Vector3(5f, y, 94f), new Vector3(5f, k_GateGroundY, 105f),
                    5.6f, PathSides.Mountain | PathSides.Drop,
                    "Funnels the player toward the closed gate");

                // Side path east to the Gate Tower: the alternative route the player
                // must discover once the gate reads as locked.
                PathSpan(region, area, Base, "PB_A02_TowerSidePath",
                    new Vector3(23f, y, 94f), new Vector3(23f, y, 100f),
                    5.6f, PathSides.Mountain | PathSides.Drop,
                    "Hidden side path toward the Gate Tower");

                AddGraveyardProps(region, area, y);

                Fog(region, area, new Vector3(16f, 5f, 80f), new Vector3(50f, 14f, 46f),
                    "Low ground fog across the graves");
                LightPlaceholder(region, area, new Vector3(16f, 6f, 72f), "Cold moonlight on the central fight");
                LightPlaceholder(region, area, new Vector3(2f, 6f, 75f), "Faint warm light marking the loot branch");
            }

            private void AddGraveyardProps(int region, string area, float y)
            {
                // Gravestones in rows: they read as cover, but the fight happens in
                // the open ground the player first sees from the entrance.
                float[] rowsX = { -4f, -1f, 2f, 5f, 8f };
                float[] rowsZ = { 60f, 64f, 68f, 86f, 90f };
                int index = 0;
                foreach (float z in rowsZ)
                {
                    foreach (float x in rowsX)
                    {
                        float lean = (index % 3 - 1) * 3f;
                        Prop(region, area, $"PB_A02_Prop_Grave_{index:00}",
                            new Vector3(x, y + 0.8f, z),
                            new Vector3(0.9f, 1.6f, 0.25f),
                            "Gravestone: partial cover and sightline break");
                        index++;
                    }
                }

                // The loot branch is walled by denser stones so it is found after
                // the fight rather than before it.
                for (int i = 0; i < 5; i++)
                {
                    Prop(region, area, $"PB_A02_Prop_Alcove_{i:00}",
                        new Vector3(-1f + i * 0.9f, y + 0.9f, 73f + (i % 2) * 2.4f),
                        new Vector3(0.9f, 1.8f, 0.3f),
                        "Dense stones hiding the loot alcove");
                }

                Prop(region, area, "PB_A02_Prop_BrokenColumn_00",
                    new Vector3(30f, y + 1.4f, 66f), new Vector3(0.9f, 2.8f, 0.9f),
                    "Broken column landmark by the east wall");
                Prop(region, area, "PB_A02_Prop_Statue_00",
                    new Vector3(16f, y + 1.6f, 84f), new Vector3(1.4f, 3.2f, 1.4f),
                    "Weathered statue facing the gate");
                Prop(region, area, "PB_A02_Prop_Cart_00",
                    new Vector3(34f, y + 0.7f, 88f), new Vector3(2.2f, 1.4f, 3.4f),
                    "Collapsed cart near the tower path");
            }

            /// <summary>R01 A03: the locked gate. Reads as a dead end that demands another route.</summary>
            public void AddMainGate()
            {
                int region = RegionOutskirts;
                string area = MainGate;
                float y = k_GateGroundY;
                float halfSpan = 16.65f;
                float halfOpening = GateWidth * 0.5f;

                // Landing in front of the gate, wide enough to turn and fight.
                Floor(region, area, Base, "PB_A03_GateLanding",
                    new Vector3(3f, y, 110.5f), 22f, 11f,
                    "Turnaround in front of the gate");

                float sideWidth = halfSpan - halfOpening;
                float sideCentre = halfOpening + sideWidth * 0.5f;

                Wall(region, area, Base, "PB_A03_Wall_W",
                    new Vector3(-sideCentre, y, k_GateZ), sideWidth,
                    CurtainWallHeight, k_GateWallThickness, "Curtain wall, west of the gate");
                Wall(region, area, Base, "PB_A03_Wall_E",
                    new Vector3(sideCentre, y, k_GateZ), sideWidth,
                    CurtainWallHeight, k_GateWallThickness, "Curtain wall, east of the gate");

                // Return walls frame the approach so the gate fills the view.
                Wall(region, area, Base, "PB_A03_Return_W",
                    new Vector3(-8f, y, 104f), WallThickness, MountainWallHeight, 4f,
                    "Return wall framing the approach");
                Wall(region, area, Base, "PB_A03_Return_E",
                    new Vector3(14f, y, 104f), WallThickness, MountainWallHeight, 4f,
                    "Return wall framing the approach");

                // Landing behind the gate: the monastery side of the threshold.
                Floor(region, area, Base, "PB_A03_InnerLanding",
                    new Vector3(0f, y, 112f), 16f, 8f,
                    "Threshold on the monastery side");

                // The opening itself is left void. The Lever Gate prefab fills it in
                // the Spawners slice so the closed/open state is real gameplay.
                Prop(region, area, "PB_A03_Prop_Rubble_00",
                    new Vector3(-6f, y + 0.4f, 106f), new Vector3(1.6f, 0.8f, 1.2f),
                    "Rubble at the gate foot");
                Prop(region, area, "PB_A03_Prop_Rubble_01",
                    new Vector3(6.4f, y + 0.3f, 106.4f), new Vector3(1.2f, 0.6f, 1.4f),
                    "Rubble at the gate foot");

                Fog(region, area, new Vector3(0f, 8f, 112f), new Vector3(30f, 16f, 16f),
                    "Fog spilling from the monastery interior");
                LightPlaceholder(region, area, new Vector3(0f, 12f, 112f), "Warm light leaking through the gate");
            }

            /// <summary>R01 A04: the vertical detour and the first shortcut loop.</summary>
            public void AddGateTower()
            {
                int region = RegionOutskirts;
                string area = GateTower;

                // Solid base: ground y 5 up to the tower floor at y 11.
                Box(region, area, Base, "PB_A04_TowerBase", GreyboxRole.Walkable,
                    new Vector3(24f, 8f, 108f), new Vector3(16f, 6f, 16f),
                    "Tower mass, ground y 5 to floor y 11");

                // Upper shaft: floor y 11 up to the lever platform at y 17.
                Box(region, area, Base, "PB_A04_TowerShaft", GreyboxRole.Walkable,
                    new Vector3(26f, 14f, 110f), new Vector3(8f, 6f, 8f),
                    "Upper shaft, floor y 11 to lever level y 17");

                // South parapet stops short of the first flight's landing, otherwise
                // the stair would deliver the player into a waist-high wall.
                Parapet(region, area, Base, "PB_A04_FloorParapet_N", ParapetEdge.North,
                    k_TowerFloorY, k_TowerMaxZ, k_TowerMinX, k_TowerMaxX,
                    "Tower floor parapet");
                Parapet(region, area, Base, "PB_A04_FloorParapet_W", ParapetEdge.West,
                    k_TowerFloorY, k_TowerMinX, k_TowerMinZ, k_TowerMaxZ,
                    "Tower floor parapet");
                Parapet(region, area, Base, "PB_A04_FloorParapet_E", ParapetEdge.East,
                    k_TowerFloorY, k_TowerMaxX, k_TowerMinZ, k_TowerMaxZ,
                    "Tower floor parapet");
                Parapet(region, area, Base, "PB_A04_FloorParapet_S", ParapetEdge.South,
                    k_TowerFloorY, k_TowerMinZ, 20.4f, k_TowerMaxX,
                    "Tower floor parapet, gated for the stair landing");

                // West parapet stops short of the bridge onto the lever platform.
                Parapet(region, area, Base, "PB_A04_UpperParapet_N", ParapetEdge.North,
                    k_TowerUpperY, 114f, 22f, 30f, "Lever platform parapet");
                Parapet(region, area, Base, "PB_A04_UpperParapet_S", ParapetEdge.South,
                    k_TowerUpperY, 106f, 22f, 30f, "Lever platform parapet");
                Parapet(region, area, Base, "PB_A04_UpperParapet_E", ParapetEdge.East,
                    k_TowerUpperY, 30f, 106f, 114f, "Lever platform parapet");
                Parapet(region, area, Base, "PB_A04_UpperParapet_W", ParapetEdge.West,
                    k_TowerUpperY, 22f, 106f, 112f,
                    "Lever platform parapet, gated for the bridge");

                // Ground to tower floor: 24 treads, 0.58 m deep, running north.
                PathSpan(region, area, Base, "PB_A04_Stair_GroundToFloor",
                    new Vector3(18f, 5f, 85f), new Vector3(18f, k_TowerFloorY, 99f),
                    StairWidth, PathSides.Drop,
                    "First flight: graveyard level up to the tower floor");
                Floor(region, area, Base, "PB_A04_FloorLanding",
                    new Vector3(18f, k_TowerFloorY, 99.5f), 4f, 2f,
                    "Landing between the first flight and the tower floor");

                // Tower floor to lever level: 24 treads, 0.5 m deep, up the west strip.
                PathSpan(region, area, Base, "PB_A04_Stair_FloorToUpper",
                    new Vector3(19f, k_TowerFloorY, 102f),
                    new Vector3(19f, k_TowerUpperY, 114f),
                    StairWidth, PathSides.Drop,
                    "Second flight: tower floor up to the lever");
                Floor(region, area, Base, "PB_A04_UpperBridge",
                    new Vector3(20.5f, k_TowerUpperY, 114f), 3f, 3f,
                    "Bridge from the second flight onto the lever platform");

                // Shortcut reward: a drop from the tower floor back to gate level, so
                // the loop can be run again without re-climbing.
                Floor(region, area, Base, "PB_A04_ShortcutDropPad",
                    new Vector3(14f, k_GateGroundY, 112f), 6f, 8f,
                    "Shortcut drop pad, tower floor y 11 down to gate level y 7");

                Prop(region, area, "PB_A04_Prop_Crate_00",
                    new Vector3(21f, k_TowerFloorY + 0.5f, 102f),
                    new Vector3(1f, 1f, 1f), "Crate near the second flight");
                Prop(region, area, "PB_A04_Prop_Crate_01",
                    new Vector3(21.6f, k_TowerFloorY + 0.5f, 102.6f),
                    new Vector3(1f, 1f, 1f), "Stacked crate");
                Prop(region, area, "PB_A04_Prop_Brazier_00",
                    new Vector3(26f, k_TowerUpperY + 0.6f, 110f),
                    new Vector3(0.8f, 1.2f, 0.8f), "Brazier beside the lever");

                LightPlaceholder(region, area, new Vector3(26f, k_TowerUpperY + 1f, 110f),
                    "Brazier light: makes the lever readable from the stairs");
            }

            /// <summary>R02 A01: the pressure drop from tight outside to vast inside.</summary>
            public void AddEntranceHall()
            {
                int region = RegionInterior;
                string area = EntranceHall;
                float y = k_HallFloorY;
                float ceiling = y + HallCeiling;

                Floor(region, area, Base, "PB_R02A01_HallFloor",
                    new Vector3(0f, y, 125.5f),
                    k_HallMaxX - k_HallMinX, k_HallMaxZ - k_HallMinZ,
                    "Entrance hall floor");

                // Ramp up from the gate threshold into the hall.
                PathSpan(region, area, Base, "PB_R02A01_EntryRamp",
                    new Vector3(0f, k_GateGroundY, 111f), new Vector3(0f, y, 119f),
                    GateWidth, PathSides.None,
                    "Narrow entry throat rising from the gate");

                Wall(region, area, Base, "PB_R02A01_Wall_W",
                    new Vector3(k_HallMinX, y, 125.5f), WallThickness,
                    HallCeiling, k_HallMaxZ - k_HallMinZ, "West hall wall");
                Wall(region, area, Base, "PB_R02A01_Wall_E",
                    new Vector3(k_HallMaxX, y, 125.5f), WallThickness,
                    HallCeiling, k_HallMaxZ - k_HallMinZ, "East hall wall");

                // North wall with the gate opening; the lintel keeps the hall sealed.
                float sideWidth = 10f - GateWidth * 0.5f;
                Wall(region, area, Base, "PB_R02A01_Wall_NW",
                    new Vector3(-GateWidth * 0.5f - sideWidth * 0.5f, y, k_HallMinZ),
                    sideWidth, HallCeiling, WallThickness, "North wall west of the gate opening");
                Wall(region, area, Base, "PB_R02A01_Wall_NE",
                    new Vector3(GateWidth * 0.5f + sideWidth * 0.5f, y, k_HallMinZ),
                    sideWidth, HallCeiling, WallThickness, "North wall east of the gate opening");
                Wall(region, area, Base, "PB_R02A01_Lintel_N",
                    new Vector3(0f, k_GateGroundY + GateHeight, k_HallMinZ),
                    GateWidth, ceiling - (k_GateGroundY + GateHeight), WallThickness,
                    "Lintel above the gate opening");

                // South wall with a single door to the cloister corridor.
                float doorHalf = DoorWidth * 0.5f;
                float southSide = 10f - doorHalf;
                Wall(region, area, Base, "PB_R02A01_Wall_SW",
                    new Vector3(-doorHalf - southSide * 0.5f, y, k_HallMaxZ),
                    southSide, HallCeiling, WallThickness, "South wall west of the cloister door");
                Wall(region, area, Base, "PB_R02A01_Wall_SE",
                    new Vector3(doorHalf + southSide * 0.5f, y, k_HallMaxZ),
                    southSide, HallCeiling, WallThickness, "South wall east of the cloister door");
                Wall(region, area, Base, "PB_R02A01_Lintel_S",
                    new Vector3(0f, y + DoorHeight, k_HallMaxZ),
                    DoorWidth, ceiling - (y + DoorHeight), WallThickness,
                    "Lintel above the cloister door");

                Box(region, area, Base, "PB_R02A01_Ceiling", GreyboxRole.Blocking,
                    new Vector3(0f, ceiling + 0.5f, 125.5f),
                    new Vector3(21.5f, 1f, 32.5f), "Hall ceiling");

                // Columns: give the volume scale and give the camera something to
                // fight against so the player feels the room rather than a warehouse.
                AddColumn(region, area, "PB_R02A01_Column_00", -5f, 120f, y, HallCeiling);
                AddColumn(region, area, "PB_R02A01_Column_01", 5f, 120f, y, HallCeiling);
                AddColumn(region, area, "PB_R02A01_Column_02", -5f, 131f, y, HallCeiling);
                AddColumn(region, area, "PB_R02A01_Column_03", 5f, 131f, y, HallCeiling);

                // Corridor from the hall to the cloister, climbing one metre.
                PathSpan(region, area, Base, "PB_R02A01_CloisterCorridor",
                    new Vector3(0f, y, k_HallMaxZ),
                    new Vector3(0f, k_CloisterFloorY, k_CloisterMinZ),
                    CorridorWidth, PathSides.None,
                    "Short climb into the cloister");

                Prop(region, area, "PB_R02A01_Prop_Pew_00",
                    new Vector3(-7f, y + 0.5f, 124f), new Vector3(1f, 1f, 3f), "Toppled pew");
                Prop(region, area, "PB_R02A01_Prop_Pew_01",
                    new Vector3(7f, y + 0.5f, 128f), new Vector3(1f, 1f, 3f), "Toppled pew");
                Prop(region, area, "PB_R02A01_Prop_Crate_00",
                    new Vector3(-8f, y + 0.6f, 137f), new Vector3(1.2f, 1.2f, 1.2f), "Supply crate");

                Fog(region, area, new Vector3(0f, y + 4f, 125.5f), new Vector3(20f, 9f, 31f),
                    "Interior haze, thins toward the far end");
                LightPlaceholder(region, area, new Vector3(0f, y + 6f, 119f),
                    "Light shaft at the entry: pulls the player inside");
                LightPlaceholder(region, area, new Vector3(0f, y + 5f, 139f),
                    "Light at the cloister door: confirms the way on");
            }

            /// <summary>R02 A02: the first transport hub and the checkpoint.</summary>
            public void AddCloister()
            {
                int region = RegionInterior;
                string area = Cloister;
                float y = k_CloisterFloorY;
                float ceiling = y + IndoorCeiling;

                Floor(region, area, Base, "PB_R02A02_CloisterFloor",
                    new Vector3(0f, y, 170f),
                    k_CloisterMaxX - k_CloisterMinX, k_CloisterMaxZ - k_CloisterMinZ,
                    "Cloister floor, hub for every later branch");

                // Courtyard is open to the sky; the ambulatory wraps it.
                Floor(region, area, Base, "PB_R02A02_Courtyard",
                    new Vector3(0f, y, 170f), 22f, 20f,
                    "Open courtyard, 22 x 20");

                BuildCloisterWalls(region, area, y);
                BuildColonnade(region, area, y, ceiling);
                BuildCloisterRoof(region, area, ceiling);

                Prop(region, area, "PB_R02A02_Prop_Well_00",
                    new Vector3(0f, y + 0.5f, 170f), new Vector3(2.4f, 1f, 2.4f),
                    "Dry well: the courtyard centrepiece");
                Prop(region, area, "PB_R02A02_Prop_BrokenColumn_00",
                    new Vector3(-6f, y + 0.9f, 164f), new Vector3(0.8f, 1.8f, 0.8f), "Fallen column");
                Prop(region, area, "PB_R02A02_Prop_Statue_00",
                    new Vector3(6f, y + 1.7f, 176f), new Vector3(1.2f, 3.4f, 1.2f), "Saint statue");

                // North doorway from the corridor is cut in BuildCloisterWalls; the
                // remaining three sides stay solid until R03/R04 are built.
                Fog(region, area, new Vector3(0f, y + 3f, 170f), new Vector3(48f, 12f, 44f),
                    "Still courtyard air");
                LightPlaceholder(region, area, new Vector3(0f, y + 3f, 158f),
                    "Checkpoint light beacon, visible from the hall door");
            }

            private void BuildCloisterWalls(int region, string area, float y)
            {
                float depth = k_CloisterMaxZ - k_CloisterMinZ;
                float span = k_CloisterMaxX - k_CloisterMinX;
                float centreZ = (k_CloisterMinZ + k_CloisterMaxZ) * 0.5f;

                Wall(region, area, Base, "PB_R02A02_Wall_W",
                    new Vector3(k_CloisterMinX, y, centreZ), WallThickness,
                    IndoorCeiling, depth, "West cloister wall");
                Wall(region, area, Base, "PB_R02A02_Wall_E",
                    new Vector3(k_CloisterMaxX, y, centreZ), WallThickness,
                    IndoorCeiling, depth, "East cloister wall");
                Wall(region, area, Base, "PB_R02A02_Wall_S",
                    new Vector3(0f, y, k_CloisterMaxZ), span, IndoorCeiling,
                    WallThickness, "South cloister wall, sealed until R03");

                // North wall carries the single door from the hall corridor.
                float doorHalf = DoorWidth * 0.5f;
                float sideWidth = span * 0.5f - doorHalf;
                Wall(region, area, Base, "PB_R02A02_Wall_NW",
                    new Vector3(-doorHalf - sideWidth * 0.5f, y, k_CloisterMinZ),
                    sideWidth, IndoorCeiling, WallThickness, "North wall west of the hall door");
                Wall(region, area, Base, "PB_R02A02_Wall_NE",
                    new Vector3(doorHalf + sideWidth * 0.5f, y, k_CloisterMinZ),
                    sideWidth, IndoorCeiling, WallThickness, "North wall east of the hall door");
                Wall(region, area, Base, "PB_R02A02_Lintel_N",
                    new Vector3(0f, y + DoorHeight, k_CloisterMinZ),
                    DoorWidth, IndoorCeiling - DoorHeight, WallThickness,
                    "Lintel above the hall door");
            }

            private void BuildColonnade(int region, string area, float y, float ceiling)
            {
                float columnHeight = ceiling - y;
                float[] alongX = { -11f, -5.5f, 0f, 5.5f, 11f };
                float[] alongZ = { 165f, 170f, 175f };
                int index = 0;

                foreach (float x in alongX)
                {
                    AddColumn(region, area, $"PB_R02A02_Column_{index++:00}", x, 160f, y, columnHeight);
                    AddColumn(region, area, $"PB_R02A02_Column_{index++:00}", x, 180f, y, columnHeight);
                }

                foreach (float z in alongZ)
                {
                    AddColumn(region, area, $"PB_R02A02_Column_{index++:00}", -11f, z, y, columnHeight);
                    AddColumn(region, area, $"PB_R02A02_Column_{index++:00}", 11f, z, y, columnHeight);
                }
            }

            private void BuildCloisterRoof(int region, string area, float ceiling)
            {
                // The ambulatory is roofed; the courtyard stays open to the sky.
                float westWidth = 11f - k_CloisterMinX;
                float eastWidth = k_CloisterMaxX - 11f;
                float northDepth = 160f - k_CloisterMinZ;
                float southDepth = k_CloisterMaxZ - 180f;

                Box(region, area, Base, "PB_R02A02_Roof_W", GreyboxRole.Blocking,
                    new Vector3(k_CloisterMinX + westWidth * 0.5f, ceiling + 0.5f, 170f),
                    new Vector3(westWidth, 1f, k_CloisterMaxZ - k_CloisterMinZ),
                    "West ambulatory roof");
                Box(region, area, Base, "PB_R02A02_Roof_E", GreyboxRole.Blocking,
                    new Vector3(11f + eastWidth * 0.5f, ceiling + 0.5f, 170f),
                    new Vector3(eastWidth, 1f, k_CloisterMaxZ - k_CloisterMinZ),
                    "East ambulatory roof");
                Box(region, area, Base, "PB_R02A02_Roof_N", GreyboxRole.Blocking,
                    new Vector3(0f, ceiling + 0.5f, k_CloisterMinZ + northDepth * 0.5f),
                    new Vector3(22f, 1f, northDepth), "North ambulatory roof");
                Box(region, area, Base, "PB_R02A02_Roof_S", GreyboxRole.Blocking,
                    new Vector3(0f, ceiling + 0.5f, 180f + southDepth * 0.5f),
                    new Vector3(22f, 1f, southDepth), "South ambulatory roof");
            }

            // ---- Shared helpers ---------------------------------------------

            private void AddColumn(
                int region,
                string area,
                string objectName,
                float x,
                float z,
                float baseY,
                float height)
            {
                Wall(region, area, Base, objectName,
                    new Vector3(x, baseY, z), ColumnSize, height, ColumnSize,
                    "Column: scale reference and camera collision test");
            }

            private void Cover(
                int region,
                string area,
                string objectName,
                Vector3 baseCentre,
                float length,
                float yawDegrees)
            {
                m_boxes.Add(new GreyboxBox(
                    region, area, Base, objectName, GreyboxRole.Cover,
                    new Vector3(baseCentre.x, baseCentre.y + CoverHeight * 0.5f, baseCentre.z),
                    new Vector3(0f, yawDegrees, 0f),
                    new Vector3(length, CoverHeight, 0.5f),
                    "Chest-high cover: breaks sightlines, keeps the fight readable"));
            }

            private void Prop(
                int region,
                string area,
                string objectName,
                Vector3 baseCentre,
                Vector3 size,
                string purpose)
            {
                Box(region, area, Props, objectName, GreyboxRole.Prop,
                    new Vector3(baseCentre.x, baseCentre.y, baseCentre.z), size, purpose);
            }

            private void Fog(
                int region,
                string area,
                Vector3 centre,
                Vector3 size,
                string purpose)
            {
                Box(region, area, Effects, $"PB_{area}_FogVolume", GreyboxRole.Trigger,
                    centre, size, purpose);
            }

            private void LightPlaceholder(
                int region,
                string area,
                Vector3 position,
                string purpose)
            {
                Box(region, area, Effects, $"PB_{area}_LightPlaceholder_{m_boxes.Count:00}",
                    GreyboxRole.Marker, position, new Vector3(0.5f, 0.5f, 0.5f), purpose);
            }
        }

        /// <summary>Which flanking geometry a path span receives.</summary>
        [System.Flags]
        private enum PathSides
        {
            None = 0,
            Mountain = 1 << 0,
            Drop = 1 << 1
        }

        /// <summary>Which edge of a platform a parapet segment stands on.</summary>
        private enum ParapetEdge
        {
            South,
            North,
            West,
            East
        }
    }
}
