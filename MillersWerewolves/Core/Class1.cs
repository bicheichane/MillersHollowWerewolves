using static Core.LocalizedStrings;
using static Core.Role.RolesEnum;

namespace Core
{
	public class Faction
	{
		public string Name { get; set; }
		/// <summary>
		/// Func that returns a non-null string if a faction fulfills its win condition, where the string contains a description of the win reason
		/// </summary>
		public Func<Game, string?> CheckWinCondition { get; }
		public Func<Player, bool> IsPlayerInFaction { get; }

		public Faction(string name, Func<Game, string?> winCondition, Func<Player, bool> isPlayerInFaction)
		{
			CheckWinCondition = winCondition;
			IsPlayerInFaction = isPlayerInFaction;
			Name = name;
		}

		public static Faction Villagers = new Faction("Villagers",
			state =>
			{
				var villagerList = state.
				if (state.PlayerList.Count(player => player.IsInfected) > 0)
					return "true";
				return null;
			},
			player =>
			{
				return player.IsInfected == false &&
				       player.Roles.Exists(r => r != BigBadWolf && r != WolfFather && r != Werewolf) == false;
			});

		public static Faction Wolves = new Faction("Wolves",
			state =>
			{
				if (state.PlayerList.Count(player => player.IsInfected) > 0)
					return "true";
				return null;
			},
			player =>
			{
				return player.IsInfected == false &&
				       player.Roles.Exists(r => r != BigBadWolf && r != WolfFather && r != Werewolf) == false;
			});

		public static Faction InterracialLovers = new Faction("Interracial Lovers",
			state =>
			{
				if (state.PlayerList.Count(player => player.IsInfected) > 0)
					return "true";
				return null;
			},
			player =>
			{
				return player.IsInfected == false &&
				       player.Roles.Exists(r => r != BigBadWolf && r != WolfFather && r != Werewolf) == false;
			});
	}

	public interface IGameUI
	{
		public Task<string> RequestString(string request);
		public Task<int> RequestInt(string request);
		public Task ShowText(string text);
	}

	public enum GamePhaseEnum
	{
		//----- Night time characters
		NightTime,
		//Thief,
		//Actor,
		Cupid,
		Seer,
		Fox,
		Lovers,
		StutteringJudgeNight,
		TwoSisters,
		ThreeSisters,
		WildChild,
		BearTamer,
		Scandalmonger,
		Pyromaniac,
		Defender,
		AllWerewolves, //with little girl, don't forget!
		Baker,
		WhiteWerewolf,
		AccursedWolfFather,
		BigBadWolf,
		Witch,
		GypsyNight,
		Piper,
		CharmedPlayers,

		//----- Day time characters
		DayTime,
		DeathsRevealed,
		BearGrunt,
		Medium,
		LynchingVote, //with call to devoted servant and call of attention to stuttering judge signal if applicable
		StutteringJudge

	}

	public class ConditionalAction
	{
		private Func<Game, Task<bool>> _trigger;
		private Func<Game, Task> _action;

		public ConditionalAction(Func<Game, Task<bool>> trigger, Func<Game, Task> action)
		{
			_trigger = trigger;
			_action = action;
		}


	}

	public class GameConfigFile
	{

	}

	public class GameConfig
	{

	}

	public class Game
	{
		private IGameUI GameUI { get; set; }
		private List<Player> PlayerList { get; set; }
		GamePhaseEnum GamePhase { get; set; }
		public int RoundCounter { get; set; }
		private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
		private List<Tuple<Func<Game, bool>, Action<Game>>> DeferredActionList;

		public readonly List<Faction> FactionList = new() { };

		private async Task GameStateMachine()
		{
			if (await _semaphore.WaitAsync(0) == false)
			{

			}
		}


		public void AddPlayer(Player player)
		{
			
			PlayerList.Add(player);
		}
	}

	public enum RolesEnum
	{
		Werewolf,
		//BigBadWolf,
		WolfFather,
		WhiteWerewolf,
		SimpleVillager,
		VillagerVillager,
		Seer,
		Cupid,
		Witch,
		Hunter,
		LittleGirl,
		Defender,
		Elder,
		Scapegoat,
		Idiot,
		TwoSisters,
		ThreeBrothers,
		Fox,
		BearTamer,
		StutteringJudge,
		RustySwordKnight,
		Thief,
		DevotedServant,
		Actor,
		WildChild,
		WolfHound
	}

	public class Role
	{
		public int PowerUsesCounter { get; set; }

		public Func<Game, bool> CheckTrigger { get; }
		public Action<Game> ChangeGameState { get; }

		public Role(string name, Func<Game, bool> checkTrigger)
		{
			CheckTrigger = checkTrigger;
		}
	}

	public class Player
	{
		private static int PlayerCounter = 1;
		public readonly int PlayerId;
		public string Name { get; set; }
		public Dictionary<RolesEnum, Role> Roles { get; set; }
		public bool IsAlive { get; set; }
		public bool IsInfected { get; set; }
		
		public bool CanVote { get; set; }
		public int LynchVotingPower;
		public Player? Lover { get; set; }

		public async Task<Player> PlayerFactory(IGameUI gameUI)
		{
			var playerName = await gameUI.RequestString($"{RequestPlayerName}\n{EnterToContinue}");

			return new Player()
			
		}
		public Player(Role role)
		{
			PlayerId = PlayerCounter++;
			IsAlive = true;
			IsInfected = false;
			CanVote = true;
			LynchVotingPower = 1;
		}
	}
}
