using FluentAssertions;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models.Instructions;
using Werewolves.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Werewolves.Tests.Integration;

/// <summary>
/// Tests for day phase voting: vote outcomes, tie handling, elimination flow.
/// Test IDs: DV-001 through DV-011
/// </summary>
public class DayVotingTests : DiagnosticTestBase
{
    public DayVotingTests(ITestOutputHelper output) : base(output) { }

    #region DV-001: Debate Phase Transitions to Voting

    /// <summary>
    /// DV-001: Debate sub-phase transitions to voting.
    /// After dawn phase completes, game enters Day.Debate. Confirming debate leads to voting.
    /// </summary>
    [Fact]
    public void DebatePhase_TransitionsToVoting()
    {
        // Arrange: Simple game (4 players: 1 WW, 1 Seer, 2 Villagers)
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        var players = builder.GetGameState()!.GetPlayers().ToList();
        var werewolfId = players[0].Id;
        var seerId = players[1].Id;
        var villagerIds = new List<Guid> { players[2].Id, players[3].Id };

        // Complete night phase (werewolf kills a villager)
        builder.CompleteNightPhase(
            werewolfIds: [werewolfId],
            victimId: villagerIds[0],
            seerId: seerId,
            seerTargetId: villagerIds[1]);

        // Complete dawn phase
        builder.CompleteDawnPhase();

        // Assert: We're in Day phase, and should have a confirmation instruction for debate
        var gameState = builder.GetGameState()!;
        gameState.GetCurrentPhase().Should().Be(GamePhase.Day);

        var debateInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Debate confirmation instruction");

        // Act: Confirm debate is complete
        var afterDebate = builder.Process(debateInstruction.CreateResponse(true));
        afterDebate.IsSuccess.Should().BeTrue();

        // After DetermineVoteType (silent transition), we should get a voting instruction
        var votingInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterDebate,
            "Voting selection instruction");

        // Verify it's a voting instruction with appropriate constraints
        votingInstruction.CountConstraint.Should().NotBeNull();
        votingInstruction.CountConstraint!.Minimum.Should().Be(0, "Tie votes (0 players) should be allowed");
        votingInstruction.CountConstraint!.Maximum.Should().Be(1, "Only one player can be lynched");

