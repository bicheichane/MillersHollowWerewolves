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
using static Werewolves.Core.Tests.TestHelper; // Add static imports
using static Werewolves.Core.Tests.TestModeratorInput; // Add static imports

namespace Werewolves.Core.Tests;

public class NightResolutionTests
{
    private readonly GameService _gameService = new();

    [Fact]
    public void DayResolveNight_ProcessWerewolfKill_ShouldEliminateVictimAndProceedToDayEvent()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var wolfId = pList[0];
        var victimId = pList[1];
        var victimName = session.Players[victimId].Name;

        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),         // -> Night
            Confirm(GamePhase.Night_RoleAction, true),         // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, wolfId), // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, victimId), // -> Day_ResolveNight
            Confirm(GamePhase.Day_ResolveNight, true) // -> Day_Event (Reveal Victim Role) - Step under test
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        var victimPlayer = session.Players[victimId];
        victimPlayer.Health.ShouldBe(PlayerHealth.Dead);

        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == victimId && pel.Reason == EliminationReason.WerewolfAttack);

        session.GamePhase.ShouldBe(GamePhase.Day_Event); // Should move to reveal role
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.AssignPlayerRoles);
        // Removed InstructionText assertions
        session.PendingModeratorInstruction.AffectedPlayerIds.ShouldBe(new[] { victimId });
    }

    // TODO: this test can't be run without the protector in place, or with some other role that prevents a death from happening during the night.
    /*
    [Fact]
    public void DayResolveNight_NoVictim_ShouldProceedToDebate()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob" }; // V, V (No WW)
        var roles = new List<RoleType> { RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = _gameService.StartNewGame(playerNames, roles);

        // Input sequence assuming no WWs means skipping WW ID/Action phases directly to ResolveNight confirmation
        // (Based on GameFlowManager transitions and expected GameService logic)
        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),                 // -> Night
            Confirm(GamePhase.Night, true),                 // -> Night (WW ID prompt)
            SelectPlayers(GamePhase.Night, new List<Guid>()), // -> Day_ResolveNight (No WWs identified/acted)
            Confirm(GamePhase.Day_ResolveNight, true)        // -> Day_Debate (No eliminations) - Step under test
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.Players.Values.ShouldAllBe(p => p.Status == PlayerStatus.Alive);
        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>().ShouldBeEmpty();

        session.GamePhase.ShouldBe(GamePhase.Day_Debate);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        // Removed InstructionText assertion
    }
    */
} 