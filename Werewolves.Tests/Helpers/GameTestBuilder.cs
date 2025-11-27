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

        var instruction = _gameService.StartNewGame(_playerNames, _roles, stateChangeObserver: _diagnosticObserver);
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
    /// Writes the diagnostic log to the test output.
    /// </summary>
    public void DumpDiagnostics() => _output?.WriteLine(DiagnosticLog);

    private void EnsureGameStarted()
    {
        if (!_gameStarted)
            throw new InvalidOperationException("Game must be started first. Call StartGame().");
    }
}
