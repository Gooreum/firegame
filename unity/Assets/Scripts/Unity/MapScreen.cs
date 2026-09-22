using FireGame.Core.Game;
using UnityEngine;
using UnityEngine.UI;

namespace FireGame.UnityLayer
{
    /// <summary>
    /// 출동 지도. 섬 지도 위에 현장 노드를 올리고, 신고가 들어온 현장을 표시한다.
    ///
    /// 지도 그림(tools/import-art.py가 합성)과 현장 좌표(Campaign.MapX/MapY)가 같은 1920x1080 기준이라,
    /// 지도판을 그 크기로 고정해 가운데 둔다. 더 넓은 화면에서는 양옆이 바다색으로 채워진다.
    /// </summary>
    public sealed class MapScreen
    {
        /// <summary>지도 그림에서 소방서(길의 시작점) 위치. import-art.py의 STATION과 같다.</summary>
        private static readonly Vector2 Station = new Vector2(0.10f, 0.16f);

        public static readonly Color Sea = new Color(166f / 255f, 225f / 255f, 245f / 255f);

        /// <summary>
        /// 별 아이콘. 빈 별은 외곽선 별을 어둡게 칠해 쓴다.
        /// 원래 색 그대로면 밝은 패널 위에서 안 보이고, 노란 별에 회색을 곱하면 "어두운 금별"이 돼
        /// 별을 딴 것처럼 보인다.
        /// </summary>
        public static Image StarIcon(Transform parent, string name, bool earned)
        {
            return earned
                ? UiKit.Image(parent, name, Art.Get("UI/star"), Color.white)
                : UiKit.Image(parent, name, Art.Get("UI/star_empty"), new Color(0.55f, 0.57f, 0.63f));
        }

        private readonly RectTransform _root;

        public MapScreen(Canvas canvas, GameFlow flow)
        {
            _root = UiKit.Stretch(UiKit.Node(canvas.transform, "MapScreen"));

            Image sea = UiKit.Image(_root, "Sea", Art.White, Sea);
            UiKit.Stretch(sea.rectTransform);

            RectTransform board = UiKit.Node(_root, "Board");
            UiKit.Place(board, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(UiKit.ReferenceWidth, UiKit.ReferenceHeight));
            board.pivot = new Vector2(0.5f, 0.5f);

            Image map = UiKit.Image(board, "Map", Art.Get("Map/map_background"), Color.white);
            UiKit.Stretch(map.rectTransform);

            SaveData save = flow.Save;
            MissionDef nextCall = Readiness.NextCall(save);

            for (int i = 0; i < Campaign.Missions.Length; i++)
            {
                BuildNode(board, flow, Campaign.Missions[i], i, nextCall);
            }

            // 소방차는 마지막으로 해결한 현장에 서 있다. 아직 없으면 소방서에.
            Vector2 truckAt = Station;
            for (int i = 0; i < Campaign.Missions.Length; i++)
            {
                if (save.StarsFor(Campaign.Missions[i].Id) > 0) truckAt = new Vector2(Campaign.Missions[i].MapX, Campaign.Missions[i].MapY);
            }

            Image truck = UiKit.Image(board, "Truck", Art.Get("Vehicles/firetruck"), Color.white);
            UiKit.Place(truck.rectTransform, Vector2.zero, ToBoard(truckAt) + new Vector2(-70f, 10f), new Vector2(60f, 104f));
            truck.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            truck.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);

            BuildTopBar(save);

            UnityEngine.UI.Button shop = UiKit.Button(_root, "ShopButton", Art.Get("UI/button_yellow"), "소방서 상점", 40, flow.OpenShop);
            UiKit.Place((RectTransform)shop.transform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(360f, 120f));

            // 살 수 있는 게 있으면 상점 버튼에 숫자를 단다. 돈이 모였다는 걸 지도에서 바로 알게.
            int affordable = Readiness.AffordableUpgrades(save);
            if (affordable > 0)
            {
                Image badge = UiKit.Image(shop.transform, "Badge", Art.Get("UI/button_red"), Color.white);
                UiKit.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(20f, 26f), new Vector2(72f, 72f));
                Text count = UiKit.OutlinedLabel(badge.transform, "Count", affordable.ToString(), 36, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch(count.rectTransform);
                count.rectTransform.offsetMin = new Vector2(0f, 6f);
            }
        }

        public void Destroy()
        {
            UiKit.Discard(_root.gameObject);
        }

        private static Vector2 ToBoard(Vector2 normalized)
        {
            return new Vector2(normalized.x * UiKit.ReferenceWidth, normalized.y * UiKit.ReferenceHeight);
        }

