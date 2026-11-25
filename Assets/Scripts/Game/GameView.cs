using System.Collections.Generic;
using SonTob.Core;
using UnityEngine;

namespace SonTob.Game
{
    /// <summary>
    /// 화면 전체. 규칙은 SonTob.Core에 있고, 여기서는 그리기와 입력만 합니다.
    /// 씬에 아무것도 없어도 Bootstrap이 이 컴포넌트를 만듭니다.
    /// 흐름: 제목 → 그림 컷 2장 → 밤 → (먹으면) 2초 새벽 → 낮 → 끝.
    /// </summary>
    public class GameView : MonoBehaviour
    {
        enum Mode { Title, Cutscene, Playing, Dawn, End }

        // 색 (낮 / 밤)
        static readonly Color DayGround = new Color(0.86f, 0.80f, 0.66f);
        static readonly Color DayBlocked = new Color(0.62f, 0.66f, 0.48f);   // 막힌 칸 바닥: 풀숲
        static readonly Color DayWall = new Color(0.55f, 0.47f, 0.40f);
        static readonly Color NightGround = new Color(0.24f, 0.27f, 0.38f);
        static readonly Color NightBlocked = new Color(0.14f, 0.17f, 0.22f);
        static readonly Color NightWall = new Color(0.30f, 0.32f, 0.42f);
        static readonly Color DogHoleCol = new Color(0.35f, 0.30f, 0.25f);
        static readonly Color WellCol = new Color(0.45f, 0.65f, 0.85f);
        static readonly Color HoleCol = new Color(0.08f, 0.06f, 0.05f);
        static readonly Color JarCol = new Color(0.42f, 0.26f, 0.18f);
        static readonly Color SightCol = new Color(1f, 0.70f, 0.15f);
        static readonly Color RealSightCol = new Color(0.92f, 0.18f, 0.18f);
        static readonly Color CatSightCol = new Color(0.95f, 0.15f, 0.15f);
        static readonly Color DarkCol = new Color(0.02f, 0.03f, 0.07f, 0.80f);
        static readonly Color CatCol = new Color(0.65f, 0.45f, 0.80f);
        static readonly Color WalkCol = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color RunCol = new Color(0.35f, 0.60f, 1f, 0.60f);
        static readonly Color RiskCol = new Color(1f, 0.15f, 0.15f, 0.70f);
        static readonly Color ActCol = new Color(1f, 0.85f, 0.2f, 0.9f);
        static readonly Color ShadowCol = new Color(0.10f, 0.08f, 0.06f, 0.45f);
        static readonly Color LordCol = new Color(0.25f, 0.42f, 0.85f);
        static readonly Color NailCol = new Color(1f, 0.93f, 0.55f);
        static readonly float[] BandAlpha = { 0.42f, 0.28f, 0.14f }; // 시선 띠: 멀수록 옅게

        const float DoubleTapSeconds = 0.3f;
        const float CutSeconds = 3f;      // 그림 컷 한 장(기획서 5.2)
        const float DawnSeconds = 2f;     // 먹은 뒤 아침이 될 때까지(기획서 3.1)
        const int CutCount = 2;

        Mode mode = Mode.Title;
        GameConfig config;
        GameRun run;

        Camera cam;
        Transform staticRoot, dynamicRoot;
        bool zoomOut;
        Vector3 camTarget;

        readonly List<string> toasts = new List<string>();
        float toastUntil;
        bool catHintShown;

        // 쥐가 바라보는 방향(밤에 앞 3칸만 보임)
        Dir ratFacing = Dir.PlusX;

        // 그림 컷과 새벽
        int cutIndex;
        float cutStart;
        float dawnStart;

        // 해 뜨는 바뀜(화면 덮개가 서서히 걷힘)
        float fadeStart = -10f;
        Color fadeCol;
        const float FadeSeconds = 1.4f;

        // 두 번 누르기 판정을 기다리는 터치
        Dir? pendingDir;
        float pendingTime;

        // OnGUI
        float uiScale = 1f;
        GUIStyle sTitle, sBody, sSmall, sButton, sBig, sCaption;
        Font font;
        readonly List<Rect> hudRects = new List<Rect>();
        Texture2D white;

        void Awake()
        {
            Application.targetFrameRate = 60;
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, -2.75f, -10);
            staticRoot = new GameObject("Map").transform;
            dynamicRoot = new GameObject("Pieces").transform;
            font = Resources.Load<Font>("Fonts/HUD");
            white = Texture2D.whiteTexture;
        }

        // ───────────────────────── 판 진행

        void StartRun()
        {
            config = new GameConfig();
            run = new GameRun(config);
            ratFacing = Dir.PlusX;
            pendingDir = null;
            zoomOut = false;
            BuildStatic();
            Refresh();
            mode = Mode.Cutscene;
            cutIndex = 0;
            cutStart = Time.time;
        }

        void NextCut()
        {
            cutIndex++;
            cutStart = Time.time;
            if (cutIndex >= CutCount) EndCutscene();
        }

        void EndCutscene()
        {
            mode = Mode.Playing;
            fadeStart = Time.time; fadeCol = new Color(0.02f, 0.03f, 0.07f);
            Refresh();
            if (!catHintShown)
            {
                catHintShown = true;
                Toast("고양이가 지나가기를 기다려요. 붉은 칸이 고양이 눈이에요.", 5f);
            }
        }

        void RetryNight()
        {
            run.RetryNight();
            ratFacing = Dir.PlusX;
            pendingDir = null;
            BuildStatic();
            Refresh();
        }

        void Play(Move m)
        {
            var before = run.Phase;
            if (m.Kind == MoveKind.Walk || m.Kind == MoveKind.Run) ratFacing = m.Dir;
            run.Apply(m);
            Toast(run.LastEvents);
            if (run.Phase == Phase.Over) { mode = Mode.End; Refresh(); return; }
            if (before == Phase.Night && run.Phase == Phase.Day)
            {
                // 먹었다: 쥐가 웅크리고 닭이 울며 어둠이 걷힌다. 2초 뒤 아침.
                mode = Mode.Dawn;
                dawnStart = Time.time;
                pendingDir = null;
                Refresh();
                return;
            }
            Refresh();
        }

