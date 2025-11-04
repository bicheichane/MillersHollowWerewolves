using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.StateModels.Extensions
{
	public static class StringExtensions
	{
		public static string Format(this string value, params object[] arg) => string.Format(value, arg);
	}

	public static class PublicPlayerExtensions
	{
		public static IEnumerable<IPlayer> WithRole(this IEnumerable<IPlayer> players, RoleType? roleType) =>
			PlayerExtensionHelpers.WithRole(players, roleType);
		public static IEnumerable<IPlayer> WithoutRole(this IEnumerable<IPlayer> players, RoleType? roleType) =>
			PlayerExtensionHelpers.WithoutRole(players, roleType);

		public static IEnumerable<IPlayer> WithHealth(this IEnumerable<IPlayer> players, PlayerHealth health) =>
			PlayerExtensionHelpers.WithHealth(players, health);
		public static IEnumerable<IPlayer> WithHealth(this Dictionary<Guid, IPlayer> players, PlayerHealth health) =>
			PlayerExtensionHelpers.WithHealth(players.Values, health);

		public static IEnumerable<IPlayer> WhereRevealed(this IEnumerable<IPlayer> players, bool isRevealed) =>
			PlayerExtensionHelpers.WhereRevealed(players, isRevealed);

		public static IEnumerable<IPlayer> WithRole(this Dictionary<Guid, IPlayer> players, RoleType roleType) =>
			PlayerExtensionHelpers.WithRole(players.Values, roleType);

		public static IEnumerable<IPlayer> WhereRevealed(this Dictionary<Guid, IPlayer> players, bool isRevealed) =>
			PlayerExtensionHelpers.WhereRevealed(players.Values, isRevealed);

		public static List<Guid> ToIdList(this Dictionary<Guid, IPlayer> players) =>
			players.Select(p => p.Key).ToList();

		public static List<Guid> ToIdList(this IEnumerable<IPlayer> players) =>
			players.Select(p => p.Id).ToList();
	}

	internal static class InternalPlayerExtensions
	{
		internal static IEnumerable<Player> WithRole(this IEnumerable<Player> players, RoleType? roleType) =>
			PlayerExtensionHelpers.WithRole(players, roleType);
		internal static IEnumerable<Player> WithoutRole(this IEnumerable<Player> players, RoleType? roleType) =>
			PlayerExtensionHelpers.WithoutRole(players, roleType);

		internal static IEnumerable<Player> WithHealth(this IEnumerable<Player> players, PlayerHealth health) =>
			PlayerExtensionHelpers.WithHealth(players, health);
		internal static IEnumerable<Player> WithHealth(this Dictionary<Guid, Player> players, PlayerHealth health) =>
			PlayerExtensionHelpers.WithHealth(players.Values, health);

		internal static IEnumerable<Player> WhereRevealed(this IEnumerable<Player> players, bool isRevealed) =>
			PlayerExtensionHelpers.WhereRevealed(players, isRevealed);

		internal static IEnumerable<Player> WithRole(this Dictionary<Guid, Player> players, RoleType roleType) =>
			PlayerExtensionHelpers.WithRole(players.Values, roleType);

		internal static IEnumerable<Player> WhereRevealed(this Dictionary<Guid, Player> players, bool isRevealed) =>
			PlayerExtensionHelpers.WhereRevealed(players.Values, isRevealed);
	}

	file static class PlayerExtensionHelpers
	{
		internal static IEnumerable<T> WithRole<T>(this IEnumerable<T> players, RoleType? roleType) where T : IPlayer =>
			players.Where(p => p.State.Role == roleType);
		
		internal static IEnumerable<T> WithoutRole<T>(this IEnumerable<T> players, RoleType? roleType) where T : IPlayer =>
			players.Where(p => p.State.Role != roleType);

		internal static IEnumerable<T> WithHealth<T>(this IEnumerable<T> players, PlayerHealth health) where T : IPlayer =>
			players.Where(p => p.State.Health == health);

		internal static IEnumerable<T> WhereRevealed<T>(this IEnumerable<T> players, bool isRevealed) where T : IPlayer =>
			players.Where(p => p.State.IsRoleRevealed == isRevealed);
	}
}
