﻿using FluentAssertions;
using Microsoft.Build.Framework;
using Modules.TestHelper;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ResourceEmbedder.MsBuild.Tests
{
    public class SymbolTests
    {
        private static string AssemblyDirectory()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var codebase = new Uri(assembly.CodeBase);
            var path = codebase.LocalPath;
            return new FileInfo(path).DirectoryName;
        }

        [TestCase("None", "none", false)]
        [TestCase("Full", "full", true)]
        [TestCase("PdbOnly", "pdbonly", true)]
        [TestCase("Embedded", "embedded", false)]
        [TestCase("PortablePdb", "portable", true)]
        public void MsBuildBasedEmbeddingWithSymbols(string exeName, string symbols, bool hasSymbols)
        {
            // copy elsewhere and ensure localization works
            var copyDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
            var originalExe = Path.Combine(AssemblyDirectory(), $"{exeName}.exe");
            var output = Path.Combine(copyDir, $"{exeName}.exe");
            File.Copy(originalExe, output, true);
            var originalPdb = Path.ChangeExtension(originalExe, "pdb");
            var outputPdb = Path.ChangeExtension(output, "pdb");
            if (hasSymbols)
                File.Copy(originalPdb, outputPdb, true);

            var languages = new[] { "de", "pl" };
            foreach (var lang in languages)
            {
                Directory.CreateDirectory(Path.Combine(copyDir, lang));
                var res = Path.Combine(copyDir, $"{lang}\\{exeName}.resources.dll");
                File.Copy(Path.Combine(AssemblyDirectory(), $"{lang}\\{exeName}.resources.dll"), res, true);
            }

            var fakeEngine = NSubstitute.Substitute.For<IBuildEngine>();

            var task = new SatelliteAssemblyEmbedderTask
            {
                ProjectDirectory = ".",
                AssemblyPath = output,
                TargetPath = output,
                BuildEngine = fakeEngine,
                References = new ITaskItem[0],
                DebugSymbols = true,
                DebugType = symbols
            };
            task.Execute().Should().BeTrue();
            File.Exists(outputPdb).Should().Be(hasSymbols);

            var p = Process.Start(output);
            p.WaitForExit(3000).Should().BeTrue();
            p.ExitCode.Should().Be(0);
            Directory.Delete(copyDir, true);
        }

        [TestCase("None", "none", false)]
        [TestCase("Full", "full", true)]
        [TestCase("PdbOnly", "pdbonly", true)]
        [TestCase("Embedded", "embedded", false)]
        [TestCase("PortablePdb", "portable", true)]
        public void MsBuildBasedEmbeddingWithPortableSymbolsWorksInNetCore(string exeName, string symbols, bool hasSymbolFile)
        {
            // copy elsewhere and ensure localization works
            var copyDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
            var configuration =
#if DEBUG
                "Debug";
#else
                "Release";
#endif
            // must copy from original dir as .Net Core 2.2. can't be referenced from full framework..
            // not ideal as it doesn't ensure build is up to date..
            // also must copy multiple files for .net core

            var originalDir = $"{RepositoryLocator.Locate(RepositoryDirectory.SourceCode)}\\testmodules\\Symbols\\NetCore\\NetCore{exeName}\\bin\\{configuration}\\netcoreapp3.1";
            var output = Path.Combine(copyDir, $"NetCore{exeName}.dll");
            var outputPdb = Path.Combine(copyDir, $"NetCore{exeName}.pdb");
            var toCopy = Directory.GetFiles(originalDir, "*.*", SearchOption.AllDirectories);
            foreach (var srcFile in toCopy)
            {
                var target = srcFile.Substring(originalDir.Length + 1);
                if (target.Contains("\\"))
                    Directory.CreateDirectory(Path.Combine(copyDir, target.Substring(0, target.LastIndexOf("\\"))));
                File.Copy(srcFile, Path.Combine(copyDir, target));
            }

            var fakeEngine = NSubstitute.Substitute.For<IBuildEngine>();

            var task = new SatelliteAssemblyEmbedderTask
            {
                ProjectDirectory = ".",
                AssemblyPath = output,
                TargetPath = output,
                BuildEngine = fakeEngine,
                References = new ITaskItem[0],
                DebugSymbols = true,
                DebugType = symbols
            };
            task.Execute().Should().BeTrue();
            File.Exists(output).Should().BeTrue();
            File.Exists(outputPdb).Should().Be(hasSymbolFile);

            var p = Process.Start("dotnet", output);
            p.WaitForExit(3000).Should().BeTrue();
            p.ExitCode.Should().Be(0);
            Directory.Delete(copyDir, true);
        }

        [Test]
        public void MsBuildBasedEmbeddingAndCleanup()
        {
            var msBuild = Path.Combine(AssemblyDirectory(), "MsBuildBasedInjected.exe");
            if (File.Exists(msBuild))
            {
                File.Delete(msBuild);
            }
            var de = Path.Combine(AssemblyDirectory(), "de\\MsBuildBasedInjected.resources.dll");
            File.Copy(Path.Combine(AssemblyDirectory(), "de\\WpfTest.resources.dll"), de, true);
            // de is german in generall, de-DE is german specific to germany -> if someone has set his localitation to German (Germany) he would get de-DE, if he sets it to e.g. German (Austria) he should get "de"
            // let's ensure that both levels of localization are correctly embedded
            var deDe = Path.Combine(AssemblyDirectory(), "de-DE\\MsBuildBasedInjected.resources.dll");
            File.Copy(Path.Combine(AssemblyDirectory(), "de-DE\\WpfTest.resources.dll"), deDe, true);
            var fr = Path.Combine(AssemblyDirectory(), "fr\\MsBuildBasedInjected.resources.dll");
            File.Copy(Path.Combine(AssemblyDirectory(), "fr\\WpfTest.resources.dll"), fr, true);
            File.Copy(Path.Combine(AssemblyDirectory(), "WpfTest.exe"), msBuild);

            // delete PDB as it doesn't match the exe anyway
            if (File.Exists(Path.ChangeExtension(msBuild, "pdb")))
                File.Delete(Path.ChangeExtension(msBuild, "pdb"));

            var fakeEngine = NSubstitute.Substitute.For<IBuildEngine>();
            var task = new SatelliteAssemblyEmbedderTask
            {
                ProjectDirectory = ".",
                AssemblyPath = msBuild,
                DebugType = "none",
                TargetPath = Path.GetFullPath(msBuild),
                BuildEngine = fakeEngine,
                References = new ITaskItem[0]
            };
            task.Execute().Should().BeTrue();
            task.EmbeddedCultures.Should().ContainAll("de;", "de-DE", "fr");

            File.Exists(de).Should().BeTrue();
            File.Exists(deDe).Should().BeTrue();
            File.Exists(fr).Should().BeTrue();
            var cleanupTask = new SatelliteAssemblyCleanupTask
            {
                ProjectDirectory = ".",
                AssemblyPath = msBuild,
                TargetPath = Path.GetFullPath(msBuild),
                EmbeddedCultures = task.EmbeddedCultures,
                BuildEngine = fakeEngine
            };
            cleanupTask.Execute().Should().BeTrue();
            File.Exists(de).Should().BeFalse();
            File.Exists(deDe).Should().BeFalse();
            File.Exists(fr).Should().BeFalse();

            var p = Process.Start(msBuild, "/testFullyProcessed");
            p.WaitForExit(2000).Should().BeTrue();
            p.ExitCode.Should().Be(0);
            File.Delete(de);
            File.Delete(deDe);
            File.Delete(fr);

            // assert we no longer have file lock issue
            File.Exists(msBuild).Should().BeTrue();
            File.Delete(msBuild);
            File.Exists(msBuild).Should().BeFalse();
        }
    }
}
