using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Models;


/// <summary>
/// A unified identifier for different types of hook listeners (Roles, Events).
/// Used to accommodate different types of listeners in the hook system.
/// </summary>
public record ListenerIdentifier
{
    /// <summary>
    /// The type of listener (Role or Event).
    /// </summary>
    public GameHookListenerType ListenerType { get; }
    
    /// <summary>
    /// Stores the RoleType or EventCardType enum value as a string for better debugging/logging.
    /// </summary>
    public string ListenerId { get; }

    private ListenerIdentifier(GameHookListenerType listenerType, string listenerId)
    {
        ListenerType = listenerType;
        ListenerId = listenerId;
    }

    public static ListenerIdentifier Create<T>(T listenerEnum) where T : struct, Enum
    {
        var listenerId = listenerEnum.ToString();
        GameHookListenerType listenerType;
        if (typeof(T) == typeof(RoleType))
        {
            listenerType = GameHookListenerType.Role;
        }
        /*
        else if (typeof(T) == typeof(EventCardType))
        {
            listenerType = GameHookListenerType.Event;
        } */
        else
        {
            throw new ArgumentException("ListenerIdentifier can only be created for RoleType or EventCardType enums.");
        }

        return new ListenerIdentifier(listenerType, listenerId);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ListenerType, ListenerId);
    }

    public override string ToString()
    {
        return $"{ListenerType}:{ListenerId}";
    }

    //create impilicit conversion from RoleType to ListenerIdentifier
    public static implicit operator ListenerIdentifier(RoleType roleType)
    {
        return Create(roleType);
    }

    //create implicit conversion from ListernerIdentifier to RoleType
    public static implicit operator RoleType(ListenerIdentifier listenerIdentifier)
    {
        if (listenerIdentifier.ListenerType != GameHookListenerType.Role)
        {
            throw new InvalidCastException("ListenerIdentifier is not of type Role.");
        }
        if (Enum.TryParse<RoleType>(listenerIdentifier.ListenerId, out var roleType))
        {
            return roleType;
        }
        throw new InvalidCastException("ListenerIdentifier ListenerId could not be parsed to RoleType.");
    }

    //create implicit conversion from EventCardType to ListenerIdentifier
    /*public static implicit operator ListenerIdentifier(EventCardType eventCardType)
    {
        return Create(eventCardType);
    } */
    //create implicit conversion from ListenerIdentifier to EventCardType
    /*public static implicit operator EventCardType(ListenerIdentifier listenerIdentifier)
    {
        if (listenerIdentifier.ListenerType != GameHookListenerType.Event)
        {
            throw new InvalidCastException("ListenerIdentifier is not of type Event.");
        }
        if (Enum.TryParse<EventCardType>(listenerIdentifier.ListenerId, out var eventCardType))
        {
            return eventCardType;
        }
        throw new InvalidCastException("ListenerIdentifier ListenerId could not be parsed to EventCardType.");
    } */
}
