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
using Werewolves.Core.Resources;
using Xunit.Abstractions;
using static Werewolves.Core.Tests.TestHelper;
using static Werewolves.Core.Tests.TestModeratorInput;

namespace Werewolves.Core.Tests;

public class VictoryConditionTests
{
    private readonly GameService _gameService = new();
    private readonly ITestOutputHelper _output;

    public VictoryConditionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CheckVictory_WerewolfWin_WhenWWsEqualVillagers()
    {
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" };
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };

        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var aliceId = session.Players.Values.First(p => p.Name == "Alice").Id;
        var bobId = session.Players.Values.First(p => p.Name == "Bob").Id;
        var charlieId = session.Players.Values.First(p => p.Name == "Charlie").Id;

        var inputsToReachResolveVote = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),
            Confirm(GamePhase.Night_RoleAction, true),
            SelectPlayers(GamePhase.Night_RoleAction, aliceId),
            SelectPlayer(GamePhase.Night_RoleAction, bobId),
            Confirm(GamePhase.Day_ResolveNight, true)
        };

        var setupResult = ProcessInputSequence(_gameService, gameId, inputsToReachResolveVote);
        setupResult.IsSuccess.ShouldBeTrue("Setup sequence failed");

        session.Players[bobId].Health.ShouldBe(PlayerHealth.Dead);
        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == bobId && pel.Reason == EliminationReason.WerewolfAttack);

        session.GamePhase.ShouldBe(GamePhase.GameOver);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.None);

        session.GameHistoryLog.OfType<VictoryConditionMetLogEntry>()
            .ShouldContain(vcl => vcl.WinningTeam == Team.Werewolves);
    }

    [Fact]
    public void CheckVictory_VillagerWin_WhenWWsAreEliminated()
    {
        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager, RoleType.SimpleVillager };

        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var aliceId = session.Players.Values.First(p => p.Name == "Alice").Id;
        var bobId = session.Players.Values.First(p => p.Name == "Bob").Id;
        var charlieId = session.Players.Values.First(p => p.Name == "Charlie").Id;
        var daveId = session.Players.Values.First(p => p.Name == "Dave").Id;

        var inputsToReachResolveVote = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),
            Confirm(GamePhase.Night_RoleAction, true),
            SelectPlayers(GamePhase.Night_RoleAction, aliceId),
            SelectPlayer(GamePhase.Night_RoleAction, daveId),
            Confirm(GamePhase.Day_ResolveNight, true),
            AssignPlayerRoles(GamePhase.Day_Event, new(){{daveId, RoleType.SimpleVillager}}),
            Confirm(GamePhase.Day_Debate, true),
            SelectPlayer(GamePhase.Day_Vote, aliceId),
        };

        var setupResult = ProcessInputSequence(_gameService, gameId, inputsToReachResolveVote);
        setupResult.IsSuccess.ShouldBeTrue("Setup sequence failed");

        var finalInput = Confirm(GamePhase.Day_ResolveVote, true);

        var result = _gameService.ProcessModeratorInput(gameId, finalInput);

        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.Players[daveId].Health.ShouldBe(PlayerHealth.Dead);
        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == daveId && pel.Reason == EliminationReason.WerewolfAttack);
        session.Players[aliceId].Health.ShouldBe(PlayerHealth.Dead);
        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == aliceId && pel.Reason == EliminationReason.DayVote);

        session.GamePhase.ShouldBe(GamePhase.GameOver);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.None);

        session.GameHistoryLog.OfType<VictoryConditionMetLogEntry>()
            .ShouldContain(vcl => vcl.WinningTeam == Team.Villagers);
    }

    // Potential future test: Victory check after Night Resolution (e.g., WW kills last villager)
}
