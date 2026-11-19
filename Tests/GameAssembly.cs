using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Perun.Tests;

internal static class GameAssembly
{
    // The game's enums live in Assembly-CSharp, which the build references as hollowed.dll; the runtime looks it up by
    // its assembly name, so point that name at the copied file.
    [ModuleInitializer]
    internal static void Register()
    {
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            if (name.Name != "Assembly-CSharp")
            {
                return null;
            }
            string path = Path.Combine(Path.GetDirectoryName(typeof(GameAssembly).Assembly.Location)!, "hollowed.dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
    }
}
