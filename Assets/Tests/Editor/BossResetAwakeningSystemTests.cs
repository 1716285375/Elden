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
            return File.ReadAllText($"Assets/_Game/Scripts/{relativePath}");
        }

        private static Type GetRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
