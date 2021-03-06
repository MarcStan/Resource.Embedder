﻿using Mono.Cecil;
using System;
using System.IO;
using System.Linq;

namespace ResourceEmbedder.Core.Cecil
{
    /// <summary>
    /// Implementation that uses Cecil to embedd resources into .Net assemblies.
    /// </summary>
    public class CecilBasedResourceEmbedder : IEmbedResources
    {
        /// <summary>
        /// Creates a new cecil based instance.
        /// </summary>
        /// <param name="logger"></param>
        public CecilBasedResourceEmbedder(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException("logger");
        }

        /// <summary>
        /// The logger used during the embedding.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Call to embedd the provided set of resources into the specific assembly.
        /// Uses the <see cref="Logger"/> to issue log messages.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="resourcesToEmbedd"></param>
        /// <returns></returns>
        public bool EmbedResources(AssemblyDefinition assembly, ResourceInfo[] resourcesToEmbedd)
        {
            if (assembly == null || resourcesToEmbedd == null)
            {
                throw new ArgumentException();
            }
            if (resourcesToEmbedd.Length == 0)
            {
                throw new ArgumentException("No resources to embed");
            }
            try
            {
                foreach (var res in resourcesToEmbedd)
                {
                    if (!File.Exists(res.FullPathOfFileToEmbedd))
                    {
                        Logger.Error("Could not locate file '{0}' for embedding.", res.FullPathOfFileToEmbedd);
                        return false;
                    }
                    try
                    {
                        var bytes = File.ReadAllBytes(res.FullPathOfFileToEmbedd);
                        var resource = assembly.MainModule.Resources.FirstOrDefault(r => r.Name == res.RelativePathInAssembly);
                        if (resource != null)
                        {
                            // remove the old resource if there is any
                            assembly.MainModule.Resources.Remove(resource);
                        }
                        assembly.MainModule.Resources.Add(new EmbeddedResource(res.RelativePathInAssembly, ManifestResourceAttributes.Private, bytes));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Embedding task failed for resource {0}. Could not embedd into {1}. {2}", res.FullPathOfFileToEmbedd, assembly.Name, ex.Message);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
            return true;
        }
    }
}
