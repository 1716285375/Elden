using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace ZZ.Tests
{
    public class BossResetAwakeningSystemTests
    {
        [Test]
        public void CombatStanceClearsADeadTargetBeforeItsIdleFallback()
        {
            string source = ReadRuntimeSource(
                "Character/AI/States/CombatStanceAIState.cs");
            int deadTargetCheck = source.IndexOf(
                "character.CurrentTarget?.IsDead == true",
                StringComparison.Ordinal);
            int invalidTargetCheck = source.IndexOf(
                "!character.HasValidTarget",
                StringComparison.Ordinal);

            Assert.That(deadTargetCheck, Is.GreaterThanOrEqualTo(0));
            Assert.That(deadTargetCheck, Is.LessThan(invalidTargetCheck));
            Assert.That(source, Does.Contain("character.ClearTarget();"));
        }

        [Test]
        public void HostDeathDisablesEveryBossEncounterBeforeRevival()
        {
            Type worldAIType = GetRuntimeType("ZZ.WorldAIManager");
            string worldAISource = ReadRuntimeSource(
                "World Managers/AI/WorldAIManager.cs");
            string playerNetworkSource = ReadRuntimeSource(
                "Character/Player/PlayerNetworkManager.cs");
            string sessionSource = ReadRuntimeSource(
                "World Managers/WorldGameSessionManager.cs");

            Assert.That(
                worldAIType.GetMethod("DisableAllBossFights"),
                Is.Not.Null);
            Assert.That(worldAISource, Does.Contain("?.CompleteEncounter();"));
            Assert.That(
                playerNetworkSource,
                Does.Contain("WorldAIManager.Instance?.DisableAllBossFights();"));
            Assert.That(
                playerNetworkSource,
                Does.Contain("!IsOwner || !IsServer"));
            Assert.That(sessionSource, Does.Contain("ResetAllCharacters();"));
            Assert.That(
                sessionSource,
                Does.Contain("IsPerformingLoadingOperation == true"));
        }

        [Test]
        public void EncounterEndDelaysHealthBarRemovalAndStopsMusic()
        {
            Type bossHealthBarType = GetRuntimeType("ZZ.PlayerUIBossHealthBar");
            string healthBarSource = ReadRuntimeSource(
                "Character/Player/Player UI/PlayerUIBossHealthBar.cs");
            string arenaSource = ReadRuntimeSource(
                "World Managers/AI/BossArenaController.cs");

            Assert.That(
                bossHealthBarType.GetMethod("RemoveHPBar"),
                Is.Not.Null);
            Assert.That(
                healthBarSource,
                Does.Contain("new WaitForSecondsRealtime(k_DefaultRemovalDelay)"));
            Assert.That(arenaSource, Does.Contain("?.RemoveHPBar(m_boundBoss)"));
            Assert.That(arenaSource, Does.Contain("m_bossMusicSource.Stop();"));
        }

        [Test]
        public void OrdinaryCharactersAreNotMarkedDontDestroyOnLoad()
        {
            string source = ReadRuntimeSource("Character/CharacterManager.cs");

            Assert.That(source, Does.Not.Contain("DontDestroyOnLoad"));
        }

        [Test]
        public void BossAwakeningProgressFlowsFromSaveIntoRuntimeState()
        {
            Type spawnerType = GetRuntimeType("ZZ.AICharacterSpawner");
            Type managerType = GetRuntimeType("ZZ.AICharacterManager");
            string bossSource = ReadRuntimeSource(
                "Character/AI/Boss/BossCharacterManager.cs");

            Assert.That(
                spawnerType.GetProperty("HasBossBeenAwakened"),
                Is.Not.Null);
            Assert.That(
                managerType.GetProperty("HasBeenAwakenedAlready"),
                Is.Not.Null);
            Assert.That(
                managerType.GetMethod("RestoreBossAwakeningProgress"),
                Is.Not.Null);
            Assert.That(
                bossSource,
                Does.Contain("OriginSpawner?.HasBossBeenAwakened == true"));
        }

        [Test]
        public void ConsumedAwakeningSkipsWakeAnimationOnEveryPeer()
        {
            Type networkType = GetRuntimeType("ZZ.AICharacterNetworkManager");
            string managerSource = ReadRuntimeSource(
                "Character/AI/AICharacterManager.cs");
            string networkSource = ReadRuntimeSource(
                "Character/AI/AICharacterNetworkManager.cs");

            Assert.That(
                networkType.GetField("PlayWakingAnimationOnAwake"),
                Is.Not.Null);
            Assert.That(
                managerSource,
                Does.Contain("!m_hasBeenAwakenedAlready"));
            Assert.That(
                networkSource,
                Does.Contain("PlayWakingAnimationOnAwake.Value"));
            Assert.That(
                networkSource,
                Does.Contain("animatorManager?.PlayAwakeIdleAnimation();"));
        }

        [Test]
        public void BossResetRestoresSavedAwakeningBeforeResettingState()
        {
            string source = ReadRuntimeSource(
                "World Managers/AI/AICharacterSpawner.cs");
            int restoreProgress = source.IndexOf(
                "RestoreBossAwakeningProgress(",
                StringComparison.Ordinal);
            int resetAtSpawn = source.IndexOf(
                "ResetAtSpawnPoint(",
                StringComparison.Ordinal);

            Assert.That(restoreProgress, Is.GreaterThanOrEqualTo(0));
            Assert.That(restoreProgress, Is.LessThan(resetAtSpawn));
            Assert.That(source, Does.Contain("HasBossBeenAwakened"));
        }

        private static string ReadRuntimeSource(string relativePath)
        {
            relativePath = RemapRuntimeSourcePath(relativePath);
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }
        /// <summary>Maps a pre-refactor Script-relative path to the new layout.</summary>
        private static string RemapRuntimeSourcePath(string relativePath)
        {
            if (relativePath.StartsWith("Character/Player/Player UI/"))
                return "UI/Gameplay/Player/" + relativePath.Substring("Character/Player/Player UI/".Length);
            if (relativePath.StartsWith("Character/Player/"))
                return "Characters/Player/" + relativePath.Substring("Character/Player/".Length);
            if (relativePath.StartsWith("Character/AI/"))
                return "Characters/AI/" + relativePath.Substring("Character/AI/".Length);
            if (relativePath.StartsWith("Character/Effects/"))
                return "Characters/Common/Effects/" + relativePath.Substring("Character/Effects/".Length);
            if (relativePath.StartsWith("Character/Equipment/"))
                return "Characters/Common/Equipment/" + relativePath.Substring("Character/Equipment/".Length);
            if (relativePath.StartsWith("Character/Inventory/"))
                return "Characters/Common/Inventory/" + relativePath.Substring("Character/Inventory/".Length);
            if (relativePath.StartsWith("Character/Character UI/"))
                return "UI/Gameplay/Character/" + relativePath.Substring("Character/Character UI/".Length);
            if (relativePath.StartsWith("Character/Animation State Behaviors/"))
                return "Characters/Common/Animation State Behaviors/" + relativePath.Substring("Character/Animation State Behaviors/".Length);
            if (relativePath.StartsWith("Character/"))
                return "Characters/Common/" + relativePath.Substring("Character/".Length);
            if (relativePath.StartsWith("World Managers/AI/"))
                return "World/AI/" + relativePath.Substring("World Managers/AI/".Length);
            if (relativePath.StartsWith("World Managers/"))
                return "World/Managers/" + relativePath.Substring("World Managers/".Length);
            if (relativePath.StartsWith("World Objects/"))
                return "World/Objects/" + relativePath.Substring("World Objects/".Length);
            if (relativePath.StartsWith("Save System/"))
                return "Save/" + relativePath.Substring("Save System/".Length);
            if (relativePath.StartsWith("Menu Scene/"))
                return "UI/Frontend/" + relativePath.Substring("Menu Scene/".Length);
            if (relativePath.StartsWith("Effects/"))
                return "Combat/Effects/" + relativePath.Substring("Effects/".Length);
            if (relativePath.StartsWith("Damage/"))
                return "Combat/Damage/" + relativePath.Substring("Damage/".Length);
            if (relativePath.StartsWith("Actions/"))
                return "Combat/Actions/" + relativePath.Substring("Actions/".Length);
            if (relativePath.StartsWith("Projectiles/"))
                return "Combat/Projectiles/" + relativePath.Substring("Projectiles/".Length);
            if (relativePath.StartsWith("Spells/"))
                return "Abilities/Spells/" + relativePath.Substring("Spells/".Length);
            if (relativePath.StartsWith("Utility/"))
                return "Utilities/" + relativePath.Substring("Utility/".Length);
            return relativePath;
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