        void FinishDawn()
        {
            mode = Mode.Playing;
            fadeStart = Time.time; fadeCol = new Color(1f, 0.93f, 0.75f);
            BuildStatic();
            Refresh();
            Toast("아침이다. 낮 걸음 " + run.DayState.StepLimit + " (밤 보너스 +" + run.Bonus + ")", 3.5f);
        }

        void Toast(List<string> lines)
        {
            if (lines == null || lines.Count == 0) return;
            toasts.Clear(); toasts.AddRange(lines);
            toastUntil = Time.time + 2.2f;
        }

        void Toast(string line, float seconds = 2.2f)
        {
            toasts.Clear(); toasts.Add(line);
            toastUntil = Time.time + seconds;
        }

        // ───────────────────────── 좌표

        static Vector3 ToWorld(Pos p, float lift = 0f)
        {
            return new Vector3((p.X - p.Y) * 0.5f, -(p.X + p.Y) * 0.25f + lift, 0f);
        }

        static int Order(Pos p, int layer) { return (p.X + p.Y) * 20 + layer; }

        static Vector3 ScreenVector(Dir d)
        {
            switch (d)
            {
                case Dir.PlusX: return new Vector3(0.5f, -0.25f, 0);
                case Dir.MinusX: return new Vector3(-0.5f, 0.25f, 0);
                case Dir.PlusY: return new Vector3(-0.5f, -0.25f, 0);
                default: return new Vector3(0.5f, 0.25f, 0);
            }
        }

