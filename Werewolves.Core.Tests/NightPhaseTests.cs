using Werewolves.Core.Services;
using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Roles;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Resources; // Add using statement

namespace Werewolves.Core.Tests;

public enum NightPhaseTestStep
{
    StartNight,          // After Setup Confirmation
    PromptIdentifyWW,    // After Night Starts Confirmation
    PromptActWW          // After WW Identification Input
}

public class NightPhaseTests
{
    private readonly GameService _gameService = new();

    // Helper to advance game to a specific step in Night 1
    private Guid SetupGameAndAdvanceToNightPhaseStep(List<string> playerNames, List<RoleType> roles, NightPhaseTestStep targetStep, out Guid werewolfId)
    {
        werewolfId = Guid.Empty;
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);
        var wwPlayer = session.Players.Values.FirstOrDefault(p => roles[playerNames.IndexOf(p.Name)] == RoleType.SimpleWerewolf);
        if (wwPlayer != null) werewolfId = wwPlayer.Id;

        // 1. Process Setup Confirmation
        var setupConfirmInput = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };
        var setupResult = _gameService.ProcessModeratorInput(gameId, setupConfirmInput);
        setupResult.IsSuccess.ShouldBeTrue("Failed Setup Confirmation");
        if (targetStep == NightPhaseTestStep.StartNight) return gameId;

        // 2. Process Night Starts Confirmation
        var nightStartsConfirmInput = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };
        var nightStartsResult = _gameService.ProcessModeratorInput(gameId, nightStartsConfirmInput);
        nightStartsResult.IsSuccess.ShouldBeTrue("Failed Night Starts Confirmation");
        if (targetStep == NightPhaseTestStep.PromptIdentifyWW) return gameId;

        // 3. Process WW Identification
        if (werewolfId != Guid.Empty)
        {
            var wwIdentifyInput = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionMultiple, SelectedPlayerIds = new List<Guid> { werewolfId } };
            var wwIdResult = _gameService.ProcessModeratorInput(gameId, wwIdentifyInput);
            wwIdResult.IsSuccess.ShouldBeTrue("Failed WW Identification Input");
            if (targetStep == NightPhaseTestStep.PromptActWW) return gameId;
        }
        else
        {
            // Should not happen if test setup is correct
            throw new InvalidOperationException("Werewolf ID not found for identification step.");
        }

        // Should not reach here if targetStep is valid
        throw new ArgumentOutOfRangeException(nameof(targetStep), "Target step not handled in setup helper.");
    }

    [Fact]
    public void NightPhase_ProcessWerewolfIdentification_ShouldAssignRoleAndPromptAction()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = SetupGameAndAdvanceToNightPhaseStep(playerNames, roles, NightPhaseTestStep.PromptIdentifyWW, out var werewolfId);
        var session = _gameService.GetGameStateView(gameId);
        var v1Id = session.Players.Values.First(p => p.Name == "Bob").Id;
        var v2Id = session.Players.Values.First(p => p.Name == "Charlie").Id;

        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionMultiple, SelectedPlayerIds = new List<Guid> { werewolfId } };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ModeratorInstruction.ShouldNotBeNull();
        // Expect WW Action Prompt
        result.ModeratorInstruction.InstructionText.ShouldBe(GameStrings.WerewolvesChooseVictimPrompt);
        result.ModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionSingle);

        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.ShouldNotBeNull();
        session.PendingNight1IdentificationForRole.ShouldBeNull(); // Pending ID should be cleared

        // Check Role Assignment
        var wwPlayer = session.Players[werewolfId];
        wwPlayer.Role.ShouldNotBeNull();
        wwPlayer.Role.RoleType.ShouldBe(RoleType.SimpleWerewolf);
        wwPlayer.IsRoleRevealed.ShouldBeTrue();

        // Check Log Entry
        session.GameHistoryLog.OfType<InitialRoleAssignmentLogEntry>()
            .ShouldContain(l => l.PlayerId == werewolfId && l.AssignedRole == RoleType.SimpleWerewolf);

        // Check Next Instruction Details
        var nextInstruction = result.ModeratorInstruction;
        nextInstruction.SelectablePlayerIds.ShouldNotBeNull();
        nextInstruction.SelectablePlayerIds.ShouldBe(new[] { v1Id, v2Id }); // Only villagers selectable
        // AffectedPlayerIds might be set to the identified WW, check if implemented
         // nextInstruction.AffectedPlayerIds.ShouldBe(new[] { werewolfId });
    }

    [Fact]
    public void NightPhase_ProcessWerewolfIdentification_InvalidCount_ShouldFail()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = SetupGameAndAdvanceToNightPhaseStep(playerNames, roles, NightPhaseTestStep.PromptIdentifyWW, out var werewolfId);
        var session = _gameService.GetGameStateView(gameId);
        var v1Id = session.Players.Values.First(p => p.Name == "Bob").Id;

        // Select two players instead of one required WW
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionMultiple, SelectedPlayerIds = new List<Guid> { werewolfId, v1Id } };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        // Assuming a specific error code for incorrect identification count
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount);

        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.ShouldNotBeNull();
        session.PendingNight1IdentificationForRole.ShouldBe(RoleType.SimpleWerewolf); // Should still be pending
        session.Players[werewolfId].Role.ShouldBeNull(); // Role should not be assigned
        session.GameHistoryLog.OfType<InitialRoleAssignmentLogEntry>().ShouldBeEmpty(); // No log entry
    }


    // --- Tests for WW Action Processing (Now assume identification is done) ---

    [Fact]
    public void NightPhase_Werewolves_ProcessValidVictimSelection_ShouldTransitionToResolveNight()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = SetupGameAndAdvanceToNightPhaseStep(playerNames, roles, NightPhaseTestStep.PromptActWW, out var actorId);
        var session = _gameService.GetGameStateView(gameId);
        var victimId = session.Players.Values.First(p => p.Name == "Bob").Id;
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { victimId } };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ModeratorInstruction.ShouldNotBeNull();

        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Day_ResolveNight); // Correct Phase

        // Check GameHistoryLog for the action
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldContain(nal =>
            nal.ActionType == NightActionType.WerewolfVictimSelection &&
            nal.TargetId == victimId &&
            nal.ActorId == actorId);

        // Expect instruction announcing results
        session.PendingModeratorInstruction?.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation); // Confirmation to proceed after announcement
        session.PendingModeratorInstruction?.InstructionText.ShouldContain(session.Players[victimId].Name);
    }

    [Fact]
    public void NightPhase_Werewolves_ProcessTargetSelf_ShouldFail()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob" }; // WW, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager };
        var gameId = SetupGameAndAdvanceToNightPhaseStep(playerNames, roles, NightPhaseTestStep.PromptActWW, out var werewolfId);
        var session = _gameService.GetGameStateView(gameId);
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { werewolfId } }; // Target self

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.RuleViolation);
        result.Error.Code.ShouldBe(GameErrorCode.RuleViolation_TargetIsSelf);
        session = _gameService.GetGameStateView(gameId); // Re-fetch session after input processing
        session.GamePhase.ShouldBe(GamePhase.Night); // Should remain in Night phase
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldBeEmpty(); // No action log
    }

    [Fact]
    public void NightPhase_Werewolves_ProcessTargetAlly_ShouldFail()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, WW, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleWerewolf, RoleType.SimpleVillager };
        // Helper advances to PromptActWW for Alice after identifying both Alice and Bob as WWs
        // This setup might need adjustment if helper identifies only one WW at a time.
        // For now, assume helper identifies Alice, then prompts Alice to act.
        var gameId = SetupGameAndAdvanceToNightPhaseStep(playerNames, roles, NightPhaseTestStep.PromptActWW, out var actingWerewolfId); // actingWerewolfId = Alice
        var session = _gameService.GetGameStateView(gameId);
        var targetAllyId = session.Players.Values.First(p => p.Name == "Bob").Id;

        // Ensure Bob is identified as WW in the session state for the validation to work
        // The helper should have done this, but double-check/enforce if necessary
        session.Players[targetAllyId].Role = new SimpleWerewolfRole();
        session.Players[targetAllyId].IsRoleRevealed = true;

        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { targetAllyId } }; // Target ally

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.RuleViolation);
        result.Error.Code.ShouldBe(GameErrorCode.RuleViolation_TargetIsAlly);
        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.GamePhase.ShouldBe(GamePhase.Night);
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldBeEmpty(); // No action log
    }

    [Fact]
    public void NightPhase_Werewolves_ProcessTargetDeadPlayer_ShouldFail()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = SetupGameAndAdvanceToNightPhaseStep(playerNames, roles, NightPhaseTestStep.PromptActWW, out var werewolfId);
        var session = _gameService.GetGameStateView(gameId);
        var deadPlayerId = session.Players.Values.First(p => p.Name == "Bob").Id;
        session.Players[deadPlayerId].Status = PlayerStatus.Dead; // Make Bob dead before action prompt
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { deadPlayerId } };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.RuleViolation);
        result.Error.Code.ShouldBe(GameErrorCode.RuleViolation_TargetIsDead);
        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.GamePhase.ShouldBe(GamePhase.Night);
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldBeEmpty(); // No action log
    }

     [Fact]
    public void NightPhase_Werewolves_ProcessInvalidActionSelectionCount_ShouldFail()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = SetupGameAndAdvanceToNightPhaseStep(playerNames, roles, NightPhaseTestStep.PromptActWW, out var werewolfId);
        var session = _gameService.GetGameStateView(gameId);
        var v1Id = session.Players.Values.First(p => p.Name == "Bob").Id;
        var v2Id = session.Players.Values.First(p => p.Name == "Charlie").Id;
        // Victim selection expects single, provide two
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { v1Id, v2Id } };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount);
        session = _gameService.GetGameStateView(gameId); // Re-fetch session
        session.GamePhase.ShouldBe(GamePhase.Night);
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldBeEmpty(); // No action log
    }

    // Remove original test NightPhase_Werewolves_ShouldBePromptedForVictim as it's covered by setup/identification tests
    // Remove tests checking logs in GameHistoryLog instead of NightActionsLog as that correction was already applied.
} 