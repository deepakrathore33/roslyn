﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler.Completion;

namespace Microsoft.CodeAnalysis.ExternalAccess.Razor;

internal static class Constants
{
    public const string RazorLanguageName = LanguageInfoProvider.RazorLanguageName;

    public const string CompleteComplexEditCommand = CompletionResultFactory.CompleteComplexEditCommand;
}
