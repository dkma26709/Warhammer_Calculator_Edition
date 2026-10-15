using Warhammer.Models.Enums;

namespace Warhammer.Models.Domain;

/// <summary>
/// A sub-option a command model takes — the Leader's Hochland long rifle, the Standard Bearer's
/// Look-out Gnoblar. Held as indices into the book's options, like the other selections.
/// </summary>
/// <param name="OptionIndex">Index into the unit's options: the command option.</param>
/// <param name="SubOptionIndex">Index into that option's sub-options.</param>
/// <param name="ChoiceIndex">Index into the sub-option's choices; always 0 for an upgrade, which has none.</param>
public sealed record CommandUpgradePick(int OptionIndex, int SubOptionIndex, int ChoiceIndex);

/// <summary>A command upgrade pick resolved against the book.</summary>
/// <param name="Role">The command model that carries it.</param>
/// <param name="Name">The item or upgrade as the Builder and the roster name it.</param>
/// <param name="Replaces">The gear it replaces on that model, or null when it is added.</param>
/// <param name="Points">What it costs, once: a command model is one model.</param>
public sealed record TakenCommandUpgrade(CommandRole Role, string Name, string? Replaces, double Points);