        private void BuildNode(RectTransform board, GameFlow flow, MissionDef mission, int index, MissionDef nextCall)
        {
            SaveData save = flow.Save;
            bool unlocked = save.IsMissionUnlocked(mission);
            int stars = save.StarsFor(mission.Id);
            Vector2 at = ToBoard(new Vector2(mission.MapX, mission.MapY));

            RectTransform node = UiKit.Node(board, "Mission" + mission.Id);
            UiKit.Place(node, Vector2.zero, at, new Vector2(300f, 300f));
            node.pivot = new Vector2(0.5f, 0.5f);

            // 번호 표지판. 잠긴 현장은 회색으로.
            int captured = mission.Id;
            UnityEngine.UI.Button marker = UiKit.Button(node, "Marker", Art.Get("Map/node_" + (index + 1)), string.Empty, 0, () => flow.SelectMission(captured));
            RectTransform markerRect = (RectTransform)marker.transform;
            UiKit.Place(markerRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            ((Image)marker.targetGraphic).color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            marker.interactable = unlocked;

            // 이름표: 위치 + 제목, 그 아래 별
            Image tag = UiKit.Image(node, "Tag", Art.Get("UI/panel_grey"), new Color(1f, 1f, 1f, unlocked ? 0.95f : 0.7f));
            UiKit.Place(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(300f, 110f));
            tag.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            Text title = UiKit.Label(tag.transform, "Title", unlocked ? mission.Title : "잠김", 30, UiKit.Ink, TextAnchor.UpperCenter);
            UiKit.Stretch(title.rectTransform);
            title.rectTransform.offsetMax = new Vector2(0f, -10f);

            Text place = UiKit.Label(tag.transform, "Place", mission.Location, 22, new Color(0.35f, 0.35f, 0.4f), TextAnchor.UpperCenter);
            UiKit.Stretch(place.rectTransform);
            place.rectTransform.offsetMax = new Vector2(0f, -46f);

            for (int s = 0; s < StarRating.MaxStars; s++)
            {
                Image star = StarIcon(tag.transform, "Star" + s, s < stars);
                UiKit.Place(star.rectTransform, new Vector2(0.5f, 0f), new Vector2((s - 1) * 40f, 8f), new Vector2(34f, 32f));
                star.rectTransform.pivot = new Vector2(0.5f, 0f);
            }

            BuildReadinessBand(node, save, mission, unlocked, stars);

            if (mission == nextCall)
            {
                // 신고가 들어온 현장을 눈에 띄게 한다. 처음 하는 사람이 어디를 눌러야 할지 바로 알게.
                Image badge = UiKit.Image(node, "Alert", Art.Get("UI/button_red"), Color.white);
                UiKit.Place(badge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(200f, 64f));
                badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                Text alert = UiKit.OutlinedLabel(badge.transform, "Label", "신고 접수!", 30, Color.white, TextAnchor.MiddleCenter);
                UiKit.Stretch(alert.rectTransform);
                alert.rectTransform.offsetMin = new Vector2(0f, 6f);
            }
        }

        /// <summary>
        /// 이름표 아래 띠: 장비가 모자라면 빨강 "장비 부족", 새 신고인데 준비됐으면 초록 "출동 준비 완료".
        /// 이미 깬 현장은 준비됐으면 띠를 달지 않는다(지도를 어지럽히지 않게).
        /// </summary>
        private static void BuildReadinessBand(RectTransform node, SaveData save, MissionDef mission, bool unlocked, int stars)
        {
            if (!unlocked) return;

            bool ready = Readiness.IsReady(save, mission);
            if (ready && stars > 0) return;

            Image band = UiKit.Image(node, "Readiness", Art.Get(ready ? "UI/button_green" : "UI/button_red"), Color.white);
            UiKit.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(240f, 50f));
            band.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Text text = UiKit.OutlinedLabel(band.transform, "Label", ready ? "출동 준비 완료" : "장비 부족", 26, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(0f, 5f);
        }

        private void BuildTopBar(SaveData save)
        {
            Image bar = UiKit.Image(_root, "TopBar", Art.Get("UI/panel_grey"), new Color(1f, 1f, 1f, 0.95f));
            UiKit.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(620f, 96f));

            Text title = UiKit.Label(bar.transform, "Title", "출동 지도", 42, UiKit.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 4f), new Vector2(240f, 70f));

            Text money = UiKit.Label(bar.transform, "Money", "보유금 " + Format.Money(save.Money), 32, new Color(0.15f, 0.5f, 0.2f), TextAnchor.MiddleLeft);
            UiKit.Place(money.rectTransform, new Vector2(0f, 0.5f), new Vector2(260f, 4f), new Vector2(240f, 70f));

            Image star = UiKit.Image(bar.transform, "Star", Art.Get("UI/star"), Color.white);
            UiKit.Place(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(500f, 6f), new Vector2(40f, 38f));

            Text total = UiKit.Label(bar.transform, "Stars", save.TotalStars.ToString(), 32, UiKit.Ink, TextAnchor.MiddleLeft);
            UiKit.Place(total.rectTransform, new Vector2(0f, 0.5f), new Vector2(548f, 4f), new Vector2(70f, 70f));
        }
    }

    /// <summary>화면에 쓰는 숫자 표기.</summary>
    public static class Format
    {
        public static string Money(int amount)
        {
            return "$" + amount.ToString("N0");
        }

        /// <summary>"CO2 소화기 Lv2"</summary>
        public static string Requirement(int trackId, int level)
        {
            UpgradeTrack track = UpgradeCatalog.ById(trackId);
            return (track != null ? track.Name : "장비") + " Lv" + level;
        }

        /// <summary>모자란 장비들을 "CO2 소화기 Lv2 · 폼 소화기 Lv3"처럼 잇는다.</summary>
        public static string Shortfalls(System.Collections.Generic.List<Shortfall> missing)
        {
            var parts = new string[missing.Count];
            for (int i = 0; i < missing.Count; i++) parts[i] = Requirement(missing[i].TrackId, missing[i].RequiredLevel);
            return string.Join(" · ", parts);
        }
    }
}
