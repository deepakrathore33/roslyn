﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Shared.Preview;
using Microsoft.CodeAnalysis.Editor.Shared.Tagging;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Preview;

[Export(typeof(ITaggerProvider))]
[TagType(typeof(PreviewWarningTag))]
[ContentType(ContentTypeNames.RoslynContentType)]
[ContentType(ContentTypeNames.XamlContentType)]
[TextViewRole(TextViewRoles.PreviewRole)]
internal sealed class PreviewWarningTaggerProvider
    : AbstractPreviewTaggerProvider<PreviewWarningTag>
{
    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public PreviewWarningTaggerProvider()
        : base(PredefinedPreviewTaggerKeys.WarningSpansKey, PreviewWarningTag.Instance)
    {
    }
}
