using Werewolves.Core.Services;
using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Resources;
using static Werewolves.Core.Tests.TestHelper;
using static Werewolves.Core.Tests.TestModeratorInput;

namespace Werewolves.Core.Tests;

public class NightPhaseTests
{
    private readonly GameService _gameService = new();

    [Fact]
    public void NightPhase_ProcessWerewolfIdentification_ShouldAssignRoleAndPromptAction()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var werewolfId = pList[0];
        var villagerIds = pList.Skip(1).ToList();

		var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),         // -> Night
            Confirm(GamePhase.Night_RoleAction, true),         // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, werewolfId) // -> Night (WW Action) - This is the step under test
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.ShouldNotBeNull();

        // Check state after WW Identification input
        session.GamePhase.ShouldBe(GamePhase.Night_RoleAction); // Still in Night phase, but expecting WW action next
        //session.PendingNight1IdentificationForCurrentRole.ShouldBeNull(); // Pending ID should be cleared

        // Check Role Assignment
        var wwPlayer = session.Players[werewolfId];
        wwPlayer.Role.ShouldNotBeNull();
        wwPlayer.Role.RoleType.ShouldBe(RoleType.SimpleWerewolf);
        wwPlayer.IsRoleRevealed.ShouldBeTrue(); // Identification reveals role

        // Check Log Entry
        session.GameHistoryLog.OfType<InitialRoleAssignmentLogEntry>()
            .ShouldContain(l => l.PlayerId == werewolfId && l.AssignedRole == RoleType.SimpleWerewolf);

        // Check Next Instruction Details
        var nextInstruction = session.PendingModeratorInstruction;
        nextInstruction.ShouldNotBeNull();
        nextInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionSingle);
        nextInstruction.SelectablePlayerIds.ShouldNotBeNull();
        nextInstruction.SelectablePlayerIds.ShouldBe(villagerIds); // Only villagers selectable
        nextInstruction.AffectedPlayerIds.ShouldBe(new[] { werewolfId }); // WW is the actor
    }

    [Fact]
    public void NightPhase_ProcessWerewolfIdentification_InvalidCount_ShouldFail()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var werewolfId = pList[0];
        var v1Id = pList[1];

        // Setup sequence to reach WW Identification prompt
        var setupInputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),         // -> Night
            Confirm(GamePhase.Night_RoleAction, true)          // -> Night (WW ID)
        };
        var setupResult = ProcessInputSequence(_gameService, gameId, setupInputs);
        setupResult.IsSuccess.ShouldBeTrue("Setup for the test failed");

        // The actual invalid input to test (select two players)
        var invalidInput = SelectPlayers(GamePhase.Night_RoleAction, new List<Guid> { werewolfId, v1Id });

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, invalidInput); // Use ProcessModeratorInput for single invalid step

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount);

        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Night_RoleAction); // Should remain in Night phase
        //session.PendingNight1IdentificationForCurrentRole.ShouldBe(RoleType.SimpleWerewolf); // Should still be pending
        session.Players[werewolfId].Role.ShouldBeNull(); // Role should not be assigned
        session.Players[werewolfId].IsRoleRevealed.ShouldBeFalse();
        session.GameHistoryLog.OfType<InitialRoleAssignmentLogEntry>().ShouldBeEmpty(); // No log entry
        session.PendingModeratorInstruction?.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionMultiple); // Should still expect WW ID
    }

    [Fact]
    public void NightPhase_Werewolves_ProcessValidVictimSelection_ShouldTransitionToResolveNight()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var actorId = pList[0]; // Werewolf
        var victimId = pList[1]; // Villager

        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),         // -> Night
            Confirm(GamePhase.Night_RoleAction, true),         // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, actorId), // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, victimId) // -> Day_ResolveNight - Step under test
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Day_ResolveNight); // Correct Phase

        // Check GameHistoryLog for the action
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldContain(nal =>
            nal.ActionType == NightActionType.WerewolfVictimSelection &&
            nal.TargetId == victimId &&
            nal.ActorId == Guid.Empty);

        // Expect instruction for next phase
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
    }

    [Fact]
    public void NightPhase_Werewolves_ProcessTargetAlly_ShouldFail()
    {
        // Arrange
        var playerNames = GetPlayerNames(5);
        var roles = GetRoles(3,2);
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var actingWerewolfId = pList[0];
        var targetAllyId = pList[1];

        // Setup sequence to reach WW Action prompt for the first WW
        // Note: Assumes the first identified WW performs the action.
        var setupInputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),                 // -> Night
            Confirm(GamePhase.Night_RoleAction, true),                 // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, new List<Guid>{actingWerewolfId, targetAllyId}) // -> Night (WW Action for actingWerewolfId)
        };
        var setupResult = ProcessInputSequence(_gameService, gameId, setupInputs);
        setupResult.IsSuccess.ShouldBeTrue("Setup for the test failed");

        // The actual invalid input to test (target ally)
        var invalidInput = SelectPlayer(GamePhase.Night_RoleAction, targetAllyId);

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, invalidInput);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.RuleViolation);
        result.Error.Code.ShouldBe(GameErrorCode.RuleViolation_TargetIsAlly);

        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Night_RoleAction); // Should remain in Night phase
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldBeEmpty(); // No action log
        session.PendingModeratorInstruction?.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionSingle); // Still expecting WW Action
    }

    [Fact]
    public void NightPhase_Werewolves_ProcessTargetDeadPlayer_ShouldFail()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var werewolfId = pList[0];
        var deadPlayerId = pList[1];

        // Manually set player status for test setup
        var playerToKill = _gameService.GetGameStateView(gameId).Players[deadPlayerId];
        playerToKill.Health = PlayerHealth.Dead; // Make player dead

        // Setup sequence to reach WW Action prompt
        var setupInputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),         // -> Night
            Confirm(GamePhase.Night_RoleAction, true),         // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, werewolfId) // -> Night (WW Action)
        };
        var setupResult = ProcessInputSequence(_gameService, gameId, setupInputs);
        setupResult.IsSuccess.ShouldBeTrue("Setup for the test failed");

        // The actual invalid input to test (target dead player)
        var invalidInput = SelectPlayer(GamePhase.Night_RoleAction, deadPlayerId);

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, invalidInput);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.RuleViolation);
        result.Error.Code.ShouldBe(GameErrorCode.RuleViolation_TargetIsDead);

        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Night_RoleAction); // Should remain in Night phase
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldBeEmpty(); // No action log
        session.PendingModeratorInstruction?.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionSingle); // Still expecting WW Action
    }

    [Fact]
    public void NightPhase_Werewolves_ProcessInvalidActionSelectionCount_ShouldFail()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var werewolfId = pList[0];
        var v1Id = pList[1];
        var v2Id = pList[2];

        // Setup sequence to reach WW Action prompt
        var setupInputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),         // -> Night
            Confirm(GamePhase.Night_RoleAction, true),         // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, werewolfId) // -> Night (WW Action)
        };
        var setupResult = ProcessInputSequence(_gameService, gameId, setupInputs);
        setupResult.IsSuccess.ShouldBeTrue("Setup for the test failed");

        // The actual invalid input to test (select two players for single selection action)
        var invalidInput = SelectPlayers(GamePhase.Night_RoleAction, new List<Guid> { v1Id, v2Id });

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, invalidInput);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InputTypeMismatch);   //we're sending a multiple selection type when only single is expected. maybe we'll need a different test to check for count

		session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Night_RoleAction); // Should remain in Night phase
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldBeEmpty(); // No action log
        session.PendingModeratorInstruction?.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionSingle); // Still expecting WW Action
    }
} 