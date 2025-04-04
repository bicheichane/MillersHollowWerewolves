using Xunit;
using Shouldly;
using Werewolves.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Resources;

namespace Werewolves.Core.Tests
{
    public class BasicGameFlowTests
    {
        private readonly GameService _gameService;

        public BasicGameFlowTests()
        {
            // Ensure a fresh GameService for each test if it holds static state between games
            // If GameService were instance-based and managed sessions per instance,
            // instantiation could be here. Since it uses a static dictionary,
            // we might need cleanup logic or ensure tests don't interfere.
            // For now, assuming tests run sequentially or the static nature is handled.
            _gameService = new GameService();
        }

        [Fact]
        public void GameInitialization_ShouldSetInitialStateCorrectly()
        {
            // Arrange
            var playerNames = new List<string> { "Alice", "Bob", "Charlie", "David", "Eve", "Frank", "Grace", "Heidi" };
            // List of roles that are in play (not assigned to specific players)
            var rolesInPlay = new List<RoleType>
            {
                RoleType.SimpleWerewolf, RoleType.SimpleWerewolf,
                RoleType.Seer,
                RoleType.Witch,
                RoleType.SimpleVillager, RoleType.SimpleVillager, RoleType.SimpleVillager, RoleType.SimpleVillager
            };

            // Action
            Guid gameId = Guid.Empty;
            Should.NotThrow(() => gameId = _gameService.StartNewGame(playerNames, rolesInPlay));

            // Assert
            gameId.ShouldNotBe(Guid.Empty);

            GameSession session = null!;
            Should.NotThrow(() => session = _gameService.GetGameSession(gameId));

            session.ShouldNotBeNull();
            session.Players.Count.ShouldBe(playerNames.Count);
            session.GamePhase.ShouldBe(GamePhase.Night);
            session.TurnNumber.ShouldBe(1);

            // Verify players exist but have no known roles initially
            foreach (var playerName in playerNames)
            {
                session.Players.Values.ShouldContain(p => p.Name == playerName);
                var player = session.Players.Values.First(p => p.Name == playerName);
                player.Status.ShouldBe(PlayerStatus.Alive);
                player.KnownRole.ShouldBeNull();
                player.IsRoleRevealed.ShouldBeFalse();
            }

            // Verify initial moderator instruction
            session.PendingModeratorInstruction.ShouldNotBeNull();
            session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelection);
            session.PendingModeratorInstruction.SelectablePlayerIds.ShouldNotBeNull();
            session.PendingModeratorInstruction.SelectablePlayerIds!.Count.ShouldBe(playerNames.Count);
            session.PendingModeratorInstruction.InstructionText.ShouldContain("Seer"); // Since Seer is in play
        }

        [Fact]
        public void ProcessModeratorInput_WithPlayerSelection_ShouldUpdateKnownRole()
        {
            // Arrange
            var playerNames = new List<string> { "Alice", "Bob", "Charlie" };
            var rolesInPlay = new List<RoleType> { RoleType.Seer, RoleType.SimpleVillager, RoleType.SimpleVillager };
            var gameId = _gameService.StartNewGame(playerNames, rolesInPlay);
            var session = _gameService.GetGameSession(gameId);

            // Verify initial state
            session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelection);
            session.PendingModeratorInstruction.SelectablePlayerIds.ShouldNotBeNull();
            session.PendingModeratorInstruction.SelectablePlayerIds!.Count.ShouldBe(3);

            // Action: Select Alice as the Seer
            var alice = session.Players.Values.First(p => p.Name == "Alice");
            var instruction = _gameService.ProcessModeratorInput(gameId, alice.Id);

            // Assert
            alice.KnownRole.ShouldNotBeNull();
            alice.KnownRole!.RoleType.ShouldBe(RoleType.Seer);
            alice.IsRoleRevealed.ShouldBeTrue();
        }

        // --- Placeholder Tests for other items in Section I ---

        [Fact(Skip = "Requires deterministic role assignment and input processing")]
        public void Thief_SwapWithVillager_ShouldUpdateRole()
        {
            // Arrange: Setup game with Thief, extra Villager/WW, specific players
            // Action: Start game, process Thief input selecting Villager card
            // Assert: Player's role is Villager, game progresses
            Assert.Fail("Test not implemented");
        }

        [Fact(Skip = "Requires deterministic role assignment and input processing")]
        public void Thief_SwapWithWerewolf_ShouldUpdateRoleAndWake()
        {
            // Arrange: Setup game with Thief, extra Villager/WW, specific players
            // Action: Start game, process Thief input selecting WW card
            // Assert: Player's role is WW, player wakes with WWs (verify via subsequent WW action/state)
             Assert.Fail("Test not implemented");
        }

         [Fact(Skip = "Requires deterministic role assignment and input processing")]
        public void Thief_ForcedSwapWithWerewolf_ShouldUpdateRole()
        {
             Assert.Fail("Test not implemented");
        }

        [Fact(Skip = "Requires deterministic role assignment, input processing, and state tracking")]
        public void Cupid_LinkTwoVillagers_ShouldSetLoverState()
        {
             Assert.Fail("Test not implemented");
        }

         [Fact(Skip = "Requires deterministic role assignment, input processing, and state tracking")]
        public void Cupid_LinkVillagerAndWerewolf_ShouldSetLoverState()
        {
             Assert.Fail("Test not implemented");
        }

        [Fact(Skip = "Requires deterministic role assignment, input processing, and state tracking")]
        public void Cupid_LinkSelf_ShouldSetLoverState()
        {
             Assert.Fail("Test not implemented");
        }
    }
} 