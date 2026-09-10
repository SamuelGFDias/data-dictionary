using System.Collections;
using System.Linq.Expressions;
using DataDictionary.Abstractions.Manifest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DataDictionary.EntityFrameworkCore;

/// <summary>
/// Converts a marked enum's CLR value to and from its resolved dictionary
/// <see cref="Abstractions.Persistence.DictionaryEntry.Code"/> when persisting a business
/// entity property of that enum type (FR-012). One instance is scoped to a single enum
/// type (<typeparamref name="TEnum"/>); <see cref="EnumCodeValueConverterExtensions.ApplyEnumCodeConverters"/>
/// creates and applies one per marked enum found in a <see cref="DataDictionaryManifest"/>.
/// </summary>
/// <typeparam name="TEnum">The marked enum's CLR type.</typeparam>
public sealed class EnumCodeValueConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Creates a converter for <typeparamref name="TEnum"/> using the given member-to-code
    /// mapping (normally built from a <see cref="ManifestEnumEntry"/>'s
    /// <see cref="ManifestEnumEntry.Members"/>).
    /// </summary>
    /// <param name="codeByValue">Every dictionary-eligible member of
    /// <typeparamref name="TEnum"/>, keyed by its enum value, mapped to its resolved
    /// dictionary code.</param>
    public EnumCodeValueConverter(IReadOnlyDictionary<TEnum, string> codeByValue)
        : base(
            value => ToCode(value, codeByValue),
            BuildFromCode(codeByValue))
    {
    }

    private static string ToCode(TEnum value, IReadOnlyDictionary<TEnum, string> codeByValue)
    {
        if (!codeByValue.TryGetValue(value, out var code))
        {
            throw new InvalidOperationException(
                $"No dictionary code is registered for '{typeof(TEnum).FullName}.{value}'. " +
                "Every dictionary-eligible member of a marked enum must appear in the " +
                "generated manifest.");
        }

        return code;
    }

    /// <summary>
    /// Builds the code-to-value lookup once, at converter construction time (which itself
    /// happens once per marked enum, during <c>OnModelCreating</c>/model building — not per
    /// request), and returns an expression closed over it for the <see cref="ValueConverter{TModel,TProvider}"/>
    /// base constructor (which requires an <see cref="Expression{TDelegate}"/>, not a plain
    /// delegate, so it can compile and cache the conversion). This converter runs on every
    /// EF Core read/write touching a marked-enum property — the project's one genuinely
    /// hot-path per business request (Constitution Principle IV's reflection-avoidance
    /// concern applies here to raw algorithmic cost instead: without this inversion,
    /// <c>FromCode</c> would linearly scan <paramref name="codeByValue"/> on every call).
    /// <see cref="StringComparer.Ordinal"/> preserves the original comparer-less
    /// <c>string</c> equality (<c>==</c>) used by the previous linear scan.
    /// </summary>
    private static Expression<Func<string, TEnum>> BuildFromCode(IReadOnlyDictionary<TEnum, string> codeByValue)
    {
        var valueByCode = new Dictionary<string, TEnum>(codeByValue.Count, StringComparer.Ordinal);

        foreach (var pair in codeByValue)
        {
            valueByCode[pair.Value] = pair.Key;
        }

        return code => Lookup(code, valueByCode);
    }

    private static TEnum Lookup(string code, Dictionary<string, TEnum> valueByCode)
    {
        if (valueByCode.TryGetValue(code, out var value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"No member of '{typeof(TEnum).FullName}' is registered for dictionary code " +
            $"'{code}'.");
    }
}

/// <summary>
/// Applies <see cref="EnumCodeValueConverter{TEnum}"/> to every business entity property
/// whose CLR type is a marked enum, per <c>contracts/generated-entrypoints-contract.md</c>
/// point 2 — "zero hand-written converter registration by the consumer".
/// </summary>
public static class EnumCodeValueConverterExtensions
{
    /// <summary>
    /// Scans every entity type already added to <paramref name="modelBuilder"/>'s model and,
    /// for each marked enum in <paramref name="manifest"/>, applies an
    /// <see cref="EnumCodeValueConverter{TEnum}"/> — built from that enum's manifest member
    /// codes — to every property whose CLR type (or, for a nullable property, its underlying
    /// type) is that enum.
    /// </summary>
    /// <param name="modelBuilder">The model builder whose already-discovered entity types are
    /// scanned. Call this after the business entities are added to the model (e.g. after
    /// <c>OnModelCreating</c>'s own <c>modelBuilder.Entity&lt;...&gt;()</c> calls, or after
    /// conventions have added them).</param>
    /// <param name="manifest">The compiled manifest (normally the generated
    /// <c>DataDictionaryManifest.Default</c>) naming every marked enum.</param>
    /// <returns><paramref name="modelBuilder"/>, for chaining.</returns>
    public static ModelBuilder ApplyEnumCodeConverters(
        this ModelBuilder modelBuilder,
        DataDictionaryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(manifest);

        foreach (var enumEntry in manifest.Enums)
        {
            var enumType = ResolveEnumType(enumEntry);

            if (enumType is null)
            {
                continue;
            }

            var converter = CreateConverter(enumType, enumEntry);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    var propertyClrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                    if (propertyClrType == enumType)
                    {
                        property.SetValueConverter(converter);
                    }
                }
            }
        }

        return modelBuilder;
    }

    private static Type? ResolveEnumType(ManifestEnumEntry enumEntry)
    {
        var assemblyQualifiedName = $"{enumEntry.ClrFullName}, {enumEntry.AssemblyName}";
        var type = Type.GetType(assemblyQualifiedName, throwOnError: false);
        return type is { IsEnum: true } ? type : null;
    }

    private static ValueConverter CreateConverter(Type enumType, ManifestEnumEntry enumEntry)
    {
        var codeByValue = (IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(enumType, typeof(string)))!;

        foreach (var member in enumEntry.Members)
        {
            var value = Enum.Parse(enumType, member.FieldName);
            codeByValue[value] = member.Code;
        }

        var converterType = typeof(EnumCodeValueConverter<>).MakeGenericType(enumType);
        return (ValueConverter)Activator.CreateInstance(converterType, codeByValue)!;
    }
}
