using AscensionBot.AI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AscensionBot
{
    class BotLoader
    {
        readonly IDictionary<string, string> assemblies = new Dictionary<string, string>();

#pragma warning disable 0649
        [ImportMany(typeof(IBot), AllowRecomposition = true)]
        List<IBot> bots;
#pragma warning restore 0649

        AggregateCatalog catalog = new AggregateCatalog();
        CompositionContainer container;

        public BotLoader()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var name = new AssemblyName(args.Name).Name;

                // Return an assembly already loaded in this AppDomain. This is what
                // resolves references like "AscensionBot" from the bot plugins.
                // NOTE: do NOT call Assembly.Load(name) here — if the name can't be
                // resolved it re-raises AssemblyResolve and recurses to a StackOverflow.
                var loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == name);
                if (loaded != null)
                    return loaded;

                // Otherwise try to load it from our own folder by file path.
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var candidate = Path.Combine(dir, name + ".dll");
                if (File.Exists(candidate))
                    return Assembly.LoadFrom(candidate);

                // Give up gracefully instead of recursing.
                return null;
            };
        }

        internal List<IBot> ReloadBots()
        {
            bots?.Clear();
            container?.Dispose();

            var currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // The profiles we ship: Primalist (CasterBot.dll), Melee (classless), Pyromancer
            // and Felsworn Infernal. The old class-specific bot DLLs are no longer loaded.
            var botPaths = new[] { "CasterBot.dll", "MeleeBot.dll", "PyromancerBot.dll", "FelswornInfernalBot.dll" };

            foreach (var botPath in botPaths)
            {
                var path = Path.Combine(currentFolder, botPath);
                var assembly = Assembly.Load(File.ReadAllBytes(path));
                var assemblyName = assembly.FullName.Split(',')[0];
                if (assemblies.ContainsKey(assemblyName))
                {
                    if (assemblies[assemblyName] != assembly.FullName)
                    {
                        catalog.Catalogs.Add(new AssemblyCatalog(assembly));
                        assemblies[assemblyName] = assembly.FullName;
                    }
                }
                else
                {
                    catalog.Catalogs.Add(new AssemblyCatalog(assembly));
                    assemblies.Add(assemblyName, assembly.FullName);
                }
            }
            container = new CompositionContainer(catalog);
            container.ComposeParts(this);

            return bots
                .GroupBy(b => b.Name)
                .Select(b => b.Last())
                .ToList();
        }
    }
}
