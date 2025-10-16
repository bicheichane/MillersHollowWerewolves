using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Werewolves.Core.Enums;
using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;

namespace Werewolves.Core.Extensions
{
	public static class StringExtensions
	{
		public static string Format(this string value, params object[] arg) => string.Format(value, arg);

		public static IEnumerable<Player> WithRole(this IEnumerable<Player> players, RoleType roleType) =>
			players.Where(p => p.Role?.RoleType == roleType);
		
		public static IEnumerable<Player> WithHealth(this IEnumerable<Player> players, PlayerHealth health) =>
			players.Where(p => p.Health == health);
		public static IEnumerable<Player> WithHealth(this Dictionary<Guid, Player> players, PlayerHealth health) =>
			players.Values.Where(p => p.Health == health);


		public static IEnumerable<Player> WhereRevealed(this IEnumerable<Player> players, bool isRevealed) =>
			players.Where(p => p.IsRoleRevealed == isRevealed);

		public static IEnumerable<Player> WithRole(this Dictionary<Guid, Player> players, IRole role) =>
			players.Values.Where(p => p.Role?.RoleType == role.RoleType);
		public static IEnumerable<Player> WithRole(this Dictionary<Guid, Player> players, RoleType roleType) =>
			players.Values.Where(p => p.Role?.RoleType == roleType);
		

		public static IEnumerable<Player> WhereRevealed(this Dictionary<Guid, Player> players, bool isRevealed) =>
			players.Values.Where(p => p.IsRoleRevealed == isRevealed);


		public static IEnumerable<Player> WithState(this IEnumerable<Player> source, PlayerState filter) => WithStateCore(source, filter);
		public static IEnumerable<Player> WithState(this Dictionary<Guid, Player> source, PlayerState filter) => WithStateCore(source.Values, filter);

		private static IEnumerable<Player> WithStateCore(this IEnumerable<Player> source, object filter)
		{
			var filterProperties = filter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
			var sourceType = typeof(Player);

			var filteredSource = source;

			foreach (var property in filterProperties)
			{
				var sourceProperty = sourceType.GetProperty(property.Name);
				if (sourceProperty != null && sourceProperty.CanRead)
				{
					var value = property.GetValue(filter);

					// Construct the expression p => p.PropertyName == value
					var parameter = Expression.Parameter(sourceType, "p");
					var propertyAccess = Expression.Property(parameter, sourceProperty);
					var constantValue = Expression.Constant(value);
					var equality = Expression.Equal(propertyAccess, constantValue);
					var lambda = Expression.Lambda<Func<Player, bool>>(equality, parameter);

					filteredSource = filteredSource.Where(lambda.Compile());
				}
			}

			return filteredSource;
		}
	}
}