        MarkTestCompleted();
    }

    #endregion

    #region DV-002 to DV-004: Normal Vote Flow

    /// <summary>
    /// DV-002: Vote outcome with single player selected requests role reveal.
    /// When a player is voted out, the game should request their role assignment.
    /// </summary>
    [Fact]
    public void VoteOutcome_SinglePlayer_RequestsRoleReveal()
    {
        // Arrange: Simple game (4 players: 1 WW, 1 Seer, 2 Villagers)
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        var players = builder.GetGameState()!.GetPlayers().ToList();
        var werewolfId = players[0].Id;
        var seerId = players[1].Id;
        var villager1Id = players[2].Id;
        var villager2Id = players[3].Id;

        // Complete night and dawn phases
        builder.CompleteNightPhase(
            werewolfIds: [werewolfId],
            victimId: villager1Id,
            seerId: seerId,
            seerTargetId: villager2Id);
        builder.CompleteDawnPhase();

        // Confirm debate
        var debateInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Debate confirmation");
        var afterDebate = builder.Process(debateInstruction.CreateResponse(true));

        // Get voting instruction
        var votingInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterDebate,
            "Voting instruction");

        // Act: Vote to lynch villager2 (who is still alive)
        var voteResponse = votingInstruction.CreateResponse([villager2Id]);
        var afterVote = builder.Process(voteResponse);

        // Assert: Should get a role assignment instruction for the lynched player
        var roleAssignInstruction = InstructionAssert.ExpectSuccessWithType<AssignRolesInstruction>(
            afterVote,
            "Role assignment instruction after lynch");

        roleAssignInstruction.PlayersForAssignment.Should().Contain(villager2Id);

        MarkTestCompleted();
    }

    /// <summary>
    /// DV-003: Vote elimination creates VoteOutcomeReportedLogEntry.
    /// </summary>
    [Fact]
    public void VoteElimination_CreatesVoteOutcomeLogEntry()
    {
        // Arrange: Simple game (4 players: 1 WW, 1 Seer, 2 Villagers)
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        var players = builder.GetGameState()!.GetPlayers().ToList();
        var werewolfId = players[0].Id;
        var seerId = players[1].Id;
        var villager1Id = players[2].Id;
        var villager2Id = players[3].Id;

        // Complete night and dawn phases
        builder.CompleteNightPhase(
            werewolfIds: [werewolfId],
            victimId: villager1Id,
            seerId: seerId,
            seerTargetId: villager2Id);
        builder.CompleteDawnPhase();

        // Confirm debate
        var debateInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Debate confirmation");
        var afterDebate = builder.Process(debateInstruction.CreateResponse(true));

        // Get voting instruction and vote
        var votingInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterDebate,
            "Voting instruction");

        // Act: Vote to lynch villager2
        var voteResponse = votingInstruction.CreateResponse([villager2Id]);
        builder.Process(voteResponse);

        // Assert: VoteOutcomeReportedLogEntry should exist with correct player
        var gameState = builder.GetGameState()!;
        var voteLogs = gameState.GameHistoryLog
            .OfType<VoteOutcomeReportedLogEntry>()
            .ToList();

        voteLogs.Should().HaveCount(1);
        voteLogs[0].ReportedOutcomePlayerId.Should().Be(villager2Id);

        MarkTestCompleted();
    }

    /// <summary>
    /// DV-004: Vote elimination sets player health to Dead.
    /// </summary>
    [Fact]
    public void VoteElimination_PlayerHealthSetToDead()
    {
        // Arrange: Simple game (4 players: 1 WW, 1 Seer, 2 Villagers)
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        var players = builder.GetGameState()!.GetPlayers().ToList();
        var werewolfId = players[0].Id;
        var seerId = players[1].Id;
        var villager1Id = players[2].Id;
        var villager2Id = players[3].Id;

        // Complete night and dawn phases
        builder.CompleteNightPhase(
            werewolfIds: [werewolfId],
            victimId: villager1Id,
            seerId: seerId,
            seerTargetId: villager2Id);
        builder.CompleteDawnPhase();

        // Act: Complete day phase with lynch
        builder.CompleteDayPhaseWithLynch(villager2Id);

        // Assert: Lynched player should be dead
        var gameState = builder.GetGameState()!;
        var lynchedPlayer = gameState.GetPlayers().First(p => p.Id == villager2Id);
        lynchedPlayer.State.Health.Should().Be(PlayerHealth.Dead);

        // Also verify PlayerEliminatedLogEntry was created
        var eliminationLogs = gameState.GameHistoryLog
            .OfType<PlayerEliminatedLogEntry>()
            .Where(e => e.PlayerId == villager2Id && e.Reason == EliminationReason.DayVote)
            .ToList();

        eliminationLogs.Should().HaveCount(1);

        MarkTestCompleted();
    }

    #endregion

    #region DV-010 to DV-011: Tie Votes

    /// <summary>
    /// DV-010: Tie vote (no player selected) results in no elimination.
    /// </summary>
    [Fact]
    public void TieVote_NoPlayerSelected_NoElimination()
    {
        // Arrange: Simple game (4 players: 1 WW, 1 Seer, 2 Villagers)
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        var players = builder.GetGameState()!.GetPlayers().ToList();
        var werewolfId = players[0].Id;
        var seerId = players[1].Id;
        var villager1Id = players[2].Id;
        var villager2Id = players[3].Id;

        // Complete night and dawn phases
        builder.CompleteNightPhase(
            werewolfIds: [werewolfId],
            victimId: villager1Id,
            seerId: seerId,
            seerTargetId: villager2Id);
        builder.CompleteDawnPhase();

        // Get the count of living players before voting
        var livingPlayersBefore = builder.GetGameState()!.GetPlayers()
            .Count(p => p.State.Health == PlayerHealth.Alive);

        // Confirm debate
        var debateInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Debate confirmation");
        var afterDebate = builder.Process(debateInstruction.CreateResponse(true));

        // Get voting instruction
        var votingInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterDebate,
            "Voting instruction");

        // Act: Vote with no player selected (tie)
        var tieResponse = votingInstruction.CreateResponse([]);
        builder.Process(tieResponse);

        // Complete remaining day phase
        builder.CompleteDayPhaseWithTie();

        // Assert: No player eliminated during day voting (villager1 was killed at dawn)
        var gameState = builder.GetGameState()!;
        var dayEliminationLogs = gameState.GameHistoryLog
            .OfType<PlayerEliminatedLogEntry>()
            .Where(e => e.Reason == EliminationReason.DayVote)
            .ToList();

        dayEliminationLogs.Should().BeEmpty("no player should be eliminated on a tie vote");

        // Living player count should be same as before voting
        var livingPlayersAfter = gameState.GetPlayers()
            .Count(p => p.State.Health == PlayerHealth.Alive);
        livingPlayersAfter.Should().Be(livingPlayersBefore);

        MarkTestCompleted();
    }

    /// <summary>
    /// DV-011: Tie vote creates correct log entry with Empty playerId.
    /// </summary>
    [Fact]
    public void TieVote_LogsCorrectOutcome()
    {
        // Arrange: Simple game (4 players: 1 WW, 1 Seer, 2 Villagers)
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        var players = builder.GetGameState()!.GetPlayers().ToList();
        var werewolfId = players[0].Id;
        var seerId = players[1].Id;
        var villager1Id = players[2].Id;
        var villager2Id = players[3].Id;

        // Complete night and dawn phases
        builder.CompleteNightPhase(
            werewolfIds: [werewolfId],
            victimId: villager1Id,
            seerId: seerId,
            seerTargetId: villager2Id);
        builder.CompleteDawnPhase();

        // Confirm debate
        var debateInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Debate confirmation");
        var afterDebate = builder.Process(debateInstruction.CreateResponse(true));

        // Get voting instruction
        var votingInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterDebate,
            "Voting instruction");

        // Act: Vote with no player selected (tie)
        var tieResponse = votingInstruction.CreateResponse([]);
        builder.Process(tieResponse);

        // Assert: VoteOutcomeReportedLogEntry should exist with Guid.Empty
        var gameState = builder.GetGameState()!;
        var voteLogs = gameState.GameHistoryLog
            .OfType<VoteOutcomeReportedLogEntry>()
            .ToList();

        voteLogs.Should().HaveCount(1);
        voteLogs[0].ReportedOutcomePlayerId.Should().Be(Guid.Empty, 
            "tie vote should be logged with Empty playerId");

        MarkTestCompleted();
    }

    #endregion
}
