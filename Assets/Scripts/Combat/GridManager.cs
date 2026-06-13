using System;
using System.Collections.Generic;
using BeatHero.Data;
using UnityEngine;

namespace BeatHero.Combat
{
    // 논리 그리드 + 타일 시각화. 플레이어 위치·위험 타일·장애물 상태를 보유.
    public class GridManager : MonoBehaviour
    {
        [SerializeField] private GameObject _tilePrefab;
        [SerializeField] private float _cellSize = 1f;

        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }
        public Vector2Int PlayerPosition { get; private set; }

        public event Action<Vector2Int> OnPlayerMoved;

        // 현재 CallPhase에서 표시 중인 위험 셀 효과 (null = 안전)
        private CellEffect[,] _dangerMap;
        // 장애물 목록 (BattleStateMachine이 관리 후 전달)
        private List<ActiveHazard> _hazards = new();

        private GameObject[,] _tiles;

        // 타일 색상
        private static readonly Color COLOR_NORMAL           = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color COLOR_DANGER_CALL      = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color COLOR_DANGER_RESPONSE  = new Color(1.0f, 0.85f, 0.0f);
        private static readonly Color COLOR_HAZARD           = new Color(0.6f, 0.1f, 0.6f);
        private static readonly Color COLOR_SHIELD           = new Color(0.2f, 0.4f, 0.9f);

        private bool _isResponsePhase;

        public void Initialize(GridType gridType)
        {
            GridWidth = GridHeight = gridType == GridType.Boss5x5 ? 5 : 3;
            _dangerMap = new CellEffect[GridWidth, GridHeight];
            CreateTiles();
            PlayerPosition = new Vector2Int(GridWidth / 2, GridHeight / 2);
            RefreshVisuals();
        }

        // CallPhase: 현재 BeatUnit의 GridEffectShape을 표시
        public void ShowShape(GridEffectShape shape)
        {
            _dangerMap = new CellEffect[GridWidth, GridHeight];
            if (shape == null) { RefreshVisuals(); return; }

            if (shape is GridEffectShape3x3 s3 && GridWidth == 3)
                for (int x = 0; x < 3; x++)
                    for (int y = 0; y < 3; y++)
                        _dangerMap[x, y] = s3.cells[x, y];
            else if (shape is GridEffectShape5x5 s5 && GridWidth == 5)
                for (int x = 0; x < 5; x++)
                    for (int y = 0; y < 5; y++)
                        _dangerMap[x, y] = s5.cells[x, y];

            RefreshVisuals();
        }

        public void ClearShape()
        {
            _dangerMap = new CellEffect[GridWidth, GridHeight];
            RefreshVisuals();
        }

        // ResponsePhase용: 타일 색상 변경 없이 _dangerMap만 갱신 (시각 피드백은 VFX가 담당)
        public void UpdateDangerMap(GridEffectShape shape)
        {
            _dangerMap = new CellEffect[GridWidth, GridHeight];
            if (shape == null) return;

            if (shape is GridEffectShape3x3 s3 && GridWidth == 3)
                for (int x = 0; x < 3; x++)
                    for (int y = 0; y < 3; y++)
                        _dangerMap[x, y] = s3.cells[x, y];
            else if (shape is GridEffectShape5x5 s5 && GridWidth == 5)
                for (int x = 0; x < 5; x++)
                    for (int y = 0; y < 5; y++)
                        _dangerMap[x, y] = s5.cells[x, y];
        }

        public void SetHazards(List<ActiveHazard> hazards)
        {
            _hazards = hazards;
            RefreshVisuals();
        }

        // 이동 요청: 장애물/경계 체크 후 허용 여부 반환
        public bool TryMovePlayer(Vector2Int delta)
        {
            var target = PlayerPosition + delta;
            if (target.x < 0 || target.x >= GridWidth || target.y < 0 || target.y >= GridHeight)
                return false;
            if (IsHazardAt(target))
                return false;

            PlayerPosition = target;
            RefreshVisuals();
            OnPlayerMoved?.Invoke(PlayerPosition);
            return true;
        }

        public void SetResponsePhase(bool isResponse)
        {
            _isResponsePhase = isResponse;
        }

        public Vector3 GetTileWorldPosition(Vector2Int gridPos)
        {
            float offset = (GridWidth - 1) * _cellSize * 0.5f;
            return transform.position + new Vector3(
                gridPos.x * _cellSize - offset,
                gridPos.y * _cellSize - offset,
                0f);
        }

        public CellEffect GetDangerAt(Vector2Int pos)
            => (pos.x >= 0 && pos.x < GridWidth && pos.y >= 0 && pos.y < GridHeight)
                ? _dangerMap[pos.x, pos.y]
                : null;

        public bool IsHazardAt(Vector2Int pos)
            => _hazards.Exists(h => h.Position == pos);

        private void CreateTiles()
        {
            if (_tiles != null)
                foreach (var t in _tiles) if (t) Destroy(t);

            _tiles = new GameObject[GridWidth, GridHeight];
            float offset = (GridWidth - 1) * _cellSize * 0.5f;
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                {
                    var pos = new Vector3(x * _cellSize - offset, y * _cellSize - offset, 0);
                    var tile = _tilePrefab
                        ? Instantiate(_tilePrefab, transform.position + pos, Quaternion.identity, transform)
                        : CreateDefaultTile(pos);
                    _tiles[x, y] = tile;
                    var tileSr = tile.GetComponent<SpriteRenderer>();
                    if (tileSr != null) tileSr.sortingOrder = -1;
                }
        }

        private GameObject CreateDefaultTile(Vector3 localPos)
        {
            var go = new GameObject("Tile");
            go.transform.SetParent(transform);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.transform.localScale = Vector3.one * (_cellSize * 0.9f);
            return go;
        }

        private void RefreshVisuals()
        {
            if (_tiles == null) return;
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                {
                    var sr = _tiles[x, y]?.GetComponent<SpriteRenderer>();
                    if (sr == null) continue;

                    var pos = new Vector2Int(x, y);
                    if (IsHazardAt(pos))
                        sr.color = COLOR_HAZARD;
                    else if (_dangerMap[x, y] is ShieldEffect)
                        sr.color = COLOR_SHIELD;
                    else if (_dangerMap[x, y] != null && !_isResponsePhase)
                        sr.color = COLOR_DANGER_CALL;
                    else
                        sr.color = COLOR_NORMAL;
                }
        }

        private static Sprite _squareSprite;
        private static Sprite CreateSquareSprite()
        {
            if (_squareSprite) return _squareSprite;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _squareSprite;
        }
    }
}
