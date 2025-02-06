// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using Roslyn.Utilities;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using System.Threading;

namespace Microsoft.CodeAnalysis.MSBuild
{
    public partial class MSBuildProjectLoader
    {
        private static class SlnxSolutionReader
        {
            public static bool IsSlnxFilename(string filename)
            {
                return Path.GetExtension(filename).Equals(".slnx", StringComparison.OrdinalIgnoreCase);
            }

            public static bool TryRead(string filterFilename, out ImmutableHashSet<string> projectFilter)
            {
                try
                {
                    // Get the serializer for the solution file
                    ISolutionSerializer? serializer = SolutionSerializers.GetSerializerByMoniker(filterFilename);
                    SolutionModel solutionModel;
                    var projectPaths = ImmutableHashSet.CreateBuilder<string>();

                    if (serializer != null)
                    {

                        solutionModel = serializer.OpenAsync(filterFilename, CancellationToken.None).Result;
                        foreach (SolutionProjectModel projectModel in solutionModel.SolutionProjects)
                        {
                            projectPaths.Add(projectModel.FilePath);
                        }

                    }
                    if (projectPaths.Count > 0)
                    {
                        projectFilter = projectPaths.ToImmutable();
                        return true;
                    } else {
                        projectFilter = [];
                        return false;
                    }
                }
                catch
                {
                    projectFilter = [];
                    return false;
                }
            }
        }
    }
}
