using Warhammer.Models.Enums;

namespace Warhammer.Models.Domain;

/// <summary>
/// A fully-resolved close combat weapon with pre-parsed strength and combat rules.
/// Immutable — created once during data loading.
/// </summary>
public sealed record Weapon
{
    public required CloseCombatWeaponKey Key { get; init; }
    public required string Name { get; init; }
    public required WeaponStrength Strength { get; init; }
    public required ParsedWeaponRules Rules { get; init; }
}

/// <summary>
/// Pre-parsed weapon strength: either "as user", "+N modifier", or "fixed N".
/// Replaces the string-based <c>CoreRulesService.ParseWeaponStrength</c>.
/// </summary>
public abstract record WeaponStrength
{
    public abstract int Resolve(int userStrength);

    public sealed record AsUser : WeaponStrength
    {
        public override int Resolve(int userStrength) => userStrength;
    }

    public sealed record Modifier(int Value) : WeaponStrength
    {
        public override int Resolve(int userStrength) => userStrength + Value;
    }

    public sealed record Fixed(int Value) : WeaponStrength
    {
        public override int Resolve(int userStrength) => Value;
    }

    public static WeaponStrength Parse(string field)
    {
        if (string.Equals(field, "As user", StringComparison.OrdinalIgnoreCase))
            return new AsUser();

        if (field.StartsWith('+') && int.TryParse(field, out var mod))
            return new Modifier(mod);

        if (int.TryParse(field, out var fixedVal))
            return new Fixed(fixedVal);

        return new AsUser();
    }
}
