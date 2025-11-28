using FluentAssertions;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models.Instructions;
using Werewolves.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Werewolves.Tests.Integration;

/// <summary>
/// Tests for victory conditions: villager victory, werewolf victory, and timing.
/// Test IDs: VC-001 through VC-022
/// </summary>
public class VictoryConditionTests : DiagnosticTestBase
{
    public VictoryConditionTests(ITestOutputHelper output) : base(output) { }

    #region VC-001 to VC-002: Villager Victory

    /// <summary>
    /// VC-001: Last werewolf killed by special ability at dawn triggers villager victory.
    /// Skipped: Simple game has no roles that kill werewolves at dawn (Knight, Witch not implemented).
    /// </summary>
    [Fact(Skip = "Requires roles not in simple game scope (Knight's rusty sword, Witch's poison)")]
    public void WerewolfEliminated_AtDawn_VillagerVictory()
    {
        // This test would require:
        // - Knight role that inflicts rusty sword on attacking werewolf
        // - Witch role with poison potion
        // - Other dawn-kill mechanics
        // Currently not implemented in simple game.
    }

    /// <summary>
    /// VC-002: Last werewolf voted out during day triggers villager victory.
    /// </summary>
    [Fact]
    public void WerewolfEliminated_AtDay_VillagerVictory()
    {
        // Arrange - 4 players: 1 WW, 3 Villagers (need 3 villagers so game continues to Day after night kill)
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: false);
        builder.StartGame();
        builder.ConfirmGameStart();

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        // Player 0 is the werewolf (first role assigned)
        var werewolf = players[0];
        var villager1 = players[1];

        // Night 1: Werewolf kills a villager
        builder.ConfirmNightStart();
        builder.CompleteWerewolfNightAction([werewolf.Id], villager1.Id);

        // Confirm night end
        var nightEndInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night end confirmation");
        builder.Process(nightEndInstruction.CreateResponse(true));

        // Dawn: Process victim elimination (villager1 dies)
        builder.CompleteDawnPhase(new Dictionary<Guid, MainRoleType>
        {
            { villager1.Id, MainRoleType.SimpleVillager }
        });

        // Day: Vote out the werewolf
        // Confirm debate
        var debateInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Debate confirmation");
        builder.Process(debateInstruction.CreateResponse(true));

