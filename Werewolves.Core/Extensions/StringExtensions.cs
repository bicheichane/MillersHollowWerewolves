using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Werewolves.Core.Enums;
using Werewolves.Core.Models;

namespace Werewolves.Core.Extensions
{
	public static class StringExtensions
	{
		public static string Format(this string value, params object[] arg) => string.Format(value, arg);

		public static List<Player> WhereRole(this IEnumerable<Player> players, RoleType roleType) =>
			players.Where(p => p.Role?.RoleType == roleType).ToList();
		public static List<Player> WhereStatus(this IEnumerable<Player> players, PlayerStatus status) =>
			players.Where(p => p.Status == status).ToList();

		public static List<Player> WhereRevealed(this IEnumerable<Player> players, bool isRevealed) =>
			players.Where(p => p.IsRoleRevealed == isRevealed).ToList();
	}
}
