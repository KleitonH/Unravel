using System.Text.RegularExpressions;
using Unravel.Domain.Exceptions;

namespace Unravel.Domain.ValueObjects;

public sealed class Email : IEquatable<Email>
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToLowerInvariant();

        if (!EmailRegex.IsMatch(normalized))
            throw new DomainException($"'{value}' is not a valid email address.");

        return new Email(normalized);
    }

    public static implicit operator string(Email email) => email.Value;

    public override string ToString() => Value;

    public bool Equals(Email? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is Email other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();
}
