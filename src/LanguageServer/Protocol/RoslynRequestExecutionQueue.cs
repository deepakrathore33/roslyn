﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

namespace Microsoft.CodeAnalysis.LanguageServer;

internal sealed class RoslynRequestExecutionQueue : RequestExecutionQueue<RequestContext>
{
    private readonly IInitializeManager _initializeManager;

    /// <summary>
    /// Serial access is guaranteed by the queue.
    /// </summary>
    private CultureInfo? _cultureInfo;

    public RoslynRequestExecutionQueue(AbstractLanguageServer<RequestContext> languageServer, ILspLogger logger, AbstractHandlerProvider handlerProvider)
        : base(languageServer, logger, handlerProvider)
    {
        _initializeManager = languageServer.GetLspServices().GetRequiredService<IInitializeManager>();
    }

    public override async Task WrapStartRequestTaskAsync(Task requestTask, bool rethrowExceptions)
    {
        try
        {
            await requestTask.ConfigureAwait(false);
        }
        catch (Exception) when (!rethrowExceptions)
        {
            // The caller has asked us to not rethrow, so swallow the exception to avoid bringing down the queue.
            // The queue item task itself already handles reporting the exception (if any).
        }
    }

    protected internal override void BeforeRequest<TRequest>(TRequest request)
    {
        // Update the locale for this request to the desired LSP locale.
        CultureInfo.CurrentUICulture = GetCultureForRequest();
    }

    /// <summary>
    /// Serial access is guaranteed by the queue.
    /// </summary>
    private CultureInfo GetCultureForRequest()
    {
        if (_cultureInfo != null)
        {
            return _cultureInfo;
        }

        var initializeParams = _initializeManager.TryGetInitializeParams();
        if (initializeParams == null)
        {
            // Initialize has not been called yet, no culture to set.
            // Don't update the _cultureInfo since we don't know what it should be.
            return CultureInfo.CurrentUICulture;
        }

        var locale = initializeParams.Locale;
        if (string.IsNullOrWhiteSpace(locale))
        {
            // The client did not provide a culture, use the OS configured value
            // and remember that so we can short-circuit from now on.
            _cultureInfo = CultureInfo.CurrentUICulture;
            return _cultureInfo;
        }

        try
        {
            // Parse the LSP locale into a culture and remember it for future requests.
            _cultureInfo = CultureInfo.CreateSpecificCulture(locale);
            return _cultureInfo;
        }
        catch (CultureNotFoundException)
        {
            // We couldn't parse the culture, log a warning and fallback to the OS configured value.
            // Also remember the fallback so we don't warn on every request.
            _logger.LogWarning($"Culture {locale} was not found, falling back to OS culture");
            _cultureInfo = CultureInfo.CurrentUICulture;
            return _cultureInfo;
        }
    }
}
