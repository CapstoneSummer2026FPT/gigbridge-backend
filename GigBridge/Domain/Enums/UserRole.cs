using System;

namespace Domain.Enums;

public enum UserRole
{
    Client = 0,
    Freelancer = 1,
    Admin = 2
}

public static class UserRoleExtensions
{
    // Convert UserRole enum to its corresponding integer number
    public static int ToNumber(this UserRole role)
    {
        return (int)role;
    }

    // Convert integer number to UserRole enum (with boundary validation)
    public static UserRole ToUserRole(this int roleNumber)
    {
        if (Enum.IsDefined(typeof(UserRole), roleNumber))
        {
            return (UserRole)roleNumber;
        }
        throw new ArgumentOutOfRangeException(nameof(roleNumber), $"Invalid role number: {roleNumber}");
    }

    // Parse role from string (case-insensitive) to UserRole enum
    public static UserRole ParseRole(string roleName)
    {
        if (Enum.TryParse<UserRole>(roleName, true, out var result))
        {
            return result;
        }
        throw new ArgumentException($"Invalid role name: {roleName}");
    }
}
