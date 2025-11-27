using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.GameLogic.Services;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Models.Instructions;
using Xunit.Abstractions;

namespace Werewolves.Tests.Helpers;

/// <summary>
/// Fluent builder for creating test game scenarios with minimal boilerplate.
/// </summary>
public class GameTestBuilder
{
    private List<string> _playerNames = [];
    private List<MainRoleType> _roles = [];
    private readonly GameService _gameService = new();
    private Guid _gameId;
    private bool _gameStarted;
    private ModeratorInstruction? _lastInstruction = null;
    private readonly DiagnosticStateObserver _diagnosticObserver = new();
    private readonly ITestOutputHelper? _output;

    private GameTestBuilder(ITestOutputHelper? output = null)
    {
        _output = output;
    }

	/// <summary>
	/// Creates a new test builder instance.
	/// </summary>
	public static GameTestBuilder Create(ITestOutputHelper? output = null) => new(output);

    /// <summary>
    /// Adds players with auto-generated names (Player1, Player2, etc.).
    /// </summary>
    public GameTestBuilder WithPlayers(int count)
    {
        _playerNames = Enumerable.Range(1, count)
            .Select(i => $"Player{i}")
            .ToList();
        return this;
    }

    /// <summary>
    /// Adds players with specific names in seating order.
    /// </summary>
    public GameTestBuilder WithPlayers(params string[] names)
    {
        _playerNames = [.. names];
        return this;
    }

    /// <summary>
    /// Sets the roles for the game. Count must match player count.
    /// </summary>
    public GameTestBuilder WithRoles(params MainRoleType[] roles)
    {
        _roles = [.. roles];
        return this;
    }

    /// <summary>
    /// Creates a simple game with werewolves, seer, and villagers.
    /// </summary>
    /// <param name="playerCount">Total players (minimum 4)</param>
    /// <param name="werewolfCount">Number of werewolves (default 1)</param>
    /// <param name="includeSeer">Include a seer (default true)</param>
    public GameTestBuilder WithSimpleGame(int playerCount, int werewolfCount = 1, bool includeSeer = true)
    {
        if (playerCount < 3)
            throw new ArgumentException("Minimum 3 players required", nameof(playerCount));

        _playerNames = Enumerable.Range(1, playerCount)
            .Select(i => $"Player{i}")
            .ToList();

        _roles = [];
        
        // Add werewolves
        for (int i = 0; i < werewolfCount; i++)
            _roles.Add(MainRoleType.SimpleWerewolf);
        
        // Add seer if requested
        if (includeSeer)
            _roles.Add(MainRoleType.Seer);
        
        // Fill remaining with villagers
        int villagersNeeded = playerCount - _roles.Count;
        for (int i = 0; i < villagersNeeded; i++)
            _roles.Add(MainRoleType.SimpleVillager);

        return this;
    }

    /// <summary>
    /// Starts the game and returns the confirmation instruction.
    /// </summary>
    public StartGameConfirmationInstruction StartGame()
    {
        if (_playerNames.Count != _roles.Count)
            throw new InvalidOperationException(
                $"Player count ({_playerNames.Count}) must match role count ({_roles.Count})");

        var instruction = _gameService.StartNewGameWithObserver(_playerNames, _roles, stateChangeObserver: _diagnosticObserver);
        _lastInstruction = instruction;
		_gameId = instruction.GameGuid;
        _gameStarted = true;
        
        // Wire up session for GUID-to-name resolution in diagnostics
        var session = _gameService.GetGameStateView(_gameId);
        if (session != null)
            _diagnosticObserver.SetSession(session);
        
        return instruction;
    }

    /// <summary>
    /// Confirms the game start and transitions to Night phase.
    /// </summary>
    public ProcessResult ConfirmGameStart()
    {
        EnsureGameStarted();
        var instruction = _lastInstruction as StartGameConfirmationInstruction
            ?? throw new InvalidOperationException("Last instruction is not a StartGameConfirmationInstruction");
        var response = instruction.CreateResponse(true);
		return _gameService.ProcessInstruction(_gameId, response);
    }

