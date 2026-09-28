using System.Collections.Generic;
using LegionBreak.Infrastructure.Movement;
using LegionBreak.Infrastructure.Spawning;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LegionBreak.Editor.Movement
{
    /// <summary>
    /// Play 중 MonsterMovementResolver의 실제 런타임 상태(WalkableGrid, FlowField BFS 거리장/방향장,
    /// 겹침 회피용 Spatial Hash 버킷)를 씬 뷰에 오버레이하는 디버그 툴(CLAUDE.md 9주차 여유 항목).
    /// 최적화 작업을 말로 설명하는 대신 화면으로 보여주기 위한 것 — 플레이 영상/면접 설명 소재.
    ///
    /// 그리는 것:
    /// - Walkable Grid: 장애물로 베이크된 셀(빨강)
    /// - BFS Distance: 플레이어(목표 셀)로부터의 BFS 거리 히트맵(가까울수록 따뜻한 색). 도달 불가 셀은 회색
    /// - Flow Arrows: FlowFieldSeekJob이 샘플링하는 셀별 이동 방향
    /// - Spatial Hash Cells: 몬스터가 들어있는 겹침 회피 셀(셀 크기 = separationRadius × 2)과 셀당 마릿수
    /// - Selected Query: Hierarchy에서 몬스터를 선택하면, MonsterSeparationJob이 그 몬스터에 대해 실제로
    ///   스캔하는 3x3 셀 영역과 후보 이웃(겹침 = 주황, 겹치지 않음 = 회색, 해시 충돌로 섞여 들어온 먼
    ///   셀의 몬스터 = 자홍)을 선으로 잇는다 — "O(n²) 대신 주변 후보만 검사한다"와 "해시 버킷은 충돌이
    ///   있어도 거리 판정으로 걸러지므로 정확성에 문제가 없다"를 한 화면에 보여준다.
    ///
    /// 데이터는 복사본을 만들지 않고 Resolver가 Job에 넘기는 버퍼를 그대로 읽는다(internal 접근자,
    /// Infrastructure/AssemblyInfo.cs). 그래야 "보이는 것 = Job이 실제로 쓰는 것"이 보장된다.
    /// 수천 개 셀을 Handles 호출로 하나씩 그리면 IMGUI 오버헤드가 커서 GL 즉시 모드로 일괄 그린다.
    ///
    /// 켜고 끄기는 씬 뷰의 "Movement Debug" 오버레이 패널(MovementDebugPanel)이 담당하며, 패널이
    /// 숨겨진 씬 뷰에는 아무것도 그리지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class MovementDebugGizmo
    {
        // 지면(Y=0)과 Z-fighting이 나지 않도록 살짝 띄워 그린다. 레이어끼리도 순서대로 조금씩 올린다.
        private const float HeatmapHeight = 0.03f;
        private const float HashCellHeight = 0.05f;
        private const float LineHeight = 0.07f;

        private static readonly Color BlockedColor = new Color(0.9f, 0.15f, 0.15f, 0.55f);
        private static readonly Color UnreachableColor = new Color(0.1f, 0.1f, 0.1f, 0.45f);
        private static readonly Color GoalColor = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color NearColor = new Color(1f, 0.8f, 0.2f, 0.3f);
        private static readonly Color FarColor = new Color(0.2f, 0.4f, 1f, 0.3f);
        private static readonly Color ArrowColor = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color HashCellOutlineColor = new Color(0.2f, 1f, 0.6f, 0.6f);
        private static readonly Color QueryRegionColor = new Color(0.2f, 0.9f, 1f, 0.95f);
        private static readonly Color OverlapColor = new Color(1f, 0.55f, 0.1f, 1f);
        private static readonly Color CandidateColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        private static readonly Color CollisionColor = new Color(1f, 0.2f, 1f, 1f);

        // 셀당 마릿수 → 색. 1마리는 겹침 가능성이 낮고, 4마리 이상이면 밀집 구간이다.
        private static readonly Color[] HashCellFillByCount =
        {
            new Color(0.2f, 1f, 0.6f, 0.12f),
            new Color(1f, 0.9f, 0.2f, 0.22f),
            new Color(1f, 0.6f, 0.1f, 0.3f),
            new Color(1f, 0.2f, 0.1f, 0.4f)
        };

        private const string PrefsPrefix = "LegionBreak.MovementDebug.";

        internal static bool ShowWalkableGrid
        {
            get => EditorPrefs.GetBool(PrefsPrefix + "WalkableGrid", true);
            set => SetPref("WalkableGrid", value);
        }

        internal static bool ShowDistanceHeatmap
        {
            get => EditorPrefs.GetBool(PrefsPrefix + "DistanceHeatmap", true);
            set => SetPref("DistanceHeatmap", value);
        }

        internal static bool ShowFlowArrows
        {
            get => EditorPrefs.GetBool(PrefsPrefix + "FlowArrows", true);
            set => SetPref("FlowArrows", value);
        }

        internal static bool ShowHashCells
        {
            get => EditorPrefs.GetBool(PrefsPrefix + "HashCells", true);
            set => SetPref("HashCells", value);
        }

        internal static bool ShowSelectedQuery
        {
            get => EditorPrefs.GetBool(PrefsPrefix + "SelectedQuery", true);
            set => SetPref("SelectedQuery", value);
        }

        /// <summary>패널이 표시할 마지막 Repaint 시점의 통계.</summary>
        internal static MovementDebugStats Stats;

        private static MonsterMovementResolver _resolver;
        private static bool _resolverSearched;

        // 셀 좌표 → 마릿수. 매 Repaint마다 Clear()만 하고 재할당하지 않는다(에디터 코드라 GC가 게임
        // 수치에 잡히진 않지만, 프로파일링 중에 켜둘 수 있는 툴이라 에디터 쪽 스파이크도 줄여둔다).
        private static readonly Dictionary<long, int> CellCounts = new Dictionary<long, int>();

        static MovementDebugGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += _ =>
            {
                _resolver = null;
                _resolverSearched = false;
                Stats = default;
            };
            Selection.selectionChanged += SceneView.RepaintAll;
        }

        // GL 즉시 모드용 머티리얼. 처음엔 HandleUtility.ApplyWireMaterial을 썼으나 Unity 6.3에서
        // 공개 API로 노출되지 않아(CS0117) 버텍스 컬러 + 알파 블렌딩을 지원하는 내장 셰이더
        // Hidden/Internal-Colored로 직접 만든다. 깊이 테스트는 LessEqual — 몬스터/장애물 메시가
        // 히트맵·화살표를 가리도록 해 월드에 깔린 것처럼 보이게 한다. 씬 저장/도메인 리로드에
        // 섞이지 않도록 HideAndDontSave로 두고, 리로드로 파괴되면 다시 만든다.
        private static Material _overlayMaterial;

        private static Material GetOverlayMaterial()
        {
            if (_overlayMaterial != null)
            {
                return _overlayMaterial;
            }

            _overlayMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _overlayMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _overlayMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _overlayMaterial.SetInt("_Cull", (int)CullMode.Off);
            _overlayMaterial.SetInt("_ZWrite", 0);
            _overlayMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            return _overlayMaterial;
        }

        private static void SetPref(string key, bool value)
        {
            EditorPrefs.SetBool(PrefsPrefix + key, value);
            SceneView.RepaintAll();
        }

        private static MonsterMovementResolver GetResolver()
        {
            if (_resolver == null && !_resolverSearched)
            {
                _resolver = Object.FindFirstObjectByType<MonsterMovementResolver>();
                _resolverSearched = _resolver == null;
            }

            return _resolver;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // LegionBreak.Application 네임스페이스가 UnityEngine.Application을 가려서 전체 이름을 쓴다.
            if (!UnityEngine.Application.isPlaying || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (!sceneView.TryGetOverlay(MovementDebugPanel.OverlayId, out var overlay) || !overlay.displayed)
            {
                return;
            }

            var resolver = GetResolver();
            if (resolver == null || resolver.DebugGrid == null || resolver.DebugFlowField == null)
            {
                Stats = default;
                return;
            }

            resolver.DebugCompleteJobs();

            var stats = new MovementDebugStats { Available = true };
            ComputeGridStats(resolver, ref stats);
            ComputeHashStats(resolver, ref stats);

            GetOverlayMaterial().SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Handles.matrix);

            DrawGridCells(resolver, stats.MaxDistance);
            if (ShowHashCells)
            {
                DrawHashCells(resolver.DebugSeparationCellSize);
            }

            if (ShowFlowArrows)
            {
                DrawFlowArrows(resolver);
            }

            GL.PopMatrix();

            if (ShowSelectedQuery)
            {
                DrawSelectedQuery(resolver, ref stats);
            }

            Stats = stats;
        }

        // ---- 통계 ----

        private static void ComputeGridStats(MonsterMovementResolver resolver, ref MovementDebugStats stats)
        {
            var grid = resolver.DebugGrid;
            var flow = resolver.DebugFlowField;
            var distances = flow.DebugDistances;
            var hasField = flow.DebugGoalCellIndex >= 0;

            stats.GridWidth = grid.Width;
            stats.GridHeight = grid.Height;
            stats.HasFlowField = hasField;

            for (var i = 0; i < grid.Walkable.Length; i++)
            {
                if (!grid.Walkable[i])
                {
                    stats.BlockedCells++;
                }
                else if (hasField)
                {
                    var d = distances[i];
                    if (d == int.MaxValue)
                    {
                        stats.UnreachableCells++;
                    }
                    else if (d > stats.MaxDistance)
                    {
                        stats.MaxDistance = d;
                    }
                }
            }
        }

        // MonsterSeparationJob.Execute와 같은 순회(자기 셀 기준 3x3 버킷, 체인 따라가기)를 메인
        // 스레드에서 그대로 재현해, 이번 프레임 Job이 실제로 수행한 거리 비교 횟수를 센다.
        //
        // 몬스터 수는 DebugMonsterCount(현재 등록 수)가 아니라 DebugBucketBuiltCount(버킷을
        // 마지막으로 구성한 시점의 수)를 쓴다. 등록 직후 버킷이 아직 만들어지지 않은 프레임에는
        // 0으로 초기화된 버퍼의 next[0] == 0 때문에 체인 순회가 끝나지 않아 에디터가 멈췄다
        // (2026-09-28, 사망 후 재시작으로 씬을 리로드한 직후 재현). 체인 인덱스도 같은 범위로 가드한다.
        private static void ComputeHashStats(MonsterMovementResolver resolver, ref MovementDebugStats stats)
        {
            var count = resolver.DebugBucketBuiltCount;
            var positions = resolver.DebugPositions;
            var bucketHeads = resolver.DebugBucketHeads;
            var next = resolver.DebugNext;
            var cellSize = resolver.DebugSeparationCellSize;
            var cellSizeInv = 1f / cellSize;

            stats.MonsterCount = count;
            stats.BruteForcePairChecks = (long)count * (count - 1);

            CellCounts.Clear();
            for (var i = 0; i < count; i++)
            {
                var key = CellKey(ToCell(positions[i].x, cellSizeInv), ToCell(positions[i].y, cellSizeInv));
                CellCounts.TryGetValue(key, out var c);
                CellCounts[key] = c + 1;
                if (c + 1 > stats.MaxPerCell)
                {
                    stats.MaxPerCell = c + 1;
                }
            }

            stats.OccupiedCells = CellCounts.Count;

            for (var i = 0; i < count; i++)
            {
                var cellX = ToCell(positions[i].x, cellSizeInv);
                var cellZ = ToCell(positions[i].y, cellSizeInv);
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        var j = bucketHeads[resolver.DebugHashCell(cellX + dx, cellZ + dz)];
                        while (j >= 0 && j < count)
                        {
                            if (j != i)
                            {
                                stats.PairChecks++;
                                if (!IsInNeighborhood(positions[j], cellX, cellZ, cellSizeInv))
                                {
                                    stats.HashCollisionChecks++;
                                }
                            }

                            j = next[j];
                        }
                    }
                }
            }
        }

        // ---- 그리기 ----

        private static void DrawGridCells(MonsterMovementResolver resolver, int maxDistance)
        {
            var showBlocked = ShowWalkableGrid;
            var showHeatmap = ShowDistanceHeatmap;
            if (!showBlocked && !showHeatmap)
            {
                return;
            }

            var grid = resolver.DebugGrid;
            var flow = resolver.DebugFlowField;
            var distances = flow.DebugDistances;
            var goalIndex = flow.DebugGoalCellIndex;
            var hasField = goalIndex >= 0;
            var cs = grid.CellSize;

            GL.Begin(GL.QUADS);
            for (var z = 0; z < grid.Height; z++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.CellIndex(x, z);
                    Color color;
                    if (!grid.Walkable[index])
                    {
                        if (!showBlocked)
                        {
                            continue;
                        }

                        color = BlockedColor;
                    }
                    else
                    {
                        if (!showHeatmap || !hasField)
                        {
                            continue;
                        }

                        var d = distances[index];
                        color = index == goalIndex ? GoalColor
                            : d == int.MaxValue ? UnreachableColor
                            : Color.Lerp(NearColor, FarColor, maxDistance > 0 ? (float)d / maxDistance : 0f);
                    }

                    var x0 = grid.Origin.x + x * cs;
                    var z0 = grid.Origin.y + z * cs;
                    Quad(x0, z0, x0 + cs, z0 + cs, HeatmapHeight, color);
                }
            }

            GL.End();
        }

        private static void DrawFlowArrows(MonsterMovementResolver resolver)
        {
            var grid = resolver.DebugGrid;
            var flow = resolver.DebugFlowField;
            if (flow.DebugGoalCellIndex < 0)
            {
                return;
            }

            var directions = flow.Directions;
            var cs = grid.CellSize;
            var shaft = cs * 0.35f;
            var head = cs * 0.18f;

            GL.Begin(GL.LINES);
            GL.Color(ArrowColor);
            for (var z = 0; z < grid.Height; z++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var dir = directions[grid.CellIndex(x, z)];
                    if (dir.x == 0f && dir.y == 0f)
                    {
                        continue;
                    }

                    var center = new float2(grid.Origin.x + (x + 0.5f) * cs, grid.Origin.y + (z + 0.5f) * cs);
                    var tail = center - dir * shaft;
                    var tip = center + dir * shaft;
                    Line(tail, tip, LineHeight);
                    // 화살촉: 진행 방향을 ±150° 돌린 두 날개.
                    Line(tip, tip + Rotate(dir, 150f) * head, LineHeight);
                    Line(tip, tip + Rotate(dir, -150f) * head, LineHeight);
                }
            }

            GL.End();
        }

        private static void DrawHashCells(float cellSize)
        {
            GL.Begin(GL.QUADS);
            foreach (var pair in CellCounts)
            {
                UnpackCellKey(pair.Key, out var cellX, out var cellZ);
                var fill = HashCellFillByCount[Mathf.Min(pair.Value, HashCellFillByCount.Length) - 1];
                Quad(cellX * cellSize, cellZ * cellSize, (cellX + 1) * cellSize, (cellZ + 1) * cellSize, HashCellHeight, fill);
            }

            GL.End();

            GL.Begin(GL.LINES);
            GL.Color(HashCellOutlineColor);
            foreach (var pair in CellCounts)
            {
                UnpackCellKey(pair.Key, out var cellX, out var cellZ);
                RectOutline(cellX * cellSize, cellZ * cellSize, (cellX + 1) * cellSize, (cellZ + 1) * cellSize, HashCellHeight);
            }

            GL.End();
        }

        private static void DrawSelectedQuery(MonsterMovementResolver resolver, ref MovementDebugStats stats)
        {
            var selected = Selection.activeGameObject;
            // 모델 자식(Brute 메시 등)을 클릭해도 루트의 MonsterView를 찾는다.
            var view = selected != null ? selected.GetComponentInParent<MonsterView>() : null;
            // 버킷이 아직 이 몬스터를 포함해 구성되지 않았으면(등록 직후 프레임) 그리지 않는다 —
            // ComputeHashStats 주석의 무한 루프와 같은 이유.
            var builtCount = resolver.DebugBucketBuiltCount;
            if (view == null || !resolver.DebugTryGetIndex(view, out var index) || index >= builtCount)
            {
                return;
            }

            var positions = resolver.DebugPositions;
            var bucketHeads = resolver.DebugBucketHeads;
            var next = resolver.DebugNext;
            var cellSize = resolver.DebugSeparationCellSize;
            var cellSizeInv = 1f / cellSize;
            var minDistanceSq = cellSize * cellSize;

            var self = positions[index];
            var cellX = ToCell(self.x, cellSizeInv);
            var cellZ = ToCell(self.y, cellSizeInv);
            var selfWorld = new Vector3(self.x, LineHeight, self.y);

            stats.HasSelection = true;

            // 조회 영역/후보 선은 몬스터 메시에 가려지지 않도록 깊이 테스트 없이 그린다.
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;
            Handles.color = QueryRegionColor;
            Handles.DrawAAPolyLine(3f,
                new Vector3((cellX - 1) * cellSize, LineHeight, (cellZ - 1) * cellSize),
                new Vector3((cellX + 2) * cellSize, LineHeight, (cellZ - 1) * cellSize),
                new Vector3((cellX + 2) * cellSize, LineHeight, (cellZ + 2) * cellSize),
                new Vector3((cellX - 1) * cellSize, LineHeight, (cellZ + 2) * cellSize),
                new Vector3((cellX - 1) * cellSize, LineHeight, (cellZ - 1) * cellSize));
            Handles.DrawWireDisc(selfWorld, Vector3.up, resolver.DebugSeparationRadius);

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    var j = bucketHeads[resolver.DebugHashCell(cellX + dx, cellZ + dz)];
                    while (j >= 0 && j < builtCount)
                    {
                        if (j != index)
                        {
                            var other = positions[j];
                            var otherWorld = new Vector3(other.x, LineHeight, other.y);
                            stats.SelectedCandidates++;

                            if (!IsInNeighborhood(other, cellX, cellZ, cellSizeInv))
                            {
                                // 해시 충돌: 3x3 밖 먼 셀의 몬스터가 같은 버킷에 들어와 후보로 스캔됐다.
                                // 거리 판정에서 탈락하므로 결과는 정확하고, 비용만 조금 늘어난다.
                                stats.SelectedCollisions++;
                                Handles.color = CollisionColor;
                                Handles.DrawDottedLine(selfWorld, otherWorld, 4f);
                            }
                            else if (math.lengthsq(self - other) < minDistanceSq)
                            {
                                stats.SelectedOverlaps++;
                                Handles.color = OverlapColor;
                                Handles.DrawAAPolyLine(3f, selfWorld, otherWorld);
                                Handles.DrawWireDisc(otherWorld, Vector3.up, resolver.DebugSeparationRadius);
                            }
                            else
                            {
                                Handles.color = CandidateColor;
                                Handles.DrawLine(selfWorld, otherWorld);
                            }
                        }

                        j = next[j];
                    }
                }
            }

            Handles.Label(
                selfWorld + Vector3.up * 2.2f,
                $"후보 {stats.SelectedCandidates} / 겹침 {stats.SelectedOverlaps} / 해시 충돌 {stats.SelectedCollisions}\n(전수 검사라면 {stats.MonsterCount - 1})",
                EditorStyles.whiteBoldLabel);
            Handles.zTest = previousZTest;
        }

        // ---- 헬퍼 ----

        // MonsterMovementResolver/MonsterSeparationJob과 같은 셀 좌표 규칙(floor(pos / cellSize)).
        private static int ToCell(float value, float cellSizeInv) => Mathf.FloorToInt(value * cellSizeInv);

        private static bool IsInNeighborhood(float2 pos, int cellX, int cellZ, float cellSizeInv)
        {
            return Mathf.Abs(ToCell(pos.x, cellSizeInv) - cellX) <= 1
                && Mathf.Abs(ToCell(pos.y, cellSizeInv) - cellZ) <= 1;
        }

        private static long CellKey(int cellX, int cellZ) => ((long)cellX << 32) | (uint)cellZ;

        private static void UnpackCellKey(long key, out int cellX, out int cellZ)
        {
            cellX = (int)(key >> 32);
            cellZ = (int)(uint)key;
        }

        private static float2 Rotate(float2 v, float degrees)
        {
            var r = math.radians(degrees);
            math.sincos(r, out var s, out var c);
            return new float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static void Quad(float x0, float z0, float x1, float z1, float y, Color color)
        {
            GL.Color(color);
            GL.Vertex3(x0, y, z0);
            GL.Vertex3(x0, y, z1);
            GL.Vertex3(x1, y, z1);
            GL.Vertex3(x1, y, z0);
        }

        private static void RectOutline(float x0, float z0, float x1, float z1, float y)
        {
            GL.Vertex3(x0, y, z0); GL.Vertex3(x1, y, z0);
            GL.Vertex3(x1, y, z0); GL.Vertex3(x1, y, z1);
            GL.Vertex3(x1, y, z1); GL.Vertex3(x0, y, z1);
            GL.Vertex3(x0, y, z1); GL.Vertex3(x0, y, z0);
        }

        private static void Line(float2 a, float2 b, float y)
        {
            GL.Vertex3(a.x, y, a.y);
            GL.Vertex3(b.x, y, b.y);
        }
    }

    /// <summary>MovementDebugPanel이 표시하는 한 Repaint 시점의 수치.</summary>
    internal struct MovementDebugStats
    {
        public bool Available;
        public bool HasFlowField;
        public int GridWidth;
        public int GridHeight;
        public int BlockedCells;
        public int UnreachableCells;
        public int MaxDistance;

        public int MonsterCount;
        public int OccupiedCells;
        public int MaxPerCell;
        public long PairChecks;
        public long HashCollisionChecks;
        public long BruteForcePairChecks;

        public bool HasSelection;
        public int SelectedCandidates;
        public int SelectedOverlaps;
        public int SelectedCollisions;
    }
}
