using System;
using System.Collections.Generic;
using UnityEngine;
using ZZ.Demo;

namespace ZZ.Editor
{
    /// <summary>
    /// The README demo clip library. Each clip is a declarative step list; the machinery that plays
    /// and captures it lives in <see cref="DemoCaptureDirector"/> and <c>Media/Core</c>.
    /// </summary>
    public static class DemoCapture
    {
        private const string k_Category = "演示录制";
        private const int k_FrameRate = 60;

        /// <summary>Used by clips that stage the player first and then settle on their own.</summary>
        private const float k_StageSettleSeconds = 0.2f;

        /// <summary>World pose with the monastery approach behind the player: red trees and flowers.</summary>
        private const float k_ScenicX = 227f;
        private const float k_ScenicZ = 249f;
        private const float k_ScenicYaw = 270f;

        /// <summary>Same spot facing the enemy that spawns there, for combat clips.</summary>
        private const float k_EnemyPoseX = 235f;
        private const float k_EnemyPoseZ = 249f;
        private const float k_EnemyPoseYaw = 180f;

        [ZZTool(k_Category, "录制片段：新游戏", 10)]
        public static void RecordNewGame()
        {
            Record(BuildNewGameClip());
        }

        [ZZTool(k_Category, "录制片段：战斗", 20)]
        public static void RecordCombat()
        {
            Record(BuildCombatClip());
        }

        [ZZTool(k_Category, "录制片段：移动", 30)]
        public static void RecordMovement()
        {
            Record(BuildMovementClip());
        }

        [ZZTool(k_Category, "录制片段：敌人 AI", 40)]
        public static void RecordEnemy()
        {
            Record(BuildEnemyClip());
        }

        [ZZTool(k_Category, "录制片段：UI 系统", 50)]
        public static void RecordUi()
        {
            Record(BuildUiClip());
        }

        [ZZTool(k_Category, "勘景：多机位样帧", 60)]
        public static void RecordScout()
        {
            Record(BuildScoutClip());
        }

        /// <summary>Starts a clip. Also the entry point for <see cref="DemoCaptureRelay"/>.</summary>
        public static void Record(DemoClip clip)
        {
            if (DemoCaptureDirector.IsRunning)
            {
                throw new InvalidOperationException("A demo capture is already running.");
            }

            DemoCaptureDirector.Play(clip);
        }

        /// <summary>Starts a clip by name, for the file based relay.</summary>
        public static void Record(string clipName)
        {
            Record(clipName switch
            {
                "new-game" => BuildNewGameClip(),
                "combat" => BuildCombatClip(),
                "movement" => BuildMovementClip(),
                "enemy" => BuildEnemyClip(),
                "ui" => BuildUiClip(),
                "scout" => BuildScoutClip(),
                _ => throw new InvalidOperationException($"Unknown demo clip '{clipName}'.")
            });
        }

        /// <summary>Title screen, character creation, then the world load. Drives the UI directly.</summary>
        private static DemoClip BuildNewGameClip()
        {
            var steps = new List<DemoStep>
            {
                new("title", 0.6f, default),
                new("host", 0.6f, default, DemoMenuActions.StartNetworkAsHost),
                new("main-menu", 0.8f, default, DemoMenuActions.PressStart),
                new("open-creation", 0.9f, default, DemoMenuActions.OpenCharacterCreation),
                new("class-menu", 0.8f, default, () => DemoMenuActions.PreviewClass(1)),
                new("select-class", 0.35f, default, () => DemoMenuActions.SelectClass(1)),
                new("hair-menu", 0.8f, default, () => DemoMenuActions.PreviewHairstyle(2)),
                new("select-hairstyle", 0.35f, default, () => DemoMenuActions.SelectHairstyle(2)),
                new("start-game", 1.7f, default, DemoMenuActions.StartCreatedGame)
            };

            return new DemoClip("new-game", steps, k_FrameRate, 0f, false, false);
        }

        private static DemoClip BuildCombatClip()
        {
            var steps = new List<DemoStep>
            {
                new("stage", 2f, default, StageAtEnemy),
                new("light-1", 0.1f, new DemoInputState { LightAttack = true }),
                new("light-1-recover", 0.4f, default),
                new("light-2", 0.1f, new DemoInputState { LightAttack = true }),
                new("light-2-recover", 0.4f, default),
                new("light-3", 0.1f, new DemoInputState { LightAttack = true }),
                new("light-3-recover", 0.5f, default),
                new("heavy-charge", 0.8f, new DemoInputState { HeavyAttack = true }),
                new("heavy-release", 0.7f, default),
                new("run-approach", 0.4f, new DemoInputState { Move = Vector2.up }),
                new("run-attack", 0.1f, new DemoInputState { Move = Vector2.up, LightAttack = true }),
                new("run-attack-recover", 0.7f, default),
                new("jump", 0.1f, new DemoInputState { Move = Vector2.up, Jump = true }),
                new("jump-attack", 0.1f, new DemoInputState { LightAttack = true }),
                new("jump-attack-recover", 0.7f, default),
                new("end", 0.3f, default)
            };

            return new DemoClip("combat", steps, k_FrameRate, k_StageSettleSeconds);
        }