    /// <summary>
    /// Processes a moderator response and returns the result.
    /// </summary>
    public ProcessResult Process(ModeratorResponse response)
    {
        EnsureGameStarted();
        return _gameService.ProcessInstruction(_gameId, response);
    }

    /// <summary>
    /// Gets the current game state.
    /// </summary>
    public IGameSession? GetGameState() => _gameService.GetGameStateView(_gameId);

    /// <summary>
    /// Gets the current pending instruction.
    /// </summary>
    public ModeratorInstruction? GetCurrentInstruction() => _gameService.GetCurrentInstruction(_gameId);

    /// <summary>
    /// Gets the game ID.
    /// </summary>
    public Guid GameId => _gameId;

    /// <summary>
    /// Gets the underlying game service for advanced scenarios.
    /// </summary>
    public GameService GameService => _gameService;

    /// <summary>
    /// Gets player names in seating order.
    /// </summary>
    public IReadOnlyList<string> PlayerNames => _playerNames;

    /// <summary>
    /// Gets roles in play.
    /// </summary>
    public IReadOnlyList<MainRoleType> Roles => _roles;

    /// <summary>
    /// Gets the formatted diagnostic log of all state changes.
    /// </summary>
    public string DiagnosticLog => _diagnosticObserver.GetFormattedLog();

    /// <summary>
    /// Gets the raw observer log entries for assertions.
    /// </summary>
    public IReadOnlyList<string> ObserverLog => _diagnosticObserver.Log;

    /// <summary>
    /// Clears the observer log (useful for focusing on specific transitions).
    /// </summary>
    public void ClearObserverLog() => _diagnosticObserver.Clear();

    /// <summary>
    /// Writes the diagnostic log to the test output.
    /// </summary>
    public void DumpDiagnostics() => _output?.WriteLine(DiagnosticLog);

    #region Night Phase Helpers

