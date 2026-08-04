using Unity.Collections;
using UnityEngine;

namespace LegionBreak.Infrastructure.Pathfinding
{
    /// <summary>
    /// 씬의 정적 장애물(Collider, obstacleLayerMask)을 셀 단위로 한 번 베이크해 만든
    /// walkable/blocked 격자. FlowFieldGenerator가 BFS의 입력으로 사용한다.
    ///
    /// 몬스터 Collider는 4주차에 런타임 오버헤드(매 프레임 판정) 때문에 제거했지만, 이
    /// 베이크는 씬 로드 시 정적 장애물 대상으로 1회만 실행되는 별개의 비용이라 그 결정과
    /// 모순되지 않는다.
    /// </summary>
    public sealed class WalkableGrid
    {
        public Vector2 Origin { get; }
        public float CellSize { get; }
        public int Width { get; }
        public int Height { get; }

        // NativeArray<bool>을 get 전용 프로퍼티로 두면 인덱서 대입 시 "프로퍼티가 반환한
        // 값(구조체 복사본)은 변수가 아니다"(CS1612)로 컴파일 에러가 난다. 필드로 노출해야
        // Bake()에서 grid.Walkable[i] = ...가 실제 네이티브 버퍼에 쓰인다.
        // readonly로 두면 "인덱서 setter가 필드를 변경할 수 있다"고 컴파일러가 보수적으로
        // 판단해 생성자 밖에서의 인덱서 대입 자체를 막는다(CS1648) — 실제로는 NativeArray가
        // 포인터로 네이티브 버퍼를 가리킬 뿐 구조체 자신은 변경되지 않으므로 readonly를 뺀다.
        public NativeArray<bool> Walkable;

        private WalkableGrid(Vector2 origin, float cellSize, int width, int height)
        {
            Origin = origin;
            CellSize = cellSize;
            Width = width;
            Height = height;
            Walkable = new NativeArray<bool>(width * height, Allocator.Persistent);
        }

        public static WalkableGrid Bake(Vector2 center, float halfExtent, float cellSize, LayerMask obstacleLayerMask)
        {
            var width = Mathf.Max(1, Mathf.CeilToInt(halfExtent * 2f / cellSize));
            var height = width;
            var origin = new Vector2(center.x - halfExtent, center.y - halfExtent);
            var grid = new WalkableGrid(origin, cellSize, width, height);

            var halfCell = cellSize * 0.5f;
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var worldX = origin.x + (x + 0.5f) * cellSize;
                    var worldZ = origin.y + (z + 0.5f) * cellSize;
                    var blocked = Physics.CheckBox(
                        new Vector3(worldX, 0f, worldZ),
                        new Vector3(halfCell, 1f, halfCell),
                        Quaternion.identity,
                        obstacleLayerMask);
                    grid.Walkable[x + z * width] = !blocked;
                }
            }

            return grid;
        }

        public bool TryWorldToCell(Vector2 worldXZ, out int cellX, out int cellZ)
        {
            cellX = Mathf.FloorToInt((worldXZ.x - Origin.x) / CellSize);
            cellZ = Mathf.FloorToInt((worldXZ.y - Origin.y) / CellSize);
            return cellX >= 0 && cellZ >= 0 && cellX < Width && cellZ < Height;
        }

        public bool IsWalkable(int cellX, int cellZ)
        {
            if (cellX < 0 || cellZ < 0 || cellX >= Width || cellZ >= Height)
            {
                return false;
            }

            return Walkable[cellX + cellZ * Width];
        }

        public int CellIndex(int cellX, int cellZ) => cellX + cellZ * Width;

        public void Dispose()
        {
            if (Walkable.IsCreated)
            {
                Walkable.Dispose();
            }
        }
    }
}