        static float Angle(Dir d)
        {
            var v = ScreenVector(d);
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        SpriteRenderer Put(Transform parent, Sprite s, Vector3 pos, Color c, int order, float scale = 1f)
        {
            var go = new GameObject(s.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s; r.color = c; r.sortingOrder = order;
            return r;
        }

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        bool NightShown { get { return run.Phase == Phase.Night || mode == Mode.Dawn || run.DayState == null; } }

        Pos PlayerPos() { return NightShown ? run.Night.Player : run.DayState.Player; }

        // ───────────────────────── 그리기

        void BuildStatic()
        {
            Clear(staticRoot);
            var map = run.Map;
            bool night = run.Phase == Phase.Night;
            cam.backgroundColor = night ? new Color(0.04f, 0.05f, 0.09f) : new Color(0.93f, 0.91f, 0.84f);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var p = new Pos(x, y);
                    var tile = map.TileAt(p);
                    char mark = map.MarkAt(p);
                    if (tile == Tile.Wall)
                    {
                        // 막힌 칸: 길과 다른 무늬의 바닥 + 앞쪽 두 변(남동·남서)에만 낮은 담(기획서 3.3).
                        // 담이 칸 높이를 넘지 않아서 뒤에 선 쥐나 사람이 가려지지 않는다.
                        Put(staticRoot, Shapes.Hatch, ToWorld(p), night ? NightBlocked : DayBlocked, Order(p, 0));
                        if (mark == Village.JarStand) { DrawJarStand(p, night); continue; }
                        var wallCol = night ? NightWall : DayWall;
                        if (map.TileAt(p + Dir.PlusX.Step()) != Tile.Wall) Put(staticRoot, Shapes.EdgeSE, ToWorld(p), wallCol, Order(p, 5));
                        if (map.TileAt(p + Dir.PlusY.Step()) != Tile.Wall) Put(staticRoot, Shapes.EdgeSW, ToWorld(p), wallCol, Order(p, 5));
                        continue;
                    }
                    var ground = night ? NightGround : DayGround;
                    if (tile == Tile.DogHole) ground = DogHoleCol;
                    if (mark == Village.Well) ground = WellCol;
                    Put(staticRoot, Shapes.Diamond, ToWorld(p), ground, Order(p, 0));
                    if (mark == Village.RatHole) Put(staticRoot, Shapes.Circle, ToWorld(p), HoleCol, Order(p, 1), 0.35f);
                    if (tile == Tile.DogHole) Put(staticRoot, Shapes.Circle, ToWorld(p), new Color(0.1f, 0.08f, 0.06f), Order(p, 1), 0.28f);
                }
        }

        /// <summary>장독대: 막힌 칸. 낮에는 고양이가 그 위에서 잡니다. 밤에는 고양이 길이 이 칸을 지납니다.</summary>
        void DrawJarStand(Pos p, bool night)
        {
            var col = night ? JarCol * 0.6f + Color.black * 0.4f : JarCol;
            col.a = 1f;
            Put(staticRoot, Shapes.Circle, ToWorld(p, 0.10f) + new Vector3(-0.16f, 0, 0), col, Order(p, 5), 0.30f);
            Put(staticRoot, Shapes.Circle, ToWorld(p, 0.08f) + new Vector3(0.14f, -0.02f, 0), col, Order(p, 6), 0.34f);
            Put(staticRoot, Shapes.Circle, ToWorld(p, 0.22f), col * 0.9f + Color.white * 0.1f, Order(p, 7), 0.26f);
            if (!night)
            {
                Put(staticRoot, Shapes.Circle, ToWorld(p, 0.40f), CatCol, Order(p, 8), 0.24f);
                Put(staticRoot, Shapes.Circle, ToWorld(p, 0.47f) + new Vector3(0.09f, 0, 0), CatCol, Order(p, 9), 0.14f);
            }
        }

        void Refresh()
        {
            Clear(dynamicRoot);
            if (run == null) return;
            if (NightShown) DrawNight(run.Night);
            else DrawDay(run.DayState);
        }

        bool Controls { get { return mode == Mode.Playing && !run.NightFailed && run.Phase != Phase.Over; } }

        void DrawCandidates(List<Move> moves, Move act, Pos player)
        {
            if (!Controls) return;
            foreach (var m in moves)
            {
                var c = m.Risky ? RiskCol : (m.Kind == MoveKind.Run ? RunCol : WalkCol);
                Put(dynamicRoot, Shapes.DiamondEdge, ToWorld(m.Target), c, Order(m.Target, 3), 0.92f);
                Put(dynamicRoot, Shapes.Diamond, ToWorld(m.Target), new Color(c.r, c.g, c.b, c.a * 0.35f), Order(m.Target, 2), 0.8f);
            }
            var here = act.Kind == MoveKind.Act ? ActCol : (act.Risky ? RiskCol : new Color(1, 1, 1, 0.25f));
            Put(dynamicRoot, Shapes.DiamondEdge, ToWorld(player), here, Order(player, 3), 0.92f);
        }

        /// <summary>시선 띠: 보는 사람에게서 멀어질수록 옅게, 칸마다 바깥쪽 꺾쇠. 진짜 도령은 붉게, 꺾쇠 두 겹(»).</summary>
        void DrawSightBand(Npc n, MapData map, int length, Color col, bool doubleChevron)
        {
            var tiles = n.Sight(map, length);
            float angle = Angle(n.Facing);
            for (int i = 0; i < tiles.Count; i++)
            {
                var p = tiles[i];
                float a = BandAlpha[Mathf.Min(i, BandAlpha.Length - 1)];
                Put(dynamicRoot, Shapes.Diamond, ToWorld(p), new Color(col.r, col.g, col.b, a), Order(p, 2), 0.96f);
                var dir = ScreenVector(n.Facing).normalized;
                var chevCol = new Color(col.r * 0.8f, col.g * 0.6f, col.b * 0.4f, 0.95f);
                var c1 = Put(dynamicRoot, Shapes.Chevron, ToWorld(p) + dir * (doubleChevron ? 0.05f : 0f), chevCol, Order(p, 4), 0.55f);
                c1.transform.rotation = Quaternion.Euler(0, 0, angle);
                if (doubleChevron)
                {
                    var c2 = Put(dynamicRoot, Shapes.Chevron, ToWorld(p) - dir * 0.09f, chevCol, Order(p, 4), 0.55f);
                    c2.transform.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
            // 다음 걸음에 볼 칸: 점선 테두리
            var now = new HashSet<Pos>(tiles);
            foreach (var p in n.NextSight(map, length))
                if (!now.Contains(p))
                    Put(dynamicRoot, Shapes.DiamondDashed, ToWorld(p), new Color(col.r, col.g, col.b, 0.9f), Order(p, 3), 0.94f);
        }

        void DrawDay(DayPhase d)
        {
            var map = d.Map;
            // 임무 칸
            for (int i = 0; i < d.As.Missions.Length; i++)
                foreach (var p in map.Marks(d.As.Missions[i]))
                {
                    var c = d.Done[i] ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : LordCol;
                    Put(dynamicRoot, Shapes.Diamond, ToWorld(p), new Color(c.r, c.g, c.b, 0.75f), Order(p, 1), 0.7f);
                }

            // 시선
            foreach (var n in d.Npcs)
            {
                bool real = n.Spec.IsRealLord;
                DrawSightBand(n, map, d.Config.SightLength, real ? RealSightCol : SightCol, real);
            }

            DrawCandidates(d.Candidates(), d.ActHere(), d.Player);

            // 사람들: 물방울 그림자(뾰족한 쪽 = 보는 쪽) + 몸 + 머리 위 다음 방향 화살표
            foreach (var n in d.Npcs)
            {
                var shadow = Put(dynamicRoot, Shapes.Droplet, ToWorld(n.Pos), ShadowCol, Order(n.Pos, 9), 0.55f);
                shadow.transform.rotation = Quaternion.Euler(0, 0, Angle(n.Facing));
                Color c = n.Spec.IsRealLord ? LordCol * 0.75f + Color.black * 0.25f : new Color(0.45f, 0.42f, 0.40f);
                c.a = 1f;
                var body = Put(dynamicRoot, Shapes.Circle, ToWorld(n.Pos, 0.28f), c, Order(n.Pos, 10), 0.42f);
                if (n.Spec.IsRealLord)
                    Put(dynamicRoot, Shapes.Ring, ToWorld(n.Pos, 0.28f), RealSightCol, Order(n.Pos, 11), 0.5f);
                var next = n.NextFacing();
                if (next != n.Facing)
                {
                    var arrow = Put(dynamicRoot, Shapes.Arrow, ToWorld(n.Pos, 0.62f) + ScreenVector(next) * 0.25f, new Color(0.15f, 0.12f, 0.1f, 0.85f), Order(n.Pos, 12), 0.45f);
                    arrow.transform.rotation = Quaternion.Euler(0, 0, Angle(next));
                }
                body.name = n.Name;
            }

            // 나(도령으로 둔갑한 쥐)
            Put(dynamicRoot, Shapes.Circle, ToWorld(d.Player, 0.30f), LordCol, Order(d.Player, 13), 0.46f);
            Put(dynamicRoot, Shapes.Ring, ToWorld(d.Player, 0.30f), new Color(1f, 0.95f, 0.5f), Order(d.Player, 14), 0.56f);
            camTarget = ToWorld(d.Player);
        }

        void DrawNight(NightPhase n)
        {
            var map = n.Map;

            // 어둠: 쥐는 바라보는 방향 앞 3칸(과 제 칸)만 본다(기획서 3.1)
            var lit = new HashSet<Pos>(Npc.SightLine(map, n.Player, ratFacing, n.Config.SightLength));
            lit.Add(n.Player);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var p = new Pos(x, y);
                    if (!lit.Contains(p)) Put(dynamicRoot, Shapes.Diamond, ToWorld(p), DarkCol, Order(p, 1), 1.02f);
                }

            // 고양이 눈: 어둠 속에서도 붉게 보인다(자기 칸 포함). 다음 걸음에 닿을 칸은 점선.
            foreach (var p in n.CatCone())
                Put(dynamicRoot, Shapes.Diamond, ToWorld(p), new Color(CatSightCol.r, CatSightCol.g, CatSightCol.b, 0.34f), Order(p, 2), 0.96f);
            DrawCatChevrons(n);
            var coneNow = n.CatCone();
            foreach (var p in n.NextCatCone())
                if (!coneNow.Contains(p))
                    Put(dynamicRoot, Shapes.DiamondDashed, ToWorld(p), new Color(CatSightCol.r, CatSightCol.g, CatSightCol.b, 0.9f), Order(p, 3), 0.94f);

            // 손톱: 늘 같은 칸
            if (!n.Eaten)
            {
                Put(dynamicRoot, Shapes.Diamond, ToWorld(n.NailTile), new Color(1f, 0.95f, 0.6f, 0.35f), Order(n.NailTile, 2), 0.9f);
                Put(dynamicRoot, Shapes.Circle, ToWorld(n.NailTile, 0.05f), NailCol, Order(n.NailTile, 4), 0.22f);
                Put(dynamicRoot, Shapes.Ring, ToWorld(n.NailTile, 0.05f), ActCol, Order(n.NailTile, 4), 0.34f);
            }

            DrawCandidates(n.Candidates(), n.ActHere(), n.Player);

            // 고양이: 어둠 속에서도 보인다
            var cs = Put(dynamicRoot, Shapes.Droplet, ToWorld(n.Cat), ShadowCol, Order(n.Cat, 9), 0.5f);
            cs.transform.rotation = Quaternion.Euler(0, 0, Angle(n.CatNpc.Facing));
            Put(dynamicRoot, Shapes.Circle, ToWorld(n.Cat, 0.25f), CatCol, Order(n.Cat, 10), 0.42f);
            Put(dynamicRoot, Shapes.Circle, ToWorld(n.Cat, 0.42f), CatCol, Order(n.Cat, 11), 0.24f);

            // 쥐(웅크린 모습은 새벽에)
            bool curled = mode == Mode.Dawn;
            Put(dynamicRoot, Shapes.Circle, ToWorld(n.Player, curled ? 0.12f : 0.15f), new Color(0.62f, 0.60f, 0.58f), Order(n.Player, 13), curled ? 0.34f : 0.30f);
            if (!curled)
            {
                var look = Put(dynamicRoot, Shapes.Chevron, ToWorld(n.Player, 0.15f) + ScreenVector(ratFacing).normalized * 0.2f, new Color(0.95f, 0.92f, 0.85f, 0.9f), Order(n.Player, 14), 0.4f);
                look.transform.rotation = Quaternion.Euler(0, 0, Angle(ratFacing));
            }
            camTarget = ToWorld(n.Player);
        }

        void DrawCatChevrons(NightPhase n)
        {
            var tiles = n.CatNpc.Sight(n.Map, n.Config.SightLength);
            float angle = Angle(n.CatNpc.Facing);
            foreach (var p in tiles)
            {
                var c = Put(dynamicRoot, Shapes.Chevron, ToWorld(p), new Color(0.75f, 0.1f, 0.1f, 0.95f), Order(p, 4), 0.55f);
                c.transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        // ───────────────────────── 입력과 카메라

        void Update()
        {
            FitCamera();

            if (mode == Mode.Cutscene)
            {
                if (Time.time - cutStart >= CutSeconds) NextCut();
                else if (Input.GetMouseButtonDown(0) && !PointerOverHud(Input.mousePosition)) NextCut();
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) NextCut();
                return;
            }
            if (mode == Mode.Dawn)
            {
                if (Time.time - dawnStart >= DawnSeconds) FinishDawn();
                return;
            }
            if (run == null || !Controls) return;

            // 한 번 누른 뒤 0.3초가 지나도록 두 번째가 없으면 1칸 걷기
            if (pendingDir.HasValue && Time.time - pendingTime > DoubleTapSeconds)
            {
                var d = pendingDir.Value;
                pendingDir = null;
                Step(d, false);
                if (!Controls) return;
            }

            if (Input.GetMouseButtonDown(0) && !PointerOverHud(Input.mousePosition))
            {
                Dir dir;
                if (QuadrantAt(Input.mousePosition, out dir)) Tap(dir);
            }
            HandleKeys();
        }

        /// <summary>나를 중심으로 가로선·세로선을 그어 화면을 네 구역으로 나눕니다(기획서 1장).</summary>
        bool QuadrantAt(Vector3 mouse, out Dir dir)
        {
            dir = Dir.MinusY;
            var me = cam.WorldToScreenPoint(ToWorld(PlayerPos(), 0.2f));
            float dx = mouse.x - me.x, dy = mouse.y - me.y;
            float dead = 18f * uiScale;
            if (Mathf.Abs(dx) < dead && Mathf.Abs(dy) < dead) return false; // 내 몸은 무시(아래 버튼을 씀)
            if (dx >= 0 && dy >= 0) dir = Dir.MinusY;      // ↗
            else if (dx >= 0) dir = Dir.PlusX;             // ↘
            else if (dy < 0) dir = Dir.PlusY;              // ↙
            else dir = Dir.MinusX;                         // ↖
            return true;
        }

        void Tap(Dir dir)
        {
            if (pendingDir.HasValue && pendingDir.Value == dir && Time.time - pendingTime <= DoubleTapSeconds)
            {
                pendingDir = null;
                Step(dir, true);
                return;
            }
            if (pendingDir.HasValue)
            {
                var first = pendingDir.Value;
                pendingDir = null;
                Step(first, false);
                if (!Controls) return;
            }
            pendingDir = dir;
            pendingTime = Time.time;
        }

        void Step(Dir dir, bool running)
        {
            var moves = run.Phase == Phase.Night ? run.Night.Candidates() : run.DayState.Candidates();
            var want = running ? MoveKind.Run : MoveKind.Walk;
            foreach (var m in moves)
                if (m.Kind == want && m.Dir == dir) { Play(m); return; }
            if (!running && run.Phase == Phase.Day && run.Map.TileAt(PlayerPos() + dir.Step()) == Tile.DogHole)
                Toast("개구멍은 빠르게 두 번 눌러 네 발로 지나요");
            else
                Toast(running ? "그쪽으로는 두 칸을 뛸 수 없어요" : "그쪽은 막혀 있어요");
        }

        void ActOrWait(bool act)
        {
            pendingDir = null;
            if (run.Phase == Phase.Night)
                Play(act ? run.Night.ActHere() : new Move { Kind = MoveKind.Wait, Target = run.Night.Player, Middle = run.Night.Player });
            else
                Play(act ? run.DayState.ActHere() : new Move { Kind = MoveKind.Wait, Target = run.DayState.Player, Middle = run.DayState.Player });
        }

        bool CanAct()
        {
            var m = run.Phase == Phase.Night ? run.Night.ActHere() : run.DayState.ActHere();
            return m.Kind == MoveKind.Act;
        }

        void HandleKeys()
        {
            Dir? dir = null;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.E)) dir = Dir.MinusY;   // ↗
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = Dir.PlusX; // ↘
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Z)) dir = Dir.PlusY;  // ↙
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q)) dir = Dir.MinusX; // ↖
            if (Input.GetKeyDown(KeyCode.Space)) { ActOrWait(CanAct()); return; }
            if (!dir.HasValue) return;
            bool runKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            pendingDir = null;
            Step(dir.Value, runKey);
        }

        void FitCamera()
        {
            float aspect = Mathf.Max(0.3f, cam.aspect);
            bool close = mode == Mode.Playing || mode == Mode.Dawn;
            // 가까이: 마름모 7칸쯤이 가로에 들어오게. 지도 보기: 마을 전체.
            float near = Mathf.Max(2.4f, 3.6f / aspect);
            float far = Mathf.Max(3.6f, 6.6f / aspect);
            float size = (zoomOut || !close) ? far : near;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, 0.2f);
            var center = (zoomOut || !close) ? new Vector3(0, -2.75f, 0) : camTarget;
            var target = new Vector3(center.x, center.y - 0.4f, -10f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, 0.2f);
        }

        bool PointerOverHud(Vector3 mouse)
        {
            var gp = new Vector2(mouse.x / uiScale, (Screen.height - mouse.y) / uiScale);
            foreach (var r in hudRects) if (r.Contains(gp)) return true;
            return false;
        }

        // ───────────────────────── 글자 화면 (OnGUI)

        void Styles()
        {
            if (sBody != null) return;
            sBody = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = true };
            sBody.normal.textColor = new Color(0.12f, 0.1f, 0.08f);
            sSmall = new GUIStyle(sBody) { fontSize = 13 };
            sTitle = new GUIStyle(sBody) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sBig = new GUIStyle(sBody) { fontSize = 19, fontStyle = FontStyle.Bold };
            sButton = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            sCaption = new GUIStyle(sBody) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sCaption.normal.textColor = Color.white;
            if (font != null) { sBody.font = sSmall.font = sTitle.font = sBig.font = sButton.font = sCaption.font = font; }
        }

        Rect Panel(Rect r, Color c)
        {
            hudRects.Add(r);
            Rct(r, c);
            return r;
        }

        void Rct(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Disc(float cx, float cy, float rx, float ry, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(cx - rx, cy - ry, rx * 2, ry * 2), Shapes.Circle.texture);
            GUI.color = old;
        }

        /// <summary>가운데 (cx, cy)를 기준으로 deg도 기울인 막대.</summary>
        void Bar(float cx, float cy, float len, float thick, float deg, Color c)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(deg, new Vector2(cx, cy));
            Rct(new Rect(cx - len / 2, cy - thick / 2, len, thick), c);
            GUI.matrix = m;
        }

        /// <summary>게이지: 0~100을 10칸으로(1칸 = 10). 숫자도 함께.</summary>
        void Gauge(Rect r, float v, float max, Color c, string label)
        {
            int cells = Mathf.Max(1, config.GaugeCells);
            float labelW = 40f;
            GUI.Label(new Rect(r.x, r.y - 1, labelW, r.height + 2), label, sSmall);
            float x0 = r.x + labelW, w = r.width - labelW - 30f;
            float cw = w / cells;
            float per = max / cells;
            var old = GUI.color;
            for (int i = 0; i < cells; i++)
            {
                var cell = new Rect(x0 + i * cw + 1, r.y + 2, cw - 2, r.height - 4);
                GUI.color = new Color(0, 0, 0, 0.12f); GUI.DrawTexture(cell, white);
                float fill = Mathf.Clamp01((v - i * per) / per);
                if (fill > 0) { GUI.color = c; GUI.DrawTexture(new Rect(cell.x, cell.y, cell.width * fill, cell.height), white); }
            }
            GUI.color = old;
            GUI.Label(new Rect(x0 + w + 4, r.y - 1, 30, r.height + 2), Mathf.RoundToInt(v).ToString(), sSmall);
        }

        void OnGUI()
        {
            Styles();
            hudRects.Clear();
            uiScale = Mathf.Max(1f, Mathf.Min(Screen.width / 400f, Screen.height / 720f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            float W = Screen.width / uiScale, H = Screen.height / uiScale;
            var paper = new Color(0.98f, 0.96f, 0.90f, 0.94f);

            if (mode == Mode.Cutscene) { DrawCutscene(W, H); return; }

            DrawFade(W, H);

            if (mode == Mode.Title)
            {
                var r = Panel(new Rect(16, H * 0.10f, W - 32, 440), paper);
                GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
                GUILayout.Label("손톱 먹은 쥐", sTitle);
                GUILayout.Space(8);
                GUILayout.Label(
                    "1. 밤에는 쥐가 되어 어둠 속에서 고양이 눈(붉은 칸)을 피해 도령의 손톱을 먹는다. 쥐는 바라보는 방향 앞 3칸만 보인다.\n" +
                    "2. 손톱을 먹으면 도령으로 둔갑한 아침이 된다. 먹은 걸음이 빠를수록 낮 걸음이 최대 3 늘어난다.\n" +
                    "3. 낮에는 남은 걸음 안에 임무 3개(서당 · 정자 · 장승)를 끝낸다. 임무 칸에 서서 「임무하기」를 누른다.\n" +
                    "4. 가고 싶은 쪽을 한 번 누르면 한 칸 걷고, 빠르게 두 번 누르면 두 칸 달린다. 걷기 · 달리기 · 기다리기 · 먹기 · 임무하기 모두 1걸음이다.\n" +
                    "5. 눈에 든 칸을 달리면 의심 +30, 진짜 도령과 마주치면 걸어도 +40. 의심이 100이 되면 쥐불놀이로 끝난다.", sBody);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("시작", sButton, GUILayout.Height(48))) StartRun();
                GUILayout.EndArea();
                return;
            }

            if (mode == Mode.Dawn) { DrawDawn(W, H); return; }

            if (mode == Mode.End)
            {
                var r = Panel(new Rect(16, H * 0.25f, W - 32, 290), paper);
                GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
                GUILayout.Label(run.Ended == EndReason.Win ? "임무 완료" : "실패", sTitle);
                GUILayout.Space(6);
                GUILayout.Label(EndText(run.Ended), sBody);
                GUILayout.Label(ResultLine(), sSmall);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("다시 하기", sButton, GUILayout.Height(44))) RestartAtNight();
                if (GUILayout.Button("처음 화면", sButton, GUILayout.Height(44))) { mode = Mode.Title; run = null; Clear(dynamicRoot); Clear(staticRoot); }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            if (run.NightFailed)
            {
                var r = Panel(new Rect(16, H * 0.28f, W - 32, 210), paper);
                GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
                GUILayout.Label(run.Night.Ended == EndReason.CaughtByCat ? "고양이에게 잡혔다" : "해가 떴다", sTitle);
                GUILayout.Space(6);
                GUILayout.Label(EndText(run.Night.Ended) + "\n밤을 처음부터 다시 한다.", sBody);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("다시 하기", sButton, GUILayout.Height(44))) RetryNight();
                GUILayout.EndArea();
                return;
            }

            DrawQuadrantGuide(W, H);
            DrawHud(W, H, paper);
        }

        void RestartAtNight()
        {
            run.Restart();
            ratFacing = Dir.PlusX;
            pendingDir = null;
            zoomOut = false;
            mode = Mode.Playing;
            fadeStart = Time.time; fadeCol = new Color(0.02f, 0.03f, 0.07f);
            BuildStatic();
            Refresh();
        }

        /// <summary>해가 뜨거나 질 때 화면 덮개가 서서히 걷힙니다.</summary>
        void DrawFade(float W, float H)
        {
            float t = (Time.time - fadeStart) / FadeSeconds;
            if (t < 0 || t >= 1) return;
            Rct(new Rect(0, 0, W, H), new Color(fadeCol.r, fadeCol.g, fadeCol.b, 1f - t));
        }

        // ───────────────────────── 새벽 (먹은 뒤 2초)

        void DrawDawn(float W, float H)
        {
            float t = Mathf.Clamp01((Time.time - dawnStart) / DawnSeconds);
            Rct(new Rect(0, 0, W, H), new Color(1f, 0.90f, 0.62f, Mathf.Lerp(0f, 0.85f, t)));   // 어둠이 걷힌다
            // 웅크린 쥐
            float cx = W / 2, cy = H * 0.52f;
            var grey = new Color(0.62f, 0.60f, 0.58f);
            Disc(cx, cy, 46, 38, grey);
            Disc(cx - 30, cy - 30, 14, 14, grey * 0.9f);
            Disc(cx - 6, cy - 36, 12, 12, grey * 0.9f);
            Bar(cx + 48, cy + 18, 54, 5, -25f, new Color(0.8f, 0.6f, 0.6f));    // 꼬리
            if (t > 0.25f)
            {
                var old = sBig.normal.textColor;
                sBig.normal.textColor = new Color(0.25f, 0.12f, 0.05f);
                sBig.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(0, H * 0.22f, W, 40), "꼬끼오!", sBig);
                sBig.alignment = TextAnchor.UpperLeft;
                sBig.normal.textColor = old;
            }
        }

        // ───────────────────────── 그림 컷 (기획서 5)

        void DrawCutscene(float W, float H)
        {
            if (cutIndex == 0) DrawCutLord(W, H); else DrawCutVow(W, H);

            // 자막 한 줄
            Rct(new Rect(0, H * 0.84f, W, H * 0.16f), new Color(0, 0, 0, 0.62f));
            string line = cutIndex == 0 ? "쌀을 도둑맞은 도령은 고양이를 풀어 놓았다." : "내가 쌀을 구해 오마.";
            GUI.Label(new Rect(16, H * 0.84f, W - 32, H * 0.16f), line, sCaption);

            // 남은 시간 줄
            float t = Mathf.Clamp01((Time.time - cutStart) / CutSeconds);
            Rct(new Rect(0, H * 0.84f - 3, W * t, 3), new Color(1f, 0.85f, 0.4f, 0.9f));

            // 건너뛰기 (탭하면 다음 장)
            var skip = new Rect(W - 96, 12, 84, 34);
            hudRects.Add(skip);
            if (GUI.Button(skip, "건너뛰기", sButton)) EndCutscene();
        }

        /// <summary>그림 1: 쌀 창고 앞, 화난 도령의 큰 그림자와 문 밖으로 튀어 나가는 고양이.</summary>
        void DrawCutLord(float W, float H)
        {
            Rct(new Rect(0, 0, W, H), new Color(0.17f, 0.10f, 0.09f));
            Rct(new Rect(0, H * 0.70f, W, H * 0.30f), new Color(0.10f, 0.07f, 0.06f));
            // 쌀 창고와 열린 문(안쪽에서 새어 나오는 불빛)
            Rct(new Rect(W * 0.46f, H * 0.28f, W * 0.50f, H * 0.42f), new Color(0.30f, 0.20f, 0.15f));
            Rct(new Rect(W * 0.43f, H * 0.21f, W * 0.56f, H * 0.08f), new Color(0.20f, 0.12f, 0.10f));
            Rct(new Rect(W * 0.58f, H * 0.38f, W * 0.22f, H * 0.32f), new Color(0.98f, 0.80f, 0.42f));
            Disc(W * 0.64f, H * 0.66f, W * 0.04f, W * 0.025f, new Color(0.95f, 0.92f, 0.82f));   // 쌀가마
            Disc(W * 0.72f, H * 0.665f, W * 0.04f, W * 0.025f, new Color(0.90f, 0.86f, 0.74f));
            // 도령: 어두운 윗도리와 갓, 찌푸린 눈썹과 붉은 눈
            var ink = new Color(0.04f, 0.03f, 0.04f);
            Rct(new Rect(W * 0.04f, H * 0.46f, W * 0.34f, H * 0.40f), ink);
            Disc(W * 0.21f, H * 0.36f, W * 0.10f, W * 0.10f, ink);
            Rct(new Rect(W * 0.08f, H * 0.255f, W * 0.26f, H * 0.012f), ink);                   // 갓 챙
            Rct(new Rect(W * 0.14f, H * 0.19f, W * 0.14f, H * 0.07f), ink);                      // 갓 윗부분
            Disc(W * 0.17f, H * 0.37f, 6, 6, new Color(1f, 0.25f, 0.15f));
            Disc(W * 0.25f, H * 0.37f, 6, 6, new Color(1f, 0.25f, 0.15f));
            Bar(W * 0.165f, H * 0.345f, 30, 5, 22f, new Color(0.55f, 0.45f, 0.45f));           // 찌푸린 눈썹
            Bar(W * 0.255f, H * 0.345f, 30, 5, -22f, new Color(0.55f, 0.45f, 0.45f));
            Bar(W * 0.21f, H * 0.42f, 34, 5, 0f, new Color(0.55f, 0.20f, 0.18f));              // 앙다문 입
            // 화면을 덮는 도령의 커다란 그림자
            Disc(W * 0.25f, H * 0.35f, W * 0.78f, H * 0.52f, new Color(0f, 0f, 0f, 0.28f));
            // 문 밖으로 튀어 나가는 고양이와 달리는 선
            var cat = new Color(0.65f, 0.45f, 0.80f);
            Disc(W * 0.55f, H * 0.74f, 34, 20, cat);
            Disc(W * 0.47f, H * 0.70f, 17, 15, cat);
            Disc(W * 0.455f, H * 0.665f, 5, 7, cat);
            Disc(W * 0.485f, H * 0.665f, 5, 7, cat);
            Disc(W * 0.46f, H * 0.70f, 3, 3, new Color(1f, 0.9f, 0.3f));
            Disc(W * 0.485f, H * 0.70f, 3, 3, new Color(1f, 0.9f, 0.3f));
            Bar(W * 0.70f, H * 0.72f, 50, 3, 0f, new Color(1f, 1f, 1f, 0.45f));
            Bar(W * 0.72f, H * 0.76f, 60, 3, 0f, new Color(1f, 1f, 1f, 0.35f));
            Bar(W * 0.69f, H * 0.80f, 44, 3, 0f, new Color(1f, 1f, 1f, 0.25f));
        }

        /// <summary>그림 2: 쥐구멍 안의 어미 쥐와 마른 새끼들, 문 앞에서 돌아보며 다짐하는 쥐.</summary>
        void DrawCutVow(float W, float H)
        {
            Rct(new Rect(0, 0, W, H), new Color(0.06f, 0.08f, 0.16f));
            Disc(W * 0.82f, H * 0.14f, 26, 26, new Color(0.93f, 0.92f, 0.80f));                   // 달
            Rct(new Rect(0, H * 0.62f, W, H * 0.38f), new Color(0.13f, 0.10f, 0.08f));           // 땅
            // 쥐구멍: 땅 위로 솟은 둥근 입구와 그 안의 희미한 불빛
            Disc(W * 0.40f, H * 0.62f, W * 0.34f, H * 0.24f, new Color(0.02f, 0.015f, 0.015f));
            Disc(W * 0.40f, H * 0.62f, W * 0.28f, H * 0.19f, new Color(0.20f, 0.14f, 0.09f));
            Rct(new Rect(0, H * 0.62f, W, H * 0.38f), new Color(0.13f, 0.10f, 0.08f));           // 입구의 아래쪽 반을 땅으로 덮음
            Rct(new Rect(W * 0.12f, H * 0.62f, W * 0.56f, H * 0.04f), new Color(0.20f, 0.14f, 0.09f));
            // 어미 쥐(마르고 웅크림)와 새끼 셋
            var mom = new Color(0.58f, 0.55f, 0.52f);
            Disc(W * 0.34f, H * 0.60f, 40, 24, mom);
            Disc(W * 0.255f, H * 0.575f, 15, 14, mom);
            Disc(W * 0.245f, H * 0.545f, 7, 7, mom);
            var pup = new Color(0.70f, 0.67f, 0.62f);
            for (int i = 0; i < 3; i++)
            {
                float px = W * (0.46f + i * 0.07f);
                Disc(px, H * 0.61f, 18, 11, pup);
                Disc(px - 12, H * 0.60f, 7, 7, pup);
                for (int k = 0; k < 3; k++) Bar(px - 4 + k * 6, H * 0.61f, 10, 1.5f, 90f, new Color(0.35f, 0.32f, 0.30f)); // 앙상한 갈비
            }
            // 구멍 밖 문 앞: 돌아보며 다짐하는 쥐
            var me = new Color(0.66f, 0.63f, 0.60f);
            float rx = W * 0.76f, ry = H * 0.60f;
            Disc(rx, ry, 34, 26, me);
            Disc(rx - 30, ry - 20, 16, 15, me);
            Disc(rx - 33, ry - 44, 8, 8, me * 0.9f);
            Disc(rx - 18, ry - 44, 8, 8, me * 0.9f);
            Disc(rx - 37, ry - 22, 3, 3, new Color(0.05f, 0.04f, 0.04f));
            Bar(rx - 37, ry - 29, 12, 3, 20f, new Color(0.2f, 0.17f, 0.15f));                       // 굳은 눈썹
            Bar(rx + 40, ry + 18, 56, 5, 20f, new Color(0.8f, 0.6f, 0.6f));                         // 꼬리
        }

        /// <summary>나를 지나는 가로선·세로선을 옅게 그려 네 구역을 보여 줍니다.</summary>
        void DrawQuadrantGuide(float W, float H)
        {
            if (zoomOut) return;
            var me = cam.WorldToScreenPoint(ToWorld(PlayerPos(), 0.2f));
            float gx = me.x / uiScale, gy = (Screen.height - me.y) / uiScale;
            var old = GUI.color;
            GUI.color = run.Phase == Phase.Night ? new Color(1, 1, 1, 0.12f) : new Color(0, 0, 0, 0.10f);
            for (float x = 0; x < W; x += 14) GUI.DrawTexture(new Rect(x, gy, 8, 1.5f), white);
            for (float y = 0; y < H; y += 14) GUI.DrawTexture(new Rect(gx, y, 1.5f, 8), white);
            GUI.color = old;
        }

        void DrawHud(float W, float H, Color paper)
        {
            bool night = run.Phase == Phase.Night;
            var top = Panel(new Rect(8, 8, W - 16, night ? 84 : 126), paper);
            float x = top.x + 10, y = top.y + 6, w = top.width - 20;
            if (night)
            {
                var n = run.Night;
                GUI.Label(new Rect(x, y, w, 24), "밤 · 도령의 손톱 찾기" + (run.NightTries > 1 ? " (" + run.NightTries + "번째)" : ""), sBig); y += 28;
                GUI.Label(new Rect(x, y, w, 20), "남은 걸음 <b>" + n.StepsLeft + "</b>/" + n.Config.NightSteps + " · 붉은 칸은 고양이 눈", sSmall); y += 20;
                GUI.Label(new Rect(x, y, w, 20), n.CanEat ? "<b>손톱 위다. 「먹기」를 누르세요.</b>" : "노란 표시의 손톱 칸까지 가서 먹으세요.", sSmall);
            }
            else
            {
                var d = run.DayState;
                GUI.Label(new Rect(x, y, w, 24), "낮 · 도령으로 둔갑", sBig); y += 28;
                Gauge(new Rect(x, y, w, 18), d.Suspicion, d.Config.MaxSuspicion, new Color(0.85f, 0.25f, 0.2f), "의심");
                y += 24;
                GUI.Label(new Rect(x, y, w, 20), "남은 걸음 <b>" + d.StepsLeft + "</b>/" + d.StepLimit + (run.Bonus > 0 ? " (밤 보너스 +" + run.Bonus + ")" : ""), sSmall); y += 20;
                for (int i = 0; i < d.As.Missions.Length; i++)
                {
                    string mark = d.Done[i] ? "■ " : "□ ";
                    GUI.Label(new Rect(x + (i % 3) * (w / 3), y, w / 3, 20), mark + d.As.MissionNames[i], sSmall);
                }
            }

            // 화면 아래 버튼: 기다리기는 늘, 먹기 · 임무하기는 그 칸 위에서만(기획서 2.1)
            float bh = 52;
            var bottom = Panel(new Rect(8, H - bh - 16, W - 16, bh + 8), new Color(0, 0, 0, 0));
            bool canAct = CanAct();
            float mapW = bottom.width * 0.26f;
            float left = bottom.width - mapW - 6;
            float aw = left * 0.62f, ww = left - aw - 6;
            string actLabel = night ? run.Night.ActLabel() : run.DayState.ActLabel();
            var oldColor = GUI.color;
            if (!canAct) GUI.color = new Color(1, 1, 1, 0.35f);                     // 비활성(흐릿한 색)
            bool actPressed = GUI.Button(new Rect(bottom.x, bottom.y, aw, bh), night ? "먹기" : "임무하기", sButton);
            GUI.color = oldColor;
            if (actPressed && canAct) ActOrWait(true);
            if (GUI.Button(new Rect(bottom.x + aw + 6, bottom.y, ww, bh), "기다리기", sButton)) ActOrWait(false);
            if (canAct && !night)
            {
                // 임무 이름은 버튼 위에 작게
                GUI.Label(new Rect(bottom.x, bottom.y - 20, aw, 20), actLabel, sSmall);
            }
            if (GUI.Button(new Rect(bottom.x + bottom.width - mapW, bottom.y, mapW, bh), zoomOut ? "가까이" : "지도", sButton))
                zoomOut = !zoomOut;

            if (Time.time < toastUntil && toasts.Count > 0)
            {
                var tr = new Rect(16, H - bh - 30 - 22 * toasts.Count - 20, W - 32, 22 * toasts.Count + 8);
                var old = GUI.color; GUI.color = new Color(0.1f, 0.08f, 0.06f, 0.8f); GUI.DrawTexture(tr, white); GUI.color = old;
                var st = new GUIStyle(sSmall); st.normal.textColor = Color.white;
                for (int i = 0; i < toasts.Count; i++) GUI.Label(new Rect(tr.x + 8, tr.y + 4 + 22 * i, tr.width - 16, 22), toasts[i], st);
            }
        }

        string ResultLine()
        {
            var d = run.DayState;
            if (d == null) return "";
            return "밤 " + run.NightTries + "번째에 성공 · 낮 걸음 " + d.StepsUsed + "/" + d.StepLimit + " · 의심 " + d.Suspicion;
        }

        static string EndText(EndReason r)
        {
            switch (r)
            {
                case EndReason.Win: return "도령으로 둔갑한 쥐는 임무 세 개를 모두 끝냈다.";
                case EndReason.Suspicion: return "마을 사람들이 눈치챘다. 의심 100, 쥐불놀이가 되었다.";
                case EndReason.StepsOut: return "임무를 다 못 했는데 걸음이 다했다. 둔갑이 풀려 버렸다.";
                case EndReason.CaughtByCat: return "고양이 눈에 들어 잡혔다.";
                case EndReason.Sunrise: return "손톱을 못 먹었는데 해가 떴다.";
                default: return "";
            }
        }
    }
}