    /// <summary>
    /// Confirms the "night starts" instruction that precedes the hook loop.
    /// </summary>
    /// <returns>The result of processing the night start confirmation.</returns>
    public ProcessResult ConfirmNightStart()
    {
        EnsureGameStarted();
        var nightStartInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            GetCurrentInstruction(),
            "Night start confirmation");
        var response = nightStartInstruction.CreateResponse(true);
        return Process(response);
    }

    /// <summary>
    /// Completes the werewolf night action sequence: identify → select victim → confirm sleep.
    /// </summary>
    /// <param name="werewolfIds">The IDs of all werewolf players to identify.</param>
    /// <param name="victimId">The ID of the player to select as the victim.</param>
    /// <returns>The result of the final sleep confirmation.</returns>
    public ProcessResult CompleteWerewolfNightAction(List<Guid> werewolfIds, Guid victimId)
    {
        EnsureGameStarted();

        // Identify werewolves
        var identifyInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            GetCurrentInstruction(),
            "Werewolf identification");
        var identifyResponse = identifyInstruction.CreateResponse(werewolfIds);
        var afterIdentify = Process(identifyResponse);

        // Select victim
        var victimInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterIdentify,
            "Werewolf victim selection");
        var victimResponse = victimInstruction.CreateResponse([victimId]);
        var afterVictim = Process(victimResponse);

        // Confirm sleep
        var sleepInstruction = InstructionAssert.ExpectSuccessWithType<ConfirmationInstruction>(
            afterVictim,
            "Werewolf sleep confirmation");
        var sleepResponse = sleepInstruction.CreateResponse(true);
        return Process(sleepResponse);
    }

    /// <summary>
    /// Completes the Seer night action sequence: identify → select target → confirm sleep.
    /// </summary>
    /// <param name="seerId">The ID of the Seer player to identify.</param>
    /// <param name="targetId">The ID of the player for the Seer to investigate.</param>
    /// <returns>The result of the final sleep confirmation.</returns>
    public ProcessResult CompleteSeerNightAction(Guid seerId, Guid targetId)
    {
        EnsureGameStarted();

        // Identify seer
        var identifyInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            GetCurrentInstruction(),
            "Seer identification");
        var identifyResponse = identifyInstruction.CreateResponse([seerId]);
        var afterIdentify = Process(identifyResponse);

        // Select target
        var targetInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterIdentify,
            "Seer target selection");
        var targetResponse = targetInstruction.CreateResponse([targetId]);
        var afterTarget = Process(targetResponse);

        // Confirm sleep
        var sleepInstruction = InstructionAssert.ExpectSuccessWithType<ConfirmationInstruction>(
            afterTarget,
            "Seer sleep confirmation");
        var sleepResponse = sleepInstruction.CreateResponse(true);
        return Process(sleepResponse);
    }

    /// <summary>
    /// Completes a full night phase with werewolf and optional Seer actions.
    /// </summary>
    /// <param name="werewolfIds">The IDs of all werewolf players.</param>
    /// <param name="victimId">The ID of the werewolf victim.</param>
    /// <param name="seerId">Optional: The ID of the Seer player. If null, Seer actions are skipped.</param>
    /// <param name="seerTargetId">Optional: The ID of the player for the Seer to investigate. Required if seerId is provided.</param>
    /// <returns>The result of the final action in the night phase.</returns>
    public ProcessResult CompleteNightPhase(List<Guid> werewolfIds, Guid victimId, Guid? seerId = null, Guid? seerTargetId = null)
    {
        EnsureGameStarted();

        // Confirm night starts
        ConfirmNightStart();

        // Complete werewolf actions
        var result = CompleteWerewolfNightAction(werewolfIds, victimId);

        // Complete Seer actions if specified
        if (seerId.HasValue)
        {
            if (!seerTargetId.HasValue)
                throw new ArgumentException("seerTargetId must be provided when seerId is specified", nameof(seerTargetId));

            result = CompleteSeerNightAction(seerId.Value, seerTargetId.Value);
        }

        return result;
    }

    #endregion

    #region Dawn Phase Helpers

    /// <summary>
    /// Completes the dawn phase flow: CalculateVictims → AnnounceVictims (with role assignments) → DawnMainActionLoop → Finalize → Day.
    /// Handles variable number of victims (0 to many) by processing instructions until Day phase is reached.
    /// </summary>
    /// <returns>The result of the final instruction that transitions to Day phase.</returns>
    public ProcessResult CompleteDawnPhase()
    {
        EnsureGameStarted();

        ProcessResult result;

        // Process instructions until we reach Day phase
        while (true)
        {
            var instruction = GetCurrentInstruction();
            var currentPhase = GetGameState()?.GetCurrentPhase();

            // If we've reached Day phase, we're done
            if (currentPhase == GamePhase.Day)
            {
                // Return a success result with the current instruction
                return ProcessResult.Success(instruction!);
            }

            // Handle different instruction types during dawn
            result = instruction switch
            {
                AssignRolesInstruction assignRoles => HandleAssignRolesInstruction(assignRoles),
                ConfirmationInstruction confirmation => Process(confirmation.CreateResponse(true)),
                SelectPlayersInstruction selectPlayers => throw new InvalidOperationException(
                    $"Unexpected SelectPlayersInstruction during dawn phase. " +
                    $"Dawn hooks requiring player selection are not handled by CompleteDawnPhase(). " +
                    $"Instruction: {selectPlayers.PrivateInstruction}"),
                null => throw new InvalidOperationException("No current instruction available during dawn phase processing."),
                _ => throw new InvalidOperationException(
                    $"Unexpected instruction type during dawn phase: {instruction.GetType().Name}")
            };

            if (!result.IsSuccess)
            {
                return result;
            }
        }
    }

    /// <summary>
    /// Handles AssignRolesInstruction by assigning SimpleVillager to all players.
    /// This is appropriate for test scenarios where the actual role doesn't matter for the test logic.
    /// </summary>
    private ProcessResult HandleAssignRolesInstruction(AssignRolesInstruction instruction)
    {
        var assignments = new Dictionary<Guid, MainRoleType>();
        
        foreach (var playerId in instruction.PlayersForAssignment)
        {
            // Use the first available role from the list (typically matches the actual role)
            // For simple test scenarios, SimpleVillager is often available
            var roleToAssign = instruction.RolesForAssignment.FirstOrDefault(MainRoleType.SimpleVillager);
            assignments[playerId] = roleToAssign;
        }

        var response = instruction.CreateResponse(assignments);
        return Process(response);
    }

    #endregion

    #region Day Phase Helpers

    /// <summary>
    /// Completes the day phase with a player being lynched.
    /// Flow: Debate → DetermineVoteType → NormalVoting → ProcessVoteOutcome → RoleAssignment → Finalize → Night.
    /// </summary>
    /// <param name="lynchTargetId">The ID of the player to be lynched.</param>
    /// <returns>The result of the final instruction that transitions to Night phase.</returns>
    public ProcessResult CompleteDayPhaseWithLynch(Guid lynchTargetId)
    {
        return CompleteDayPhaseCore(lynchTargetId);
    }

    /// <summary>
    /// Completes the day phase with a tie vote (no elimination).
    /// Flow: Debate → DetermineVoteType → NormalVoting → ProcessVoteOutcome → Finalize → Night.
    /// </summary>
    /// <returns>The result of the final instruction that transitions to Night phase.</returns>
    public ProcessResult CompleteDayPhaseWithTie()
    {
        return CompleteDayPhaseCore(null);
    }

    /// <summary>
    /// Core implementation for completing the day phase.
    /// Handles both lynch and tie scenarios by processing instructions until Night phase is reached.
    /// </summary>
    /// <param name="lynchTargetId">The ID of the player to lynch, or null for a tie vote.</param>
    /// <returns>The result of the final instruction that transitions to Night phase.</returns>
    private ProcessResult CompleteDayPhaseCore(Guid? lynchTargetId)
    {
        EnsureGameStarted();

        ProcessResult result;

        // Process instructions until we reach Night phase
        while (true)
        {
            var instruction = GetCurrentInstruction();
            var currentPhase = GetGameState()?.GetCurrentPhase();

            // If we've reached Night phase, we're done
            if (currentPhase == GamePhase.Night)
            {
                // Return a success result with the current instruction
                return ProcessResult.Success(instruction!);
            }

            // Handle different instruction types during day phase
            result = instruction switch
            {
                SelectPlayersInstruction selectPlayers => HandleDayVotingInstruction(selectPlayers, lynchTargetId),
                AssignRolesInstruction assignRoles => HandleAssignRolesInstruction(assignRoles),
                ConfirmationInstruction confirmation => Process(confirmation.CreateResponse(true)),
                null => throw new InvalidOperationException("No current instruction available during day phase processing."),
                _ => throw new InvalidOperationException(
                    $"Unexpected instruction type during day phase: {instruction.GetType().Name}")
            };

            if (!result.IsSuccess)
            {
                return result;
            }
        }
    }

    /// <summary>
    /// Handles SelectPlayersInstruction during day voting.
    /// Selects the lynch target if provided, otherwise selects no players (tie).
    /// </summary>
    private ProcessResult HandleDayVotingInstruction(SelectPlayersInstruction instruction, Guid? lynchTargetId)
    {
        var selectedPlayers = lynchTargetId.HasValue
            ? new List<Guid> { lynchTargetId.Value }
            : new List<Guid>();

        var response = instruction.CreateResponse(selectedPlayers);
        return Process(response);
    }

    #endregion

    private void EnsureGameStarted()
    {
        if (!_gameStarted)
            throw new InvalidOperationException("Game must be started first. Call StartGame().");
    }
}
