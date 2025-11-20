using System.Data;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;


namespace Werewolves.StateModels.Extensions
{
	public static class StringExtensions
	{
		public static string Format(this string value, params object[] arg) => string.Format(value, arg);
	}

	public static class PublicPlayerExtensions
	{
		public static IEnumerable<IPlayer> WithRole(this IEnumerable<IPlayer> players, MainRoleType? roleType) =>
			PlayerExtensionHelpers.WithRole(players, roleType);
		public static IEnumerable<IPlayer> WithoutRole(this IEnumerable<IPlayer> players, MainRoleType? roleType) =>
			PlayerExtensionHelpers.WithoutRole(players, roleType);

		public static IEnumerable<IPlayer> WithHealth(this IEnumerable<IPlayer> players, PlayerHealth health) =>
			PlayerExtensionHelpers.WithHealth(players, health);
		public static IEnumerable<IPlayer> WithHealth(this Dictionary<Guid, IPlayer> players, PlayerHealth health) =>
			PlayerExtensionHelpers.WithHealth(players.Values, health);

		public static IEnumerable<IPlayer> WithRole(this Dictionary<Guid, IPlayer> players, MainRoleType mainRoleType) =>
			PlayerExtensionHelpers.WithRole(players.Values, mainRoleType);

		public static List<Guid> ToIdList(this Dictionary<Guid, IPlayer> players) =>
			players.Select(p => p.Key).ToList();

		public static List<Guid> ToIdList(this IEnumerable<IPlayer> players) =>
			players.Select(p => p.Id).ToList();
	}

	internal static class PlayerExtensionHelpers
	{
		internal static IEnumerable<T> WithRole<T>(this IEnumerable<T> players, MainRoleType? roleType) where T : IPlayer =>
			players.Where(p => p.State.MainRole == roleType);
		
		internal static IEnumerable<T> WithoutRole<T>(this IEnumerable<T> players, MainRoleType? roleType) where T : IPlayer =>
			players.Where(p => p.State.MainRole != roleType);

		internal static IEnumerable<T> WithSecondaryRole<T>(this IEnumerable<T> players, SecondaryRoleType? roleType)
			where T : IPlayer =>
			players.Where(p =>
				(p.State.SecondaryRoles & roleType) == roleType);

		internal static IEnumerable<T> WithHealth<T>(this IEnumerable<T> players, PlayerHealth health) where T : IPlayer =>
			players.Where(p => p.State.Health == health);
	}

	/*
protected static class InternalPlayerExtensions
{
	internal static IEnumerable<Player> WithRole(this IEnumerable<Player> players, MainRoleType? mainRoleType) =>
		PlayerExtensionHelpers.WithRole(players, mainRoleType);
	internal static IEnumerable<Player> WithoutRole(this IEnumerable<Player> players, MainRoleType? mainRoleType) =>
		PlayerExtensionHelpers.WithoutRole(players, mainRoleType);

	internal static IEnumerable<Player> WithHealth(this IEnumerable<Player> players, PlayerHealth health) =>
		PlayerExtensionHelpers.WithHealth(players, health);
	internal static IEnumerable<Player> WithHealth(this Dictionary<Guid, Player> players, PlayerHealth health) =>
		PlayerExtensionHelpers.WithHealth(players.Values, health);

	internal static IEnumerable<Player> WhereRevealed(this IEnumerable<Player> players, bool isRevealed) =>
		PlayerExtensionHelpers.WhereRevealed(players, isRevealed);

	internal static IEnumerable<Player> WithRole(this Dictionary<Guid, Player> players, MainRoleType mainRoleType) =>
		PlayerExtensionHelpers.WithRole(players.Values, mainRoleType);

	internal static IEnumerable<Player> WhereRevealed(this Dictionary<Guid, Player> players, bool isRevealed) =>
		PlayerExtensionHelpers.WhereRevealed(players.Values, isRevealed);
}
*/
}