        private static DemoClip BuildMovementClip()
        {
            var steps = new List<DemoStep>
            {
                new("stage", 1.8f, default, StageAtScenicSpot),
                new("walk", 0.9f, new DemoInputState { Move = new Vector2(0f, 0.4f) }),
                new("run", 0.8f, new DemoInputState { Move = Vector2.up }),
                new("sprint", 1.3f, new DemoInputState { Move = Vector2.up, Sprint = true }),
                new("stop", 0.3f, default),
                new("roll-approach", 0.25f, new DemoInputState { Move = Vector2.up }),
                new("roll", 0.1f, new DemoInputState { Move = Vector2.up, Dodge = true }),
                new("roll-recover", 0.55f, default),
                new("backstep", 0.1f, new DemoInputState { Dodge = true }),
                new("backstep-recover", 0.5f, default),
                new("jump-approach", 0.2f, new DemoInputState { Move = Vector2.up }),
                new("jump", 0.1f, new DemoInputState { Move = Vector2.up, Jump = true }),
                new("jump-recover", 0.65f, default),
                new("sneak-toggle", 0.1f, new DemoInputState { Sneak = true }),
                new("sneak-walk", 0.9f, new DemoInputState { Move = new Vector2(0f, 0.4f) }),
                new("sneak-off", 0.1f, new DemoInputState { Sneak = true }),
                new("end", 0.3f, default)
            };

            return new DemoClip("movement", steps, k_FrameRate, k_StageSettleSeconds);
        }

        private static DemoClip BuildEnemyClip()
        {
            var steps = new List<DemoStep>
            {
                new("stage", 2f, default, StageAtEnemy),
                new("lock-on", 0.15f, new DemoInputState { LockOn = true }),
                new("lock-on-recover", 0.35f, default),
                new("approach", 0.6f, new DemoInputState { Move = Vector2.up }),
                new("light-1", 0.1f, new DemoInputState { LightAttack = true }),
                new("light-1-recover", 0.5f, default),
                new("light-2", 0.1f, new DemoInputState { LightAttack = true }),
                new("light-2-recover", 0.5f, default),
                new("dodge-back", 0.1f, new DemoInputState { Dodge = true }),
                new("dodge-recover", 0.7f, default),
                new("heavy", 0.1f, new DemoInputState { HeavyAttack = true }),
                new("heavy-recover", 0.9f, default),
                new("light-3", 0.1f, new DemoInputState { LightAttack = true }),
                new("light-3-recover", 0.8f, default),
                new("end", 0.4f, default)
            };

            return new DemoClip("enemy", steps, k_FrameRate, k_StageSettleSeconds);
        }

        /// <summary>HUD, character menu and equipment panel over the gameplay scene.</summary>
        private static DemoClip BuildUiClip()
        {
            var steps = new List<DemoStep>
            {
                new("stage", 1.5f, default, StageAtScenicSpot),
                new("hud", 1f, default),
                new("open-character-menu", 1.6f, default, DemoMenuActions.OpenInGameCharacterMenu),
                new("open-equipment", 1.4f, default, DemoMenuActions.OpenEquipmentMenu),
                new("select-slot", 1f, default, () => DemoMenuActions.SelectEquipmentSlot(1)),
                new("close", 0.8f, default, DemoMenuActions.CloseInGameMenus)
            };

            return new DemoClip("ui", steps, k_FrameRate, k_StageSettleSeconds, true, false);
        }

        /// <summary>
        /// Teleports through candidate poses so framing can be picked from stills before a real
        /// recording is made. One segment per pose, in list order.
        /// </summary>
        private static DemoClip BuildScoutClip()
        {
            (float x, float z, float yaw)[] poses =
            {
                (k_EnemyPoseX, k_EnemyPoseZ, 0f),
                (k_EnemyPoseX, k_EnemyPoseZ, 90f),
                (k_EnemyPoseX, k_EnemyPoseZ, 180f),
                (k_EnemyPoseX, k_EnemyPoseZ, 270f),
                (228f, 244f, 0f),
                (242f, 256f, 0f)
            };

            var steps = new List<DemoStep>();
            foreach ((float x, float z, float yaw) in poses)
            {
                steps.Add(new DemoStep(
                    $"scout-{steps.Count}",
                    2.5f,
                    default,
                    () => DemoStage.PlacePlayer(x, z, yaw)));
            }

            return new DemoClip("scout", steps, 30, 0.5f);
        }

        private static void StageAtScenicSpot()
        {
            DemoStage.PlacePlayer(k_ScenicX, k_ScenicZ, k_ScenicYaw);
        }

        private static void StageAtEnemy()
        {
            DemoStage.PlacePlayer(k_EnemyPoseX, k_EnemyPoseZ, k_EnemyPoseYaw);
        }
    }
}
