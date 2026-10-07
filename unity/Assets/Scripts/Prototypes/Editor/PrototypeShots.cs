using System;
using System.Collections.Generic;
using System.IO;
using FireGame.Core.Grid;
using FireGame.Prototypes.Logic;
using FireGame.UnityLayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FireGame.Prototypes.EditorTools
{
    /// <summary>
    /// 시험판 화면을 배치 모드에서 PNG로 찍는다. 본 게임 하니스와 따로 둬서 시험판을 버릴 때 같이 지운다.
    /// 실행: tools/unity-check.sh proto-shots
    /// </summary>
    public static class PrototypeShots
    {
        private const int Width = 1920;
        private const int Height = 1080;

        public static void CaptureAll()
        {
            string dir = ArgValue("-shotDir") ?? Path.Combine(Application.dataPath, "../../tools/.shots-proto");
            Directory.CreateDirectory(dir);
            int failures = 0;

            failures += Shot(dir, "b1_level1_start", view => { });
            failures += Shot(dir, "b2_level2_spray_aim", view =>
            {
                view.LoadLevel(1);
                view.Game.Do(TAction.Move, new GridPoint(5, 4));
                view.Game.Do(TAction.Move, new GridPoint(5, 2));
                view.Game.EndTurn();
                view.Game.Do(TAction.Move, new GridPoint(5, 4));
                view.Game.Do(TAction.Move, new GridPoint(3, 4));
                view.Game.EndTurn();
                view.AimSpray(Dir.W);
            });
            failures += Shot(dir, "b3_level3_gas", view =>
            {
                view.LoadLevel(2);
                for (int i = 0; i < 6; i++) view.Game.EndTurn();
            });

            failures += ActionShot(dir, "a1_action_fight", 7f);
            failures += ActionShot(dir, "a2_action_later", 16f);

            // 동네 전체를 한 화면에: 가게 아홉 채가 저마다 알아볼 수 있게 그려졌는지 본다.
            failures += SurvivorShot(dir, "c0_town", view => view.Sim.Time >= 1f, 5, false, view => view.Frame(new Vector3(SurvivorSim.ArenaSize / 2f, 29f, 0f), 14f));
            failures += SurvivorShot(dir, "c1_early", view => view.Sim.Time >= 40f);
            // 레벨 5로 오르는 카드: 노란 특수 장비가 반드시 한 장 있다.
            failures += SurvivorShot(dir, "c2_levelup_cards", view => view.Sim.Level == 5 && view.Sim.PendingChoices != null, 45);
            // 진화·MAX 순간은 판 흐름에 따라 안 올 수 있어, 물대포 Lv4 + 고압 펌프로 시작한다(카드 고르기 경로).
            failures += SurvivorShot(dir, "c4_evolved", view => view.Sim.JustEvolved, 9, false, null, 1, view => Pick(view, UpgradeId.Hose, UpgradeId.Hose, UpgradeId.Hose, UpgradeId.Hose, UpgradeId.Hose, UpgradeId.Tank));
            failures += SurvivorShot(dir, "c5_levelup_burst", view => view.Sim.Time >= 30f && view.Sim.JustLeveled, 8);
            failures += SurvivorShot(dir, "c6_arsenal", view => view.Sim.Time >= 150f && view.Sim.PendingChoices == null);
            // 보는 용도: 봇이 잘 안 고르는 아이템까지 전부 최대 레벨로 쥐여 주고 레벨별 연출을 한 화면에서 본다.
            failures += SurvivorShot(dir, "c7_gear_maxed", view => view.Sim.Time >= 25f && view.Sim.PendingChoices == null, 20, true);

            // 동네: 불난 가게에 사람이 갇혀 있고 소방관이 가까이 있는 장면, 그리고 판이 끝난 결과창.
            failures += SurvivorShot(dir, "c8_house_fire", view => view.Sim.Structures.Exists(s => s.IsBuilding && s.Burning && s.Residents > 0 && s.DistanceTo(view.Sim.Player) < 6f), 30);
            failures += SurvivorShot(dir, "c9_result", view => view.Sim.Outcome != SOutcome.Playing, 90);
            // 호스를 쥔 손과 물줄기를 확대: 노즐이 옆구리에 있고 물이 끝까지 한 줄로 이어지는지 본다.
            failures += SurvivorShot(dir, "c11_hose_closeup", view => view.Sim.Time >= 12f && view.Sim.Spraying && view.Sim.PendingChoices == null && view.Sim.Shots.FindAll(s => s.Hose).Count >= 6, 2, false, view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y, 0f), 4.5f));

            failures += TouchShot(dir, "c12_touch_sticks");
            failures += AimShot(dir, "c13_aim_assist");
            // 무기·보조가 Lv5가 되는 순간(금빛 기둥, "○○ MAX!").
            failures += SurvivorShot(dir, "c14_max_burst", view => view.Sim.JustMaxed.HasValue, 8, false, null, 1, view => Pick(view, UpgradeId.Hose, UpgradeId.Hose, UpgradeId.Hose, UpgradeId.Hose));
            failures += NextStageShot(dir, "c14b_next_stage");
            // 공구상자를 주워 건물을 고치는 순간(초록 빛줄기·"수리!").
            // 건물 하나를 미리 상하게 해 첫 40초에 상자가 떨어지게 하고, 떨어지면 소방관을 3칸 옆으로 옮긴다(봇은 갇힌 사람을 먼저 가느라 상자를 못 볼 때가 있다).
            failures += SurvivorShot(dir, "c15_toolbox", view =>
            {
                if (view.Sim.Toolboxes.Count > 0 && view.Sim.PendingChoices == null && view.Sim.Toolboxes[0].Pos.DistanceTo(view.Sim.Player) >= 5f)
                    view.Sim.Player = new Vec2(view.Sim.Toolboxes[0].Pos.X + 3f, view.Sim.Toolboxes[0].Pos.Y);
                return view.Sim.Toolboxes.Exists(b => b.Pos.DistanceTo(view.Sim.Player) < 5f);
            }, 5, false, null, 1, view => NearestHouse(view).Integrity = 0.6f);
            // 줍는 순간만 보려고, 공구상자가 떨어지면 소방관을 그 위로 옮긴다.
            failures += SurvivorShot(dir, "c15b_toolbox_repair", view =>
            {
                if (view.Sim.Toolboxes.Count > 0 && view.Sim.PendingChoices == null) view.Sim.Player = view.Sim.Toolboxes[0].Pos;
                return view.Sim.Repaired.Count > 0 || view.Sim.JustPickedToolbox;
            }, 12);
            // 2스테이지 산불 숲: 흙길·소나무, 불다람쥐, 막 날아온 재 박쥐 떼, 바람 화살표.
            failures += SurvivorShot(dir, "c17_forest", view => view.Sim.Time >= 40.5f && view.Sim.Enemies.Exists(e => (e.Kind == EnemyKind.Bat || e.Kind == EnemyKind.Squirrel) && e.Pos.DistanceTo(view.Sim.Player) < 6f), 10, false, null, 2);
            // 대형 신고(1:20): 갇힌 사람 얼굴 줄, "대형 화재!" 띠, 화면 밖이면 붉은 화살표.
            failures += SurvivorShot(dir, "c18_big_report", view => view.Sim.BigReport != null && view.Sim.Time >= view.Sim.BigTimes[0] + 0.6f, 4, pro: true);
            // 대형 신고를 다 구해 떨어진 보물상자, 그리고 상자가 열리며 카드가 뜨는 순간.
            // 상자는 한 명도 잃지 않고 다 구해야 나온다: 봇에게 맡기면 시드 따라 못 받으니 대형 신고가 뜨면 소방관을 문 앞에 세운다.
            failures += SurvivorShot(dir, "c18b_chest", view =>
            {
                // 바닥에 놓인 상자를 보려고: 소방관이 문에서 떨어져 있을 때 불을 꺼 버려 상자가 떨어지게 한다(줍기 범위 = 구조 범위라 문 앞에선 바로 줍는다).
                Structure big = view.Sim.BigReport;
                if (big != null && big.Burning && big.Door.DistanceTo(view.Sim.Player) > 2f) big.Fire = 0f;
                return view.Sim.Chests.Count > 0;
            }, 10, pro: true);
            // 여는 순간만 보려고, 상자가 떨어지면 소방관을 상자 위로 옮긴다(다음 틱에 줍는다).
            failures += SurvivorShot(dir, "c18c_chest_open", view =>
            {
                HoldBigReportDoor(view);
                if (view.Sim.Chests.Count > 0 && view.Sim.PendingChoices == null) view.Sim.Player = view.Sim.Chests[0].Pos;
                return view.Sim.JustChest;
            }, 45, pro: true);
            // 대화재(3:00~): 붉은 가장자리, 남은 시간 막대와 랜드마크 갇힌 사람 수.
            failures += SurvivorShot(dir, "c20_finale", view => view.Sim.Finale && view.Sim.Time >= SurvivorSim.FinaleAt + 2f, 5);
            // 불 규칙: 크게 타는 빵집이 옆 가게로 번지는 순간(빨간 알림 + 두 건물을 잇는 불길). 두 건물 가운데를 본다.
            failures += SurvivorShot(dir, "c26_fire_spread", view =>
            {
                // 봇이 빵집을 끄기 전에 번져야 한다: 번질 때까지 소방관을 빵집 반대편에 둔다.
                if (view.Sim.Spread.Count == 0)
                {
                    Structure bakery = view.Sim.Structures.Find(st => st.Name == "빵집");
                    if (bakery != null) view.Sim.Player = new Vec2(SurvivorSim.ArenaSize - bakery.Pos.X, SurvivorSim.ArenaSize - bakery.Pos.Y);
                }
                return view.Sim.Spread.Count > 0;
            }, 4, false, view =>
            {
                Structure from = view.Sim.SpreadFrom[0];
                Structure to = view.Sim.Spread[0];
                view.Frame(new Vector3((from.Pos.X + to.Pos.X) / 2f, (from.Pos.Y + to.Pos.Y) / 2f, 0f), 12f);
            }, 1, view => view.Sim.Ignite(view.Sim.Structures.Find(st => st.Name == "빵집"), 1f));
            // 소방서: 켜면 먼저 보이는 화면(새 소방서)과, 별 5개로 드론 담당을 해금한 직후.
            failures += StationShot(dir, "c33_station", null);
            // 맵 특색: 숲은 통나무 동네, 항구는 창고·등대 부두, 야시장은 천막 점포 골목(Kenney 주택이 안 보여야 한다).
            failures += SurvivorShot(dir, "c60_forest_camp", view => view.Sim.Time > 0.5f, 0, false, view => view.Frame(new Vector3(30f, 20f, 0f), 14f), 2);
            failures += SurvivorShot(dir, "c61_harbor_quay", view => view.Sim.Time > 0.5f, 0, false, view => view.Frame(new Vector3(30f, 32f, 0f), 14f), 4);
            failures += SurvivorShot(dir, "c62_market_stalls", view => view.Sim.Time > 0.5f, 0, false, view => view.Frame(new Vector3(30f, 30f, 0f), 16f), 5);
            // 야시장 밝기·특색: 밝아진 밤 골목(c62와 비교)과 색 네온 간판, 무대 스포트라이트.
            failures += SurvivorShot(dir, "c71_market_night", view => view.Sim.Time > 1f, 0, false, view => view.Frame(new Vector3(30f, 30f, 0f), 18f), 5);
            failures += SurvivorShot(dir, "c72_stage_lights", view => view.Sim.Time > 1f, 0, false, view => view.Frame(new Vector3(30f, 11f, 0f), 10f), 5);
            // 가게 디테일: 마을 가게가 지붕 상징물·앞마당 진열로 이름표 없이도 읽히는가(서쪽·동쪽 블록, 편의점 근접).
            failures += SurvivorShot(dir, "c73_town_west", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(17f, 37f, 0f), 10f));
            failures += SurvivorShot(dir, "c74_town_east", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(43f, 37f, 0f), 10f));
            failures += SurvivorShot(dir, "c75_convenience_close", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(22f, 36.8f, 0f), 4.5f));
            failures += SurvivorShot(dir, "c75b_barber_close", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(12f, 35.5f, 0f), 4.5f));
            // 야시장 좌판: 과일 노점에 과일이, 점포마다 이름에 맞는 물건이 보이는가(차양이 좌판을 안 가린다).
            failures += SurvivorShot(dir, "c76_market_goods", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(45f, 38.8f, 0f), 4.5f), 5);
            failures += SurvivorShot(dir, "c77_market_row", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(30f, 20.5f, 0f), 11f), 5);
            failures += SurvivorShot(dir, "c77b_market_food", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(15f, 39f, 0f), 6f), 5);
            // 같은 종류 건물도 이름별 앞마당으로 구별되는가(숲 통나무집·항구 창고·공단 공장).
            failures += SurvivorShot(dir, "c78_forest_yards", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(17f, 25f, 0f), 7f), 2);
            failures += SurvivorShot(dir, "c79_harbor_yards", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(30f, 33f, 0f), 11f), 4);
            failures += SurvivorShot(dir, "c80_factory_yards", view => view.Sim.Time > 3.5f, 0, false, view => view.Frame(new Vector3(14f, 36f, 0f), 7f), 3);
            // 물 손맛: 쥔 동안 굵어진 줄기·달리는 결·끝 무지개·바닥 물길(낮 마을, 노즐 근접).
            failures += SurvivorShot(dir, "c81_spray_pressure", view => view.Sim.Time >= 4f && view.Sim.PendingChoices == null, 0, false, view =>
            {
                // 빈 땅에서 왼쪽 아래로 길게 쏜 채 0.7초: 굵어진 줄기·달리는 결·끝 무지개·바닥 물길이 보인다.
                view.Sim.Player = new Vec2(26f, 28f);
                for (int i = 0; i < 42; i++)
                {
                    view.Sim.Enemies.Clear();
                    view.Sim.Spraying = true;
                    view.Sim.Aim = new Vec2(-1f, -0.45f);
                    view.Step(new Vec2(0f, 0f));
                    view.Refresh(SurvivorSim.Dt);
                }
                view.Frame(new Vector3(23f, 28f, 0f), 5f);
            });
            // 건물에 물이 맞는 동안: 물 왕관·치익 김·벽 타고 흐르는 물·눌린 지붕 불꽃. 끄는 순간: 히트스톱·솟는 물 왕관·무지개 반짝이.
            // 수호자 마을 샘플(docs §20): 수호 반경·쉼터·잿더미 둥지·결과 한 장면·소방서 얼굴.
            failures += SurvivorShot(dir, "g80_guard_radius", view => view.Sim.Time >= 30f && view.Sim.PendingChoices == null, 4, false,
                view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y, 0f), 8f), 1, null, true, true);
            failures += SurvivorShot(dir, "g83_haven", view => view.Sim.Haven != null && view.Sim.PendingChoices == null, 6, false,
                view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y, 0f), 6f), 1,
                view => view.Sim.Hp = view.Sim.MaxHp * 0.5f, false, true);
            failures += SurvivorShot(dir, "g84_ruin_nest", view => view.Sim.Time >= 12f && view.Sim.PendingChoices == null, 0, false,
                view =>
                {
                    Structure ruin = view.Sim.Structures.Find(s => s.IsBuilding && s.Collapsed);
                    view.Frame(new Vector3(ruin.Pos.X, ruin.Pos.Y - 1f, 0f), 6f);
                }, 1, view => Ruin(view), true, true);
            failures += SurvivorShot(dir, "g85_result_crowd", view => view.Sim.Outcome != SOutcome.Playing, 66, false, null, 1, view => DayEnd(view), true, true);
            failures += SurvivorShot(dir, "g86_result_pullback", view => view.Sim.Outcome != SOutcome.Playing, 240, false, null, 1, view => DayEnd(view), true, true);
            failures += SurvivorShot(dir, "g87_station_face", view => view.Sim.Outcome != SOutcome.Playing, 240, false, view => view.OpenStation(), 1, view => DayEnd(view), true, true);
            failures += SurvivorShot(dir, "c82_hose_on_building", view => view.Sim.Time >= 4f && view.Sim.PendingChoices == null, 0, false, view => HoseBuilding(view, false));
            failures += SurvivorShot(dir, "c83_douse_moment", view => view.Sim.Time >= 4f && view.Sim.PendingChoices == null, 0, false, view => HoseBuilding(view, true));
            // 항구 할 일 가독성: 불배가 떠 있는 동안 띠·부두 끝 "요격 지점" 고리·"N초 뒤 접안"·발밑 화살표.
            failures += SurvivorShot(dir, "c70_harbor_guide", view => view.Sim.Structures.Exists(s => s.Kind == StructureKind.Boat && !s.Docked && s.Burning && !s.Tanker && view.Sim.BoatEta(s) < 12f), 1, false,
                view =>
                {
                    Structure boat = view.Sim.Structures.Find(s => s.Kind == StructureKind.Boat && !s.Docked && s.Burning && !s.Tanker);
                    float x = Mathf.Clamp(boat != null ? boat.Pos.X : 30f, 14f, 46f);
                    view.Frame(new Vector3(x, 44f, 0f), 12f);
                }, 4, null, true, true);
            // 대화재 다섯 종류: 숲 불 전선(폭 60 불의 띠), 공단 연쇄 폭발(드럼 초읽기), 항구 유조선(부두 가운데·불기름), 야시장 불꽃 폭주(가판대 전부·등줄).
            failures += SurvivorShot(dir, "c66_forest_front", view => view.Sim.Finale && view.Sim.Time >= SurvivorSim.FinaleAt + 2.5f, 2, true,
                view => view.Frame(new Vector3(SurvivorSim.ArenaSize / 2f, view.Sim.FrontY ?? 34f, 0f), 15f), 2, null, true, true);
            failures += SurvivorShot(dir, "c67_factory_chain", view => view.Sim.Finale && view.Sim.Structures.Exists(s => s.Kind == StructureKind.Gas && s.Fuse > SurvivorSim.GasFuse), 2, true,
                view =>
                {
                    Structure drum = view.Sim.Structures.Find(s => s.Kind == StructureKind.Gas && s.Fuse > 0f);
                    Vec2 at = drum != null ? drum.Pos : view.Sim.Player;
                    view.Frame(new Vector3(Mathf.Clamp(at.X, 12f, 48f), Mathf.Clamp(at.Y, 10f, 50f), 0f), 9f);
                }, 3, null, true, true);
            failures += SurvivorShot(dir, "c68_harbor_tanker", view => view.Sim.TankerBoat != null && view.Sim.TankerBoat.Docked && view.Sim.Time >= SurvivorSim.FinaleAt + 40f, 2, true,
                view =>
                {
                    Vec2 at = view.Sim.TankerBoat != null ? view.Sim.TankerBoat.Pos : new Vec2(30f, 40f);
                    view.Frame(new Vector3(Mathf.Clamp(at.X, 14f, 46f), at.Y - 3f, 0f), 11f);
                }, 4, null, true, true);
            failures += SurvivorShot(dir, "c69_market_storm", view => view.Sim.Finale && view.Sim.Lanterns.Exists(l => l.Storm && l.Burn > 0.2f), 2, true,
                view =>
                {
                    Lantern line = view.Sim.Lanterns.Find(l => l.Storm && l.Burn > 0f);
                    Vec2 at = line != null ? view.Sim.LanternFire(line) : view.Sim.Landmark != null ? view.Sim.Landmark.Pos : view.Sim.Player;
                    view.Frame(new Vector3(Mathf.Clamp(at.X, 12f, 48f), Mathf.Clamp(at.Y, 10f, 50f), 0f), 11f);
                }, 5, null, true, true);
            // 맵 특색 몹: 갈매기가 지붕에 불을 떨어뜨린 직후(갈매기 실루엣·떨어뜨렸다 글자), 게가 상륙한 순간(목표 고리), 풍등이 점포 가까이(종이등·바닥 빛·고리).
            failures += SurvivorShot(dir, "c63_gull_bomb", view => view.Sim.Enemies.Exists(e => e.Kind == EnemyKind.Gull && e.Dropped), 8, false,
                view =>
                {
                    Enemy g = view.Sim.Enemies.Find(e => e.Kind == EnemyKind.Gull && e.Dropped) ?? view.Sim.Enemies.Find(e => e.Kind == EnemyKind.Gull);
                    Vec2 at = g != null ? g.Pos : view.Sim.Player;
                    view.Frame(new Vector3(Mathf.Clamp(at.X, 12f, 48f), at.Y - 3f, 0f), 9f);
                }, 4, null, true, true);
            failures += SurvivorShot(dir, "c64_crab", view => view.Sim.Enemies.Exists(e => e.Kind == EnemyKind.Crab && e.Pos.Y < SurvivorHarbor.SeaFrom - 0.5f), 1, false,
                view =>
                {
                    Enemy c = view.Sim.Enemies.Find(e => e.Kind == EnemyKind.Crab);
                    Vec2 at = c != null ? c.Pos : view.Sim.Player;
                    view.Frame(new Vector3(Mathf.Clamp(at.X, 12f, 48f), at.Y - 2f, 0f), 8f);
                }, 4, view => view.Sim.Spawn(EnemyKind.Crab, new Vec2(30f, 48f)), true, true);
            failures += SurvivorShot(dir, "c65_sky_lantern", view => view.Sim.Enemies.Exists(e => e.Kind == EnemyKind.SkyLantern && e.Goal != null && e.Goal.DistanceTo(e.Pos) < 8f), 1, false,
                view =>
                {
                    Enemy l = view.Sim.Enemies.Find(e => e.Kind == EnemyKind.SkyLantern);
                    Vec2 at = l != null ? l.Pos : view.Sim.Player;
                    view.Frame(new Vector3(Mathf.Clamp(at.X, 12f, 48f), Mathf.Clamp(at.Y, 10f, 50f), 0f), 9f);
                }, 5, view => view.Sim.Spawn(EnemyKind.SkyLantern, new Vec2(14f, 52f)), true, true);   // (30,52)에서 띄우면 가는 길의 불꽃 가판대에 내려앉는다
            // 판 시작 1.5초 뒤 할 일 한 줄("할 일: 부두 끝에 서서 바다 위 배를 쏘아 끈다")이 위 알림 줄에 뜬다.
            failures += SurvivorShot(dir, "c33c_goal_alert", view => view.Sim.Time > 1.7f, 0, false, null, 4);
            failures += StationShot(dir, "c33b_station_unlock", "stars=5;best=3,2,0;unlocked=rookie,rescue;selected=rescue", "pilot");
            // 곧 무너진다: 사람이 갇힌 가게의 지붕 초읽기 라벨·붉은 고리와 위 알림 줄.
            failures += SurvivorShot(dir, "c31_collapse_warning", view => view.Sim.CollapseWarnings.Count > 0, 12, false,
                view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y + 3f, 0f), 9f), 1, view =>
            {
                Structure near = NearestHouse(view);
                near.Residents = 2;
                near.Integrity = 0.3f;
                view.Sim.Ignite(near, 1f);
                view.Sim.Player = new Vec2(near.Door.X, near.Door.Y - 4f);
            });
            // 아슬아슬 구조: 무너지기 몇 초 전 문 앞에서 한 명을 데리고 나오는 순간(금색 글자·번쩍·줌).
            failures += SurvivorShot(dir, "c32_close_call", view =>
            {
                Structure near = NearestHouse(view);
                view.Sim.Hp = view.Sim.MaxHp;
                if (near.Residents > 0 && view.Sim.PendingChoices == null) view.Sim.Player = near.Door;
                return view.Sim.CloseCalls.Count > 0;
            }, 4, false, view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y + 2f, 0f), 8f), 1, view =>
            {
                Structure near = NearestHouse(view);
                near.Residents = 1;
                near.Integrity = 0.15f;
                view.Sim.Ignite(near, 1f);
                view.Sim.Player = near.Door;
            }, false);
            // 발밑 안내 화살표: 가장 먼 집에 2명 갇힌 불이 나면 소방관 곁에서 그쪽으로 초록 화살표 + "이름 2명 갇힘".
            failures += SurvivorShot(dir, "c46_guide_arrow", view => view.Sim.Time > 0.4f, 3, false,
                view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y + 0.5f, 0f), 7f), 1, view =>
            {
                view.Sim.Enemies.Clear();
                Structure far = null;
                foreach (Structure st in view.Sim.Structures)
                {
                    if (st.IsBuilding && !st.Collapsed && (far == null || st.DistanceTo(view.Sim.Player) > far.DistanceTo(view.Sim.Player))) far = st;
                }
                far.Residents = 2;
                view.Sim.Ignite(far, 1f);
                view.PointAt(far, 6f);
            });
            // 머리 위 체력 바: 체력 40%로 고정(KeepAlive 끔). 바가 붉게 줄어 있고 HUD 왼쪽 위엔 체력 바가 없다.
            failures += SurvivorShot(dir, "c45_head_hp", view =>
            {
                view.Sim.Hp = view.Sim.MaxHp * 0.4f;
                return view.Sim.Time > 2f;
            }, 4, false, view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y + 1f, 0f), 5f), 1, view => view.Sim.Enemies.Clear(), false);
            // 구급상자: 흰 상자에 빨간 십자가 소방관 곁 바닥에서 튄다.
            failures += SurvivorShot(dir, "c47_medkit", view => view.Sim.Time > 1f && view.Sim.Kits.Count > 0, 6,
                false, view => view.Frame(new Vector3(view.Sim.Player.X + 1.5f, view.Sim.Player.Y + 0.5f, 0f), 6f), 1, view =>
            {
                view.Sim.Enemies.Clear();
                view.Sim.Kits.Add(new Pickup { Pos = new Vec2(view.Sim.Player.X + 3f, view.Sim.Player.Y), Life = 20f });
            });
            // 진압 게이지 + 마감 바: 불 0.7(게이지 30%) 속 2명, 연기가 6초 쌓여 밑의 마감 바에 "7초 · 2명"이 붉게.
            failures += SurvivorShot(dir, "c48_deadline", view => view.Sim.Time > 0.5f, 4, false,
                view => { Structure near = NearestHouse(view); view.Frame(new Vector3(near.Pos.X, near.Pos.Y - 1f, 0f), 7f); }, 1, view =>
            {
                Structure near = NearestHouse(view);
                near.Residents = 2;
                view.Sim.Ignite(near, 0.7f);
                near.Smoke = 6f;
                view.Sim.Player = new Vec2(near.Door.X, near.Door.Y - 4f);
            });
            // 진압 게이지: 다 탄 건물(0%, 주황 일렁임)에 증기 충전이 80%쯤 찬 흰 금.
            failures += SurvivorShot(dir, "c52_douse_gauge", view => view.Sim.Time > 0.5f, 4, false,
                view => { Structure near = NearestHouse(view); view.Frame(new Vector3(near.Pos.X, near.Pos.Y - 1f, 0f), 7f); }, 1, view =>
            {
                Structure near = NearestHouse(view);
                near.Residents = 0;
                view.Sim.Ignite(near, 1f);
                near.HoseHold = SurvivorSim.SteamHold * 0.8f;
                view.Sim.Player = new Vec2(near.Door.X, near.Door.Y - 3f);
            });
            // 지형: 마을의 강과 다리(가운데를 넓게), 숲의 캠프 줄과 북쪽 숲(바람 화살표가 아래쪽), 공단 골목과 드럼 줄.
            failures += SurvivorShot(dir, "c53_river_bridge", view => view.Sim.Time > 0.5f, 4, false,
                view => view.Frame(new Vector3(SurvivorSim.ArenaSize / 2f, SurvivorSim.ArenaSize / 2f, 0f), 15f), 1);
            failures += SurvivorShot(dir, "c54_forest_camp", view => view.Sim.Time > 1f, 4, false,
                view => view.Frame(new Vector3(SurvivorSim.ArenaSize / 2f, SurvivorSim.ArenaSize / 2f - 2f, 0f), 17f), 2);
            failures += SurvivorShot(dir, "c55_factory_alley", view => view.Sim.Time > 0.5f, 4, false,
                view => view.Frame(new Vector3(SurvivorSim.ArenaSize / 2f, SurvivorSim.ArenaSize / 2f, 0f), 15f), 3);
            failures += SurvivorShot(dir, "c43_boots", view =>
            {
                view.Sim.Enemies.Clear();
                return view.Sim.Footprints.Count > 0 && view.Sim.Time > 0.5f;
            }, 20, false, view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y, 0f), 6f), 1, view =>
            {
                Pick(view, UpgradeId.Boots);
                for (int k = 0; k < 6; k++) view.Sim.BurningGround.Add(new Puddle { Pos = new Vec2(view.Sim.Player.X + 1f + k, view.Sim.Player.Y), Radius = 0.8f, Life = 30f, MaxLife = 30f });
            });
            // 증기 폭발: 큰 불에 물줄기를 버틴 끝에 지붕에서 김이 터지는 순간. 적을 치워 봇이 건물만 쏘게 한다.
            failures += SurvivorShot(dir, "c30_steam_burst", view =>
            {
                view.Sim.Enemies.Clear();
                return view.Sim.SteamBursts.Count > 0;
            }, 6, false, view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y + 3f, 0f), 9f), 1, view =>
            {
                Structure near = null;
                foreach (Structure st in view.Sim.Structures) if (st.IsBuilding && (near == null || st.DistanceTo(view.Sim.Player) < near.DistanceTo(view.Sim.Player))) near = st;
                view.Sim.Ignite(near, 1f);
                view.Sim.Player = new Vec2(near.Door.X, near.Door.Y - 5f);
            });
            // 한 번에 쏟는 물(풀장비 물폭탄)이 큰 불을 줄인 순간: 블룸 번쩍임 + 지붕 위 "−N%".
            failures += SurvivorShot(dir, "c27_fire_knock", view => view.Sim.Knocked.Count > 0 && view.Sim.PendingChoices == null, 6, true);
            // 3스테이지 공단: 북서 공장 둘과 그 앞 드럼 줄, 골목의 컨테이너, 콘크리트 바닥과 노란 차선.
            failures += SurvivorShot(dir, "c28_factory", view => view.Sim.Time >= 45f && view.Sim.Enemies.Exists(e => e.Kind == EnemyKind.Oil), 10, false,
                view => view.Frame(new Vector3(14f, 32f, 0f), 13f), 3);
            // 공단 기름 방울과 기름 불: 소방관 옆에 방울 둘과 공장 곁 기름 불을 놓고 크게 잡는다.
            failures += SurvivorShot(dir, "c28b_oil", view => view.Sim.Time >= 3.5f, 4, false,
                view => view.Frame(new Vector3(view.Sim.Player.X + 3f, view.Sim.Player.Y + 2f, 0f), 7f), 3,
                view =>
                {
                    Vec2 p = view.Sim.Player;
                    foreach (Vec2 at in new[] { new Vec2(p.X + 4f, p.Y + 1f), new Vec2(p.X + 6f, p.Y + 3f) })
                    {
                        Enemy blob = view.Sim.Spawn(EnemyKind.Oil, at);
                        blob.Speed = 0f;
                        blob.Hp = blob.MaxHp = 999f;
                    }
                    for (int k = 0; k < 4; k++) view.Sim.BurningGround.Add(new Puddle { Pos = new Vec2(p.X + 2f + (k * 0.9f), p.Y + 4f + ((k % 2) * 0.5f)), Radius = SurvivorSim.OilRadius, Life = 30f, MaxLife = 30f, Oil = true });
                });

            // 4스테이지 항구: 바다·부두 전경(불배가 떠가며 노린 건물까지 빨간 점선), 그리고 불배가 부두에 닿는 순간.
            failures += SurvivorShot(dir, "c58a_harbor_map", view => view.Sim.Time >= 30f, 4, false,
                view => view.Frame(new Vector3(SurvivorSim.ArenaSize / 2f, 44f, 0f), 16f), 4, null, true, true);
            failures += SurvivorShot(dir, "c58_harbor_boat", view => view.Sim.BoatsDocked.Count > 0, 3, false,
                view =>
                {
                    Structure boat = view.Sim.Structures.Find(s => s.Kind == StructureKind.Boat && s.Docked);
                    float x = Mathf.Clamp(boat != null ? boat.Pos.X : 30f, 16f, 44f);
                    view.Frame(new Vector3(x, (boat != null ? boat.Pos.Y : 40f) - 2f, 0f), 10f);
                }, 4, null, true, true);


            // 5스테이지 야시장(밤): 등줄을 타고 가는 불(또는 막 건너간 순간)과, 불꽃 가판대가 쏜 로켓이 떨어지는 순간.
            failures += SurvivorShot(dir, "c59_market_lantern", view => view.Sim.LanternCaught.Count > 0 || view.Sim.Lanterns.Exists(l => l.Burn > 0.3f), 2, false,
                view =>
                {
                    Lantern line = view.Sim.Lanterns.Find(l => l.Burn > 0f) ?? view.Sim.Lanterns.Find(l => l.Cool > 0f);
                    Vec2 at = line != null ? view.Sim.LanternFire(line) : view.Sim.Player;
                    view.Frame(new Vector3(at.X, at.Y + 1f, 0f), 9f);
                }, 5, null, true, true);
            failures += SurvivorShot(dir, "c59b_rockets", view => view.Sim.RocketBursts.Count > 0, 2, false,
                view => view.Frame(new Vector3(18f, 32f, 0f), 11f), 5, view =>
                {
                    Structure stand = view.Sim.Structures.Find(s => s.Kind == StructureKind.Fireworks && s.Pos.X < 20f);
                    view.Sim.Ignite(stand, 0.8f);
                    view.Sim.Player = new Vec2(24f, 30f);
                }, true, true);


            // 새 무기 9종(2026-10-07): Lv5로 쥐고 둘레에 불 몹을 세워 움직임을 찍는다(h1x), 진화(h2x), 진화 카드(h30).
            failures += SurvivorShot(dir, "h10_dog", view => view.Sim.Time > 3.3f && view.Sim.Time > 2.5f && view.Sim.DogBites.Count > 0, 2, false, Near(6f), 1, view => Armed(view, UpgradeId.Sprinkler));
            failures += SurvivorShot(dir, "h11_whip", view => view.Sim.Time > 3.3f && view.Sim.Time > 1.5f, 1, false, Near(6f), 1, view => Armed(view, UpgradeId.Whip));
            failures += SurvivorShot(dir, "h12_ladder", view => view.Sim.Time > 3.3f && view.Sim.LadderStrikes.Count > 0, 4, false, Near(8f), 1, view => Armed(view, UpgradeId.Chain));
            failures += SurvivorShot(dir, "h13_balloon", view => view.Sim.Time > 3.3f && view.Sim.Time > 1f && view.Sim.Balloons.Count >= 2, 1, false, Near(7f), 1, view => Armed(view, UpgradeId.Balloon));
            failures += SurvivorShot(dir, "h14_extinguisher", view => view.Sim.Time > 3.3f && view.Sim.Boomerangs.Exists(b => b.Back), 1, false, Near(8f), 1, view => Armed(view, UpgradeId.Extinguisher));
            failures += SurvivorShot(dir, "h15_foam", view => view.Sim.Time > 3.3f && view.Sim.FoamBalls.Exists(f => f.Age > 0.8f), 1, false, Near(7f), 1, view => Armed(view, UpgradeId.Foam));
            failures += SurvivorShot(dir, "h16_mine", view => view.Sim.Time > 3.3f && view.Sim.Enemies.Exists(e => e.Frozen > 1f), 1, false, Near(6f), 1, view =>
            {
                Pick(view, UpgradeId.Mine, UpgradeId.Mine, UpgradeId.Mine, UpgradeId.Mine, UpgradeId.Mine);
                Crowd(view, 6, 4f, 30f, true);
            });
            failures += SurvivorShot(dir, "h17_bubble", view => view.Sim.Time > 3.3f && view.Sim.Enemies.Exists(e => e.Captured > 0f && e.Captured < 0.9f), 1, false, Near(6f), 1, view =>
            {
                Pick(view, UpgradeId.Bubble, UpgradeId.Bubble, UpgradeId.Bubble, UpgradeId.Bubble, UpgradeId.Bubble);
                Crowd(view, 8, 4f, 5f, false);
            });
            failures += SurvivorShot(dir, "h18_manhole", view => view.Sim.Time > 3.3f && view.Sim.GeyserBursts.Count > 0, 3, false, Near(7f), 1, view => Armed(view, UpgradeId.Manhole));
            failures += SurvivorShot(dir, "h20_dogpack", view => view.Sim.Time > 3.3f && view.Sim.Time > 2f && view.Sim.Dogs.Count == SurvivorSim.DogPackCount, 1, false, Near(7f), 1, view => Evolved(view, UpgradeId.Crown));
            failures += SurvivorShot(dir, "h21_whirl", view => view.Sim.Time > 3.3f && view.Sim.WhirlMarks.Count > 12, 1, false, Near(6f), 1, view => Evolved(view, UpgradeId.Whirl));
            failures += SurvivorShot(dir, "h22_bridge", view => view.Sim.Time > 3.3f && view.Sim.Ladders.Exists(l => l.Struck && l.Age > 0.6f), 1, false, Near(8f), 1, view => Evolved(view, UpgradeId.Surge));
            failures += SurvivorShot(dir, "h23_storm", view => view.Sim.Time > 3.3f && view.Sim.Balloons.FindAll(b => b.Small).Count >= 3, 1, false, Near(7f), 1, view => Evolved(view, UpgradeId.BalloonStorm));
            failures += SurvivorShot(dir, "h24_tornado", view => view.Sim.Time > 3.3f && view.Sim.Tornadoes.Exists(t => t.Life < SurvivorSim.TornadoLife - 1f), 1, false, Near(8f), 1, view => Evolved(view, UpgradeId.Tornado));
            failures += SurvivorShot(dir, "h25_avalanche", view => view.Sim.Time > 3.3f && view.Sim.AvalancheX.HasValue && System.Math.Abs(view.Sim.AvalancheX.Value - view.Sim.Player.X) < 3f, 1, false, Near(10f), 1, view => Evolved(view, UpgradeId.Avalanche));
            failures += SurvivorShot(dir, "h26_icefield", view => view.Sim.Time > 3.3f && view.Sim.Time > 1.5f, 1, false, Near(7f), 1, view =>
            {
                Evolved(view, UpgradeId.IceField);
                Vec2 p = view.Sim.Player;
                foreach (Vec2 o in new[] { new Vec2(-3f, 2f), new Vec2(1f, 3f), new Vec2(4f, 1f), new Vec2(2f, -2.5f) }) view.Sim.Mines.Add(new Mine { Pos = new Vec2(p.X + o.X, p.Y + o.Y) });
                Crowd(view, 6, 5f, 30f, true);
            });
            failures += SurvivorShot(dir, "h27_bubblefall", view => view.Sim.Time > 3.3f && view.Sim.Enemies.FindAll(e => e.Captured > 0f).Count >= 2, 1, false, Near(6f), 1, view =>
            {
                Evolved(view, UpgradeId.BubbleFall);
                Crowd(view, 10, 4f, 5f, false);
            });
            failures += SurvivorShot(dir, "h28_waterline", view => view.Sim.Time > 3.3f && view.Sim.Geysers.Count >= 4 && view.Sim.GeyserBursts.Count > 0, 1, false, Near(9f), 1, view => Evolved(view, UpgradeId.Waterline));
            failures += SurvivorShot(dir, "h30_evolve_card", view => view.Sim.PendingChoices != null && view.Sim.PendingChoices.Exists(Loadout.IsEvolution), 40, false, null, 1, view =>
            {
                Pick(view, UpgradeId.Whip, UpgradeId.Whip, UpgradeId.Whip, UpgradeId.Whip, UpgradeId.Whip, UpgradeId.Tank);
                view.Sim.DropGem(view.Sim.Player, view.Sim.XpToNext);
            });

            // 마을 몹(2026-10-07): 불쥐 줄, 도깨비 예고, 불풍선 퓨즈, 불곰, 50초 습격, 3:00 화마와 쓰러지는 순간.
            failures += SurvivorShot(dir, "h40_rats", view =>
            {
                Only(view, EnemyKind.Rat);
                if (view.Sim.Time > 3f && view.Sim.RatLines.Count == 0 && !view.Sim.Enemies.Exists(e => e.Kind == EnemyKind.Rat))
                {
                    Structure goal = NearestHouse(view);
                    view.Sim.SpawnRatLine(new Vec2(goal.Pos.X + 9f, goal.Pos.Y - 5f), goal, 8);
                    view.Sim.SpawnRatLine(new Vec2(goal.Pos.X - 8f, goal.Pos.Y - 6f), goal, 6);
                    foreach (Enemy e in view.Sim.Enemies) e.MaxHp = e.Hp = 999f;
                }
                return view.Sim.Time > 4.2f;
            }, 1, false, Near(9f), 1, view => view.Sim.Reports = false);
            failures += SurvivorShot(dir, "h41_goblin", view =>
            {
                Only(view, EnemyKind.Goblin);
                return view.Sim.Time > 3.4f && view.Sim.Enemies.Exists(e => e.Kind == EnemyKind.Goblin && e.Phase > 0.9f);
            }, 1, false, view =>
            {
                Enemy g = view.Sim.Enemies.Find(e => e.Kind == EnemyKind.Goblin);
                Vec2 c = g != null ? g.Pos : view.Sim.Player;
                view.Frame(new Vector3(c.X, c.Y + 1.5f, 0f), 6f);
            }, 1, view =>
            {
                view.Sim.Reports = false;
                Structure goal = NearestHouse(view);
                Enemy g = view.Sim.Spawn(EnemyKind.Goblin, new Vec2(goal.Pos.X + 1f, goal.Pos.Y - goal.Half.Y - 1.8f));
                g.Goal = goal;
                g.MaxHp = g.Hp = 9999f;
                view.Sim.Player = new Vec2(goal.Pos.X - 5f, goal.Pos.Y - goal.Half.Y - 6f);
            }, true, false);
            failures += SurvivorShot(dir, "h43_bear", view =>
            {
                Only(view, EnemyKind.Bear);
                return view.Sim.Time > 4.5f;
            }, 1, false, view =>
            {
                Enemy b = view.Sim.Enemies.Find(e => e.Kind == EnemyKind.Bear);
                Vec2 c = b != null ? b.Pos : view.Sim.Player;
                view.Frame(new Vector3(c.X, c.Y + 1.5f, 0f), 9f);
            }, 1, view =>
            {
                view.Sim.Reports = false;
                Structure goal = NearestHouse(view);
                Enemy bear = view.Sim.Spawn(EnemyKind.Bear, new Vec2(goal.Pos.X - 9f, goal.Pos.Y));
                bear.Goal = goal;
                bear.MaxHp = 99999f;
                bear.Hp = bear.MaxHp * 0.7f;
                view.Sim.Player = new Vec2(goal.Pos.X - 3f, goal.Pos.Y - 7f);
            }, true, false);
            failures += SurvivorShot(dir, "h42_fire_balloon", view => view.Sim.Time > 3.4f && view.Sim.Enemies.Exists(e => e.Kind == EnemyKind.FireBalloon && e.Phase > 0.5f), 1, false, Near(8f), 1, view =>
            {
                Structure goal = NearestHouse(view);
                for (int k = 0; k < 2; k++)
                {
                    Enemy b = view.Sim.Spawn(EnemyKind.FireBalloon, new Vec2(goal.Pos.X + (k * 1.5f), goal.Pos.Y + 2.5f));
                    b.Goal = goal;
                    b.MaxHp = b.Hp = 9999f;
                    b.Phase = -2.2f;
                }
                view.Sim.Player = new Vec2(goal.Pos.X - 4f, goal.Pos.Y - goal.Half.Y - 3f);
            }, true, false);
            failures += SurvivorShot(dir, "h44_raid", view => view.Sim.Raids.Count > 0 && view.Sim.Time > view.Sim.Raids[0].At + 1.2f, 1, false, view =>
            {
                Raid r = view.Sim.Raids.Count > 0 ? view.Sim.Raids[0] : null;
                Vec2 p = view.Sim.Player;
                view.Frame(r != null ? new Vector3((r.From.X + p.X) / 2f, (r.From.Y + p.Y) / 2f, 0f) : new Vector3(p.X, p.Y, 0f), 13f);
            }, 1, null, true, true);
            failures += SurvivorShot(dir, "h45_hwama", view => view.Sim.Boss != null && view.Sim.Time > SurvivorSim.FinaleAt + 4f, 1, true, view =>
            {
                Vec2 b = view.Sim.Boss != null ? view.Sim.Boss.Pos : view.Sim.Player;
                view.Frame(new Vector3(b.X, b.Y + 1.5f, 0f), 12f);
            }, 1, null, true, true);
            failures += SurvivorShot(dir, "h46_hwama_down", view =>
            {
                if (view.Sim.Boss != null && !view.Sim.Boss.Dead && view.Sim.Time > SurvivorSim.FinaleAt + 3f) view.Sim.Kill(view.Sim.Boss);
                return view.Sim.BossKilled;
            }, 6, true, view => view.Frame(new Vector3(view.Sim.BossDownAt.X, view.Sim.BossDownAt.Y + 1f, 0f), 11f), 1, null, true, true);

            Debug.Log("[ProtoShots] 완료, 실패 " + failures);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Shot(string dir, string name, Action<TacticsView> prepare)
        {
            if (!Wanted(name)) return 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new TacticsView(root.transform, camera, canvas);
                view.LoadLevel(0);
                prepare(view);
                view.Refresh();

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name);
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        /// <summary>봇에게 몇 초 맡겨 교전 중인 화면을 찍는다.</summary>
        private static int ActionShot(string dir, string name, float seconds)
        {
            if (!Wanted(name)) return 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new ActionView(root.transform, camera, canvas);
                var bot = new ActionBot(view.Sim);
                int ticks = (int)(seconds / ActionSim.Dt);
                for (int i = 0; i < ticks && view.Sim.Outcome == AOutcome.Playing; i++)
                {
                    view.Step(bot.Next());
                    view.Refresh(ActionSim.Dt);
                }

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (" + view.Sim.Outcome + ", 구조 " + view.Sim.Rescued + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        /// <summary>봇에게 판을 맡겨 조건이 될 때까지 굴린 뒤 찍는다. 카드 장면은 카드를 고르지 않고 멈춘다.</summary>
        /// <param name="settle">조건에 닿은 뒤 효과를 흘려 보낼 프레임 수(60 = 1초).</param>
        private static string GearOf(SurvivorSim sim)
        {
            var parts = new List<string>();
            foreach (UpgradeId id in sim.Build.Owned()) parts.Add(id + " " + sim.Build.Level(id));
            return string.Join(" · ", parts);
        }

        /// <param name="frame">찍기 직전에 카메라를 옮긴다(동네 전체 보기 등).</param>
        /// <summary>
        /// 캡처 판의 시드: 봇이 4:00까지 지켜 이기고, 대형 신고를 다 구해 보물상자를 열고, 진화·MAX까지 가는 판
        /// (보스가 없어져 지는 판에선 뒤 장면을 못 찍는다).
        /// </summary>
        private const int ShotSeed = 5;

        /// <summary>
        /// 캡처는 화면을 보는 용도라 판이 중간에 끝나지 않게 붙잡는다: 체력은 채우고, 건물은 튼튼함 0.3 밑으로 안 내려간다
        /// (밸런스는 proto-tests의 봇 측정이 본다. 규칙이 바뀔 때마다 시드가 지는 판이 되어 뒷장면을 못 찍었다).
        /// </summary>
        /// <summary>판 시작에 카드를 차례로 고른다(플레이어 경로: Choose).</summary>
        private static void Pick(SurvivorView view, params UpgradeId[] cards)
        {
            foreach (UpgradeId id in cards)
            {
                view.Sim.PendingChoices = new System.Collections.Generic.List<UpgradeId> { id };
                view.Sim.Choose(0);
            }
        }

        /// <summary>캡처 장면을 깨끗하게: 이 종류 말고 가장자리에서 새로 나온 몹은 지운다.</summary>
        private static void Only(SurvivorView view, EnemyKind kind)
        {
            view.Sim.Enemies.RemoveAll(e => e.Kind != kind);
        }

        /// <summary>소방관을 가운데 두고 size 크기로 찍는다.</summary>
        private static Action<SurvivorView> Near(float size)
        {
            return view => view.Frame(new Vector3(view.Sim.Player.X, view.Sim.Player.Y, 0f), size);
        }

        /// <summary>진화까지: 무기 Lv5(+불 몹 둘레) → 짝 보조 → 진화 카드.</summary>
        private static void Evolved(SurvivorView view, UpgradeId evolution)
        {
            Armed(view, Loadout.BaseOf(evolution));
            Pick(view, Loadout.PairOf(evolution), evolution);
        }

        /// <summary>소방관 둘레 r칸에 불씨 n마리(walk면 소방관에게 걸어온다, 아니면 선다).</summary>
        private static void Crowd(SurvivorView view, int n, float r, float hp, bool walk)
        {
            SurvivorSim sim = view.Sim;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Enemy e = sim.Spawn(EnemyKind.Ember, new Vec2(sim.Player.X + (Mathf.Cos(a) * r), sim.Player.Y + (Mathf.Sin(a) * r)));
                if (!walk) e.Speed = 0f;
                e.MaxHp = e.Hp = hp;
            }
        }

        /// <summary>무기를 Lv5까지 고르고 소방관 둘레 3~5칸에 움직이지 않는 불 몹 10마리를 세운다(타격 캡처).</summary>
        private static void Armed(SurvivorView view, UpgradeId weapon)
        {
            for (int i = 0; i < Loadout.MaxLevel; i++) Pick(view, weapon);
            SurvivorSim sim = view.Sim;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                float r = 3f + (2f * (i % 2));
                Enemy e = sim.Spawn(EnemyKind.Blaze, new Vec2(sim.Player.X + (Mathf.Cos(a) * r), sim.Player.Y + (Mathf.Sin(a) * r)));
                e.Speed = 0f;
                e.MaxHp = e.Hp = 400f;
            }
        }

        private static void KeepAlive(SurvivorSim sim)
        {
            sim.Hp = sim.MaxHp;
            foreach (Structure st in sim.Structures)
            {
                if (st.IsBuilding && !st.Collapsed && st.Integrity < 0.3f) st.Integrity = 0.3f;
            }
        }

        /// <summary>소방서 화면을 찍는다. save가 있으면 그 저장 글을 끼우고(저장하지 않는다), tap이 있으면 그 소방관 칸을 누른 뒤 찍는다.</summary>
        private static int StationShot(string dir, string name, string save, string tap = null)
        {
            if (!Wanted(name)) return 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);
                var view = new SurvivorView(root.transform, camera, canvas);
                view.LoadStation(save ?? "");
                if (tap != null) view.TapFirefighter(tap);
                if (!view.StationOpen) throw new Exception("소방서가 열려 있지 않다");
                for (int i = 0; i < 5; i++) view.Refresh(SurvivorSim.Dt);
                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (소방서: " + view.Station.Serialize() + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        /// <summary>소방관에게 가장 가까운 가게·창고.</summary>
        private static Structure NearestHouse(SurvivorView view)
        {
            Structure near = null;
            foreach (Structure st in view.Sim.Structures)
            {
                if (st.IsBuilding && !st.Collapsed && (near == null || st.DistanceTo(view.Sim.Player) < near.DistanceTo(view.Sim.Player))) near = st;
            }
            return near;
        }

        /// <summary>대형 신고 건물에 사람이 남아 있으면 소방관을 그 문 앞에 세운다(상자 캡처용: 한 명도 잃지 않아야 상자가 나온다).</summary>
        private static void HoldBigReportDoor(SurvivorView view)
        {
            Structure big = view.Sim.BigReport;
            // 문 앞 구조 범위(1.3) 안이되 줍기 범위(0.9) 밖에 서서, 떨어진 상자가 바로 주워지지 않게 한다.
            if (big != null && !big.Collapsed && big.Residents > 0 && view.Sim.PendingChoices == null) view.Sim.Player = new Vec2(big.Door.X, big.Door.Y - 1.1f);
        }

        /// <param name="keepAlive">false면 체력·튼튼함을 받쳐 주지 않는다(무너지기 직전 장면용).</param>
        /// <param name="pro">true면 숙련 봇이 판을 굴린다(기본 봇은 아이템 다이어트 뒤 1:20까지 집을 다 태워 대형 신고가 뜰 자리가 없다 — docs §16).</param>
        private static int SurvivorShot(string dir, string name, Func<SurvivorView, bool> until, int settle = 20, bool maxGear = false, Action<SurvivorView> frame = null, int stage = 1, Action<SurvivorView> setup = null, bool keepAlive = true, bool pro = false)
        {
            if (!Wanted(name)) return 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas, maxGear, stage);
                view.CloseStation();
                view.Restart(ShotSeed);
                setup?.Invoke(view);
                var bot = new SurvivorBot(view.Sim) { Pro = pro };
                int guard = 0;
                while (!until(view) && view.Sim.Outcome == SOutcome.Playing && guard++ < 60 * 400) BotTick(view, bot, keepAlive);
                if (!until(view)) throw new Exception("조건에 닿기 전에 판이 끝났다: " + view.Sim.Outcome + " t=" + view.Sim.Time);
                // 카드는 0.3초 뒤에 튀어 오르니 다 뜬 뒤를 찍는다. 효과도 조금 흐르게 둔다.
                for (int i = 0; i < settle; i++) view.Refresh(SurvivorSim.Dt);
                frame?.Invoke(view);

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (" + view.Sim.Outcome + " 별 " + view.Sim.Stars + " 무너짐 " + view.Sim.HousesLost + " 구조 " + view.Sim.Rescued + ", t=" + (int)view.Sim.Time + ", Lv " + view.Sim.Level + ", 적 " + view.Sim.Enemies.Count + ", 카드 " + (view.Sim.PendingChoices != null) + ", " + GearOf(view.Sim) + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        /// <summary>봇 한 칸: 카드가 떠 있으면 고르고, 아니면 살려 둔 채 조준·이동하고 화면을 60Hz 한 칸 흘린다.</summary>
        /// <summary>
        /// 가장 가까운 가게에 불(1.0)을 붙이고 문 4칸 앞에서 지붕을 겨눠 쏜다. untilOut이면 꺼질 때까지 쏘고 몇 프레임 뒤를,
        /// 아니면 0.8초 쏜 뒤를 찍는다. 몹은 지운다(물 반응만 본다).
        /// </summary>
        private static void HoseBuilding(SurvivorView view, bool untilOut)
        {
            Structure near = null;
            foreach (Structure st in view.Sim.Structures) if (st.IsBuilding && !st.Collapsed && (near == null || st.DistanceTo(view.Sim.Player) < near.DistanceTo(view.Sim.Player))) near = st;
            view.Sim.Ignite(near, 1f);
            view.Sim.Player = new Vec2(near.Door.X, near.Door.Y - 4f);
            int guard = 0;
            while (guard++ < (untilOut ? 60 * 30 : 48))
            {
                view.Sim.Enemies.Clear();
                view.Sim.Spraying = true;
                view.Sim.Aim = new Vec2(near.Pos.X - view.Sim.Player.X, near.Pos.Y - view.Sim.Player.Y);
                view.Step(new Vec2(0f, 0f));
                view.Refresh(SurvivorSim.Dt);
                if (untilOut && !near.Burning) break;
            }
            for (int i = 0; i < (untilOut ? 4 : 0); i++) view.Refresh(SurvivorSim.Dt);
            view.Frame(new Vector3(near.Pos.X, near.Pos.Y - 2f, 0f), 5.5f);
        }

        /// <summary>수호자 결과 장면용: 판 끝 5초 전으로 건너뛰고, 구한 사람 18명·무너진 집 넷(상처)을 만든다.</summary>
        private static void DayEnd(SurvivorView view)
        {
            SurvivorSim sim = view.Sim;
            sim.Time = SurvivorSim.RunTime - 5f;
            sim.Reports = false;
            sim.Rescued = 18;
            int fell = 0;
            foreach (Structure st in sim.Structures)
            {
                if (!st.IsBuilding) continue;
                if (fell < 4 && st.Kind != StructureKind.Depot)
                {
                    st.Fire = 1f;
                    st.Integrity = 0.0001f;
                    fell++;
                }
                else st.Guarded = true;
            }
            // 하니스의 KeepAlive가 무너지려는 집을 붙잡기 전에 한 틱 흘려 무너뜨린다.
            view.Step(new Vec2(0f, 0f));
        }

        /// <summary>잿더미 둥지용: 플레이어에서 가장 가까운 가게 하나를 곧 무너지게 한다.</summary>
        private static void Ruin(SurvivorView view)
        {
            SurvivorSim sim = view.Sim;
            Structure near = null;
            foreach (Structure st in sim.Structures) if (st.IsBuilding && st.Kind != StructureKind.Depot && (near == null || st.DistanceTo(sim.Player) < near.DistanceTo(sim.Player))) near = st;
            near.Fire = 1f;
            near.Integrity = 0.0001f;
            view.Step(new Vec2(0f, 0f));
        }

        /// <summary>봇 한 칸: 카드가 떠 있으면 고르고, 아니면 살려 둔 채 조준·이동하고 화면을 60Hz 한 칸 흘린다.</summary>
        private static void BotTick(SurvivorView view, SurvivorBot bot, bool keepAlive = true)
        {
            if (view.Sim.PendingChoices != null)
            {
                view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices, view.Sim.Build));
                return;
            }
            if (keepAlive) KeepAlive(view.Sim);
            bot.AimHose();
            view.Step(bot.Move());
            view.Refresh(SurvivorSim.Dt);
        }

        /// <summary>
        /// README용 플레이 장면: 봇이 판을 굴리다 ClipFrom초부터 ClipSeconds초 동안 ClipStep칸마다 한 장씩 찍는다(60Hz ÷ 5 = 12fps).
        /// 실행: tools/unity-check.sh clip [폴더] → frame_000.png…, GIF는 tools/make-gif.py.
        /// </summary>
        public static void RecordClip()
        {
            const float ClipFrom = 140f;
            const float ClipSeconds = 8f;
            const int ClipStep = 5;
            try
            {
                string dir = ArgValue("-shotDir") ?? Path.Combine(Application.dataPath, "../../tools/.clip");
                Directory.CreateDirectory(dir);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);
                var view = new SurvivorView(root.transform, camera, canvas);
                view.CloseStation();
                view.Restart(ShotSeed);
                var bot = new SurvivorBot(view.Sim);
                int guard = 0;
                // 무기·대원이 갖춰지고 불이 번지는 중반까지 굴린다.
                while (view.Sim.Time < ClipFrom && view.Sim.Outcome == SOutcome.Playing && guard++ < 60 * 400) BotTick(view, bot);
                int frames = Mathf.RoundToInt(ClipSeconds * 60f / ClipStep);
                for (int f = 0; f < frames; f++)
                {
                    for (int k = 0; k < ClipStep; k++) BotTick(view, bot);
                    Canvas.ForceUpdateCanvases();
                    Capture(camera, Path.Combine(dir, "frame_" + f.ToString("000") + ".png"));
                }
                Debug.Log("[ProtoClip] 완료 " + frames + "장, t=" + (int)view.Sim.Time + ", " + view.Sim.Outcome);
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoClip] 실패 — " + e);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 폰 조작: 봇이 몇 초 굴린 판에 가짜 두 손가락(왼손 오른쪽으로 끌기, 오른손 위로 끌기)을 Tick으로 넣는다.
        /// 조이스틱 두 개가 보이고, 소방관이 걸으면서 끈 쪽으로 물을 쏘는지 본다.
        /// </summary>
        private static int TouchShot(string dir, string name)
        {
            if (!Wanted(name)) return 0;
            RenderTexture screen = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                // 손가락 좌표와 캔버스가 찍을 화면(1920×1080)과 같은 크기를 보게 한다.
                screen = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = screen;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas);
                view.CloseStation();
                var bot = new SurvivorBot(view.Sim);
                while (view.Sim.Time < 8f && view.Sim.Outcome == SOutcome.Playing)
                {
                    if (view.Sim.PendingChoices != null)
                    {
                        view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices, view.Sim.Build));
                        continue;
                    }
                    KeepAlive(view.Sim);
                    bot.AimHose();
                    view.Step(bot.Move());
                    view.Refresh(SurvivorSim.Dt);
                }

                var left = new Vec2(Width * 0.18f, Height * 0.3f);
                var right = new Vec2(Width * 0.78f, Height * 0.35f);
                for (int frame = 0; frame < 45; frame++)
                {
                    if (view.Sim.PendingChoices != null) view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices, view.Sim.Build));
                    FingerPhase phase = frame == 0 ? FingerPhase.Down : FingerPhase.Held;
                    float t = Mathf.Clamp01(frame / 10f);
                    var input = new ProtoInput
                    {
                        Fingers = new List<Finger>
                        {
                            new Finger { Id = 0, Phase = phase, At = new Vec2(left.X + (80f * t), left.Y) },
                            new Finger { Id = 1, Phase = phase, At = new Vec2(right.X + (40f * t), right.Y + (90f * t)) },
                        },
                    };
                    view.Tick(SurvivorSim.Dt, input);
                }
                if (!view.Sim.Spraying) throw new Exception("오른손 스틱을 눌렀는데 물이 안 나간다");
                foreach (RectTransform r in canvas.GetComponentsInChildren<RectTransform>(true))
                {
                    if (r.name.StartsWith("Stick")) Debug.Log("[ProtoShots] 스틱 " + r.name + " 켜짐 " + r.gameObject.activeInHierarchy + " 자리 " + r.anchoredPosition + " 크기 " + r.rect.size + " 부모 " + r.parent.name);
                }

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (조준 " + view.Sim.Aim.X.ToString("0.00") + "," + view.Sim.Aim.Y.ToString("0.00") + ", 쏨 " + view.Sim.Spraying + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
            finally
            {
                if (screen != null) UnityEngine.Object.DestroyImmediate(screen);
            }
        }

        /// <summary>
        /// 폰 조준 보정: 불 하나를 소방관 오른쪽 위 20°에 두고, 가짜 오른손은 정확히 오른쪽(0°)으로 끈다.
        /// 물줄기가 끈 쪽이 아니라 불 쪽으로 휘어 들어가는지 본다.
        /// </summary>
        private static int AimShot(string dir, string name)
        {
            if (!Wanted(name)) return 0;
            RenderTexture screen = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                screen = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = screen;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas);
                view.CloseStation();
                view.Sim.Reports = false;
                var bot = new SurvivorBot(view.Sim);
                while (view.Sim.Time < 3f && view.Sim.Outcome == SOutcome.Playing)
                {
                    bot.AimHose();
                    view.Step(bot.Move());
                    view.Refresh(SurvivorSim.Dt);
                }
                view.Sim.Enemies.Clear();
                Vec2 p = view.Sim.Player;
                double a = 20.0 * Math.PI / 180.0;
                Enemy fire = view.Sim.Spawn(EnemyKind.Blaze, new Vec2(p.X + (float)(Math.Cos(a) * 6.0), p.Y + (float)(Math.Sin(a) * 6.0)));
                fire.MaxHp = fire.Hp = 9999f;

                var right = new Vec2(Width * 0.78f, Height * 0.4f);
                for (int frame = 0; frame < 40; frame++)
                {
                    FingerPhase phase = frame == 0 ? FingerPhase.Down : FingerPhase.Held;
                    float t = Mathf.Clamp01(frame / 8f);
                    var input = new ProtoInput { Fingers = new List<Finger> { new Finger { Id = 1, Phase = phase, At = new Vec2(right.X + (120f * t), right.Y) } } };
                    view.Tick(SurvivorSim.Dt, input);
                }
                Vec2 me = view.Sim.Player;
                float toFire = Mathf.Atan2(fire.Pos.Y - me.Y, fire.Pos.X - me.X) * Mathf.Rad2Deg;
                float aimed = Mathf.Atan2(view.Sim.Aim.Y, view.Sim.Aim.X) * Mathf.Rad2Deg;
                float miss = Mathf.Abs(Mathf.DeltaAngle(toFire, aimed));
                if (miss > 8f) throw new Exception("조준 보정이 불 쪽으로 안 당긴다: 불 " + toFire + "° 조준 " + aimed + "°");

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (끈 방향 0°, 불 " + toFire.ToString("0.0") + "°, 조준 " + aimed.ToString("0.0") + "°)");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
            finally
            {
                if (screen != null) UnityEngine.Object.DestroyImmediate(screen);
            }
        }

        /// <summary>1스테이지를 이긴 결과창에서 탭하면 2스테이지가 "STAGE 2" 띠와 함께 열린다(플레이어 경로: Tick에 클릭).</summary>
        private static int NextStageShot(string dir, string name)
        {
            if (!Wanted(name)) return 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Prototype");
                Camera camera = PrototypeHost.SetUpCamera();
                camera.aspect = (float)Width / Height;
                Canvas canvas = UiKit.CreateCanvas(root.transform, camera, "Canvas", 0);

                var view = new SurvivorView(root.transform, camera, canvas);
                view.CloseStation();
                view.Restart(ShotSeed);
                var bot = new SurvivorBot(view.Sim);
                int guard = 0;
                while (view.Sim.Outcome == SOutcome.Playing && guard++ < 60 * 400)
                {
                    if (view.Sim.PendingChoices != null)
                    {
                        view.Choose(SurvivorBot.PickCard(view.Sim.PendingChoices, view.Sim.Build));
                        continue;
                    }
                    KeepAlive(view.Sim);
                    bot.AimHose();
                    view.Step(bot.Move());
                }
                if (view.Sim.Outcome != SOutcome.Won) throw new Exception("1스테이지를 못 이겼다: " + view.Sim.Outcome);
                for (int i = 0; i < 80; i++) view.Tick(SurvivorSim.Dt, new ProtoInput());
                view.Tick(SurvivorSim.Dt, new ProtoInput { MouseClicked = true });
                if (view.Sim.Stage.Number != 2) throw new Exception("이긴 뒤 탭했는데 스테이지 " + view.Sim.Stage.Number);
                for (int i = 0; i < 40; i++) view.Tick(SurvivorSim.Dt, new ProtoInput());

                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(dir, name + ".png"));
                Debug.Log("[ProtoShots] " + name + " (스테이지 " + view.Sim.Stage.Number + " " + view.Sim.Stage.Name + ", t=" + view.Sim.Time.ToString("0.0") + ")");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[ProtoShots] 실패: " + name + " — " + e);
                return 1;
            }
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                // 픽셀 3D: 월드 카메라(낮은 해상도 텍스처에 그린다)를 먼저 그려야 화면 카메라가 그 도트를 담는다.
                foreach (Camera other in Camera.allCameras)
                {
                    if (other != camera && other.targetTexture != null) other.Render();
                }
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>FIREGAME_SHOTS="c73,c74"이면 그 접두로 시작하는 장면만 찍는다(빠르게 고쳐 보기). 비면 전부.</summary>
        private static bool Wanted(string name)
        {
            string only = Environment.GetEnvironmentVariable("FIREGAME_SHOTS");
            if (string.IsNullOrEmpty(only)) return true;
            foreach (string p in only.Split(',')) if (p.Length > 0 && name.StartsWith(p.Trim())) return true;
            return false;
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
