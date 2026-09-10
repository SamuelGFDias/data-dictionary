#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler-recognized marker type that makes <c>init</c>-only accessors (and hence
    /// positional <c>record</c> declarations) usable from a compilation targeting
    /// <c>netstandard2.0</c>, which does not ship this type in its reference
    /// assemblies. Never referenced directly from source; the C# compiler emits
    /// references to it for every <c>init</c> accessor. Mirrors
    /// <c>DataDictionary.Abstractions.Internal.Polyfills</c> — the generator project
    /// cannot reference Abstractions (see the project's own records' remarks), so it
    /// carries its own copy of this polyfill too.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
#endif