        // Vote for werewolf
        var voteInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction(),
            "Vote selection");
        builder.Process(voteInstruction.CreateResponse([werewolf.Id]));

        // Death announcement confirmation (role already known from night wake)
        var deathAnnouncementInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Death announcement confirmation");
        var result = builder.Process(deathAnnouncementInstruction.CreateResponse(true));

        // Assert - Should get FinishedGameConfirmationInstruction
        var finalInstruction = result.ModeratorInstruction;
        finalInstruction.Should().BeOfType<FinishedGameConfirmationInstruction>();

        // Verify victory log entry
        var updatedState = builder.GetGameState()!;
        var victoryLog = updatedState.GameHistoryLog
            .OfType<VictoryConditionMetLogEntry>()
            .SingleOrDefault();

        victoryLog.Should().NotBeNull();
        victoryLog!.WinningTeam.Should().Be(Team.Villagers);

        MarkTestCompleted();
    }

    #endregion

    #region VC-010 to VC-013: Werewolf Victory

    /// <summary>
    /// VC-010: When 1 werewolf and 1 villager remain, werewolves win.
    /// </summary>
    [Fact]
    public void WerewolvesEqualVillagers_WerewolvesWin()
    {
        // Arrange - 3 players: 1 WW, 2 Villagers
        // After werewolf kills 1 villager, we have 1 WW vs 1 Villager = WW wins
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 3, werewolfCount: 1, includeSeer: false);
        builder.StartGame();
        builder.ConfirmGameStart();

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        var werewolf = players[0];
        var villager1 = players[1];

        // Night 1: Werewolf kills villager1
        builder.ConfirmNightStart();
        builder.CompleteWerewolfNightAction([werewolf.Id], villager1.Id);

        // Confirm night end
        var nightEndInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night end confirmation");
        builder.Process(nightEndInstruction.CreateResponse(true));

        // Dawn: Process victim - after this, 1 WW vs 1 Villager
        builder.CompleteDawnPhase(new Dictionary<Guid, MainRoleType>
        {
            { villager1.Id, MainRoleType.SimpleVillager }
        });

        // Assert - Should get FinishedGameConfirmationInstruction (victory detected at dawn)
        var finalInstruction = builder.GetCurrentInstruction();
        finalInstruction.Should().BeOfType<FinishedGameConfirmationInstruction>();

        // Verify victory log entry
        var updatedState = builder.GetGameState()!;
        var victoryLog = updatedState.GameHistoryLog
            .OfType<VictoryConditionMetLogEntry>()
            .SingleOrDefault();

        victoryLog.Should().NotBeNull();
        victoryLog!.WinningTeam.Should().Be(Team.Werewolves);

        MarkTestCompleted();
    }

    /// <summary>
    /// VC-011: When 2 werewolves and 1 villager remain, werewolves win.
    /// </summary>
    [Fact]
    public void WerewolvesOutnumberVillagers_WerewolvesWin()
    {
        // Arrange - 4 players: 2 WW, 2 Villagers
        // After werewolf kills 1 villager, we have 2 WW vs 1 Villager = WW wins
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 2, includeSeer: false);
        builder.StartGame();
        builder.ConfirmGameStart();

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        // First 2 players are werewolves
        var werewolf1 = players[0];
        var werewolf2 = players[1];
        var villager1 = players[2];

        // Night 1: Werewolves kill villager1
        builder.ConfirmNightStart();
        builder.CompleteWerewolfNightAction([werewolf1.Id, werewolf2.Id], villager1.Id);

        // Confirm night end
        var nightEndInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night end confirmation");
        builder.Process(nightEndInstruction.CreateResponse(true));

        // Dawn: Process victim - after this, 2 WW vs 1 Villager
        builder.CompleteDawnPhase(new Dictionary<Guid, MainRoleType>
        {
            { villager1.Id, MainRoleType.SimpleVillager }
        });

        // Assert - Should get FinishedGameConfirmationInstruction (victory detected at dawn)
        var finalInstruction = builder.GetCurrentInstruction();
        finalInstruction.Should().BeOfType<FinishedGameConfirmationInstruction>();

        // Verify victory log entry
        var updatedState = builder.GetGameState()!;
        var victoryLog = updatedState.GameHistoryLog
            .OfType<VictoryConditionMetLogEntry>()
            .SingleOrDefault();

        victoryLog.Should().NotBeNull();
        victoryLog!.WinningTeam.Should().Be(Team.Werewolves);

        MarkTestCompleted();
    }

    /// <summary>
    /// VC-012: 1 werewolf, 2 villagers; werewolf kills 1 villager at dawn → werewolf victory.
    /// This is the same as VC-010 but explicitly named per tests.md.
    /// </summary>
    [Fact]
    public void VillagerKilled_AtDawn_WerewolfVictory()
    {
        // Arrange - 3 players: 1 WW, 2 Villagers
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 3, werewolfCount: 1, includeSeer: false);
        builder.StartGame();
        builder.ConfirmGameStart();

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        var werewolf = players[0];
        var villager1 = players[1];

        // Night 1: Werewolf kills villager1
        builder.ConfirmNightStart();
        builder.CompleteWerewolfNightAction([werewolf.Id], villager1.Id);

        // Confirm night end
        var nightEndInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night end confirmation");
        builder.Process(nightEndInstruction.CreateResponse(true));

        // Dawn: Process victim - victory should be detected after elimination
        builder.CompleteDawnPhase(new Dictionary<Guid, MainRoleType>
        {
            { villager1.Id, MainRoleType.SimpleVillager }
        });

        // Assert - Victory detected at dawn, game ends
        var finalInstruction = builder.GetCurrentInstruction();
        finalInstruction.Should().BeOfType<FinishedGameConfirmationInstruction>();

        var updatedState = builder.GetGameState()!;
        var victoryLog = updatedState.GameHistoryLog
            .OfType<VictoryConditionMetLogEntry>()
            .SingleOrDefault();

        victoryLog.Should().NotBeNull();
        victoryLog!.WinningTeam.Should().Be(Team.Werewolves);

        MarkTestCompleted();
    }

    /// <summary>
    /// VC-013: 1 werewolf, 3 villagers; werewolf kills 1 at night, 1 is voted out at day → werewolf victory.
    /// </summary>
    [Fact]
    public void VillagerKilled_AtDay_WerewolfVictory()
    {
        // Arrange - 4 players: 1 WW, 3 Villagers
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: false);
        builder.StartGame();
        builder.ConfirmGameStart();

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        var werewolf = players[0];
        var villager1 = players[1];
        var villager2 = players[2];
        var villager3 = players[3];

        // Night 1: Werewolf kills villager1 (now 1 WW vs 2 Villagers)
        builder.ConfirmNightStart();
        builder.CompleteWerewolfNightAction([werewolf.Id], villager1.Id);

        // Confirm night end
        var nightEndInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night end confirmation");
        builder.Process(nightEndInstruction.CreateResponse(true));

        // Dawn: Process victim
        builder.CompleteDawnPhase(new Dictionary<Guid, MainRoleType>
        {
            { villager1.Id, MainRoleType.SimpleVillager }
        });

        // Game continues (1 WW vs 2 Villagers)
        var gamePhase = builder.GetGameState()!.GetCurrentPhase();
        gamePhase.Should().Be(GamePhase.Day);

        // Day: Vote out villager2 (now 1 WW vs 1 Villager = WW wins)
        // Confirm debate
        var debateInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Debate confirmation");
        builder.Process(debateInstruction.CreateResponse(true));

        // Vote for villager2
        var voteInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction(),
            "Vote selection");
        builder.Process(voteInstruction.CreateResponse([villager2.Id]));

        // Assign villager role (SimpleVillager doesn't wake at night, so role is unknown)
        var roleInstruction = InstructionAssert.ExpectType<AssignRolesInstruction>(
            builder.GetCurrentInstruction(),
            "Villager role assignment");
        builder.Process(roleInstruction.CreateResponse(new Dictionary<Guid, MainRoleType>
        {
            { villager2.Id, MainRoleType.SimpleVillager }
        }));

        // Confirm death announcement
        var deathAnnouncementInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Death announcement confirmation");
        builder.Process(deathAnnouncementInstruction.CreateResponse(true));

        // Assert - Victory detected at Day.Finalize
        var finalInstruction = builder.GetCurrentInstruction();
        finalInstruction.Should().BeOfType<FinishedGameConfirmationInstruction>();

        var updatedState = builder.GetGameState()!;
        var victoryLog = updatedState.GameHistoryLog
            .OfType<VictoryConditionMetLogEntry>()
            .SingleOrDefault();

        victoryLog.Should().NotBeNull();
        victoryLog!.WinningTeam.Should().Be(Team.Werewolves);

        MarkTestCompleted();
    }

    #endregion

    #region VC-020 to VC-022: Victory Timing

    /// <summary>
    /// VC-020: Victory condition is checked and detected at dawn (before Day phase starts).
    /// </summary>
    [Fact]
    public void VictoryCondition_CheckedAtDawn()
    {
        // Arrange - 3 players: 1 WW, 2 Villagers
        // After night kill, 1 WW vs 1 Villager = victory at dawn, never reaches Day
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 3, werewolfCount: 1, includeSeer: false);
        builder.StartGame();
        builder.ConfirmGameStart();

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        var werewolf = players[0];
        var villager1 = players[1];

        // Night 1: Werewolf kills villager1
        builder.ConfirmNightStart();
        builder.CompleteWerewolfNightAction([werewolf.Id], villager1.Id);

        // Confirm night end
        var nightEndInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night end confirmation");
        builder.Process(nightEndInstruction.CreateResponse(true));

        // Dawn: Process victim
        builder.CompleteDawnPhase(new Dictionary<Guid, MainRoleType>
        {
            { villager1.Id, MainRoleType.SimpleVillager }
        });

        // Assert - Game ends at dawn, never reaches Day phase
        var finalInstruction = builder.GetCurrentInstruction();
        finalInstruction.Should().BeOfType<FinishedGameConfirmationInstruction>();

        // Verify victory detected at transition to Day phase (after dawn processing)
        var updatedState = builder.GetGameState()!;
        var victoryLog = updatedState.GameHistoryLog
            .OfType<VictoryConditionMetLogEntry>()
            .Single();

        victoryLog.CurrentPhase.Should().Be(GamePhase.Day);

        MarkTestCompleted();
    }

    /// <summary>
    /// VC-021: Victory condition is checked and detected after day vote.
    /// </summary>
    [Fact]
    public void VictoryCondition_CheckedAfterVote()
    {
        // Arrange - 4 players: 1 WW, 3 Villagers
        // Night: WW kills 1 (now 1 WW vs 2 Villagers)
        // Day: Vote out WW → Villager victory at Day.Finalize
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: false);
        builder.StartGame();
        builder.ConfirmGameStart();

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        var werewolf = players[0];
        var villager1 = players[1];

        // Night 1: Werewolf kills villager1
        builder.ConfirmNightStart();
        builder.CompleteWerewolfNightAction([werewolf.Id], villager1.Id);

        // Confirm night end
        var nightEndInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night end confirmation");
        builder.Process(nightEndInstruction.CreateResponse(true));

        // Dawn: Process victim
        builder.CompleteDawnPhase(new Dictionary<Guid, MainRoleType>
        {
            { villager1.Id, MainRoleType.SimpleVillager }
        });

        // Game continues to Day (1 WW vs 2 Villagers)
        builder.GetGameState()!.GetCurrentPhase().Should().Be(GamePhase.Day);

        // Day: Vote out the werewolf
        var debateInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Debate confirmation");
        builder.Process(debateInstruction.CreateResponse(true));

        var voteInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction(),
            "Vote selection");
        builder.Process(voteInstruction.CreateResponse([werewolf.Id]));

        var deathAnnouncementInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Death announcement confirmation");
        var result = builder.Process(deathAnnouncementInstruction.CreateResponse(true));

		// Assert - Victory detected at Day phase
		var finalInstruction = result.ModeratorInstruction;
        finalInstruction.Should().BeOfType<FinishedGameConfirmationInstruction>();

        var updatedState = builder.GetGameState()!;
        var victoryLog = updatedState.GameHistoryLog
            .OfType<VictoryConditionMetLogEntry>()
            .Single();

        victoryLog.CurrentPhase.Should().Be(GamePhase.Night);

        MarkTestCompleted();
    }

    /// <summary>
    /// VC-022: When no victory condition is met, game continues to next phase.
    /// </summary>
    [Fact]
    public void NoVictoryCondition_GameContinues()
    {
        // Arrange - 5 players: 1 WW, 4 Villagers (plenty of cushion)
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 5, werewolfCount: 1, includeSeer: false);
        builder.StartGame();
        builder.ConfirmGameStart();

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        var werewolf = players[0];
        var villager1 = players[1];

        // Night 1: Werewolf kills villager1 (now 1 WW vs 3 Villagers)
        builder.ConfirmNightStart();
        builder.CompleteWerewolfNightAction([werewolf.Id], villager1.Id);

        // Confirm night end
        var nightEndInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night end confirmation");
        builder.Process(nightEndInstruction.CreateResponse(true));

        // Dawn: Process victim
        builder.CompleteDawnPhase(new Dictionary<Guid, MainRoleType>
        {
            { villager1.Id, MainRoleType.SimpleVillager }
        });

        // Assert - Game continues to Day (no victory)
        var currentPhase = builder.GetGameState()!.GetCurrentPhase();
        currentPhase.Should().Be(GamePhase.Day);

        // No victory log should exist yet
        var victoryLogs = builder.GetGameState()!.GameHistoryLog
            .OfType<VictoryConditionMetLogEntry>()
            .ToList();
        victoryLogs.Should().BeEmpty();

        // Current instruction should NOT be FinishedGameConfirmationInstruction
        builder.GetCurrentInstruction().Should().NotBeOfType<FinishedGameConfirmationInstruction>();

        MarkTestCompleted();
    }

    #endregion
}
